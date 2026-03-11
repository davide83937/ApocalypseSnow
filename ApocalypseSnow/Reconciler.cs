using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace ApocalypseSnow;

/// <summary>
/// Client-side reconciler: mantiene gli input pendenti (solo movimento),
/// riceve lo stato autoritativo (ack + pos) e corregge con:
/// - dead-zone (ignora errori piccolissimi)
/// - soft correction (lerp) per errori medi
/// - snap + replay per errori grandi
/// </summary>
public sealed class Reconciler
{
    private struct Pending
    {
        public uint Seq;
        public StateList MoveMask; // SOLO UDLR ripulito
    }

    private readonly List<Pending> _pending = new(capacity: 256);
    private uint _nextSeq = 1;

    private bool _hasAuth;
    private uint _ack;
    private Vector2 _authPos;
    
    private static Reconciler _instance;

    
    public static Reconciler Instance
    {
        get
        {
            if (_instance == null)
                throw new Exception("NetworkManager deve essere inizializzato in Game1 prima dell'uso!");
            return _instance;
        }
    }

    // Il costruttore deve accettare 'Game' e passarlo al padre tramite base(game)
    public Reconciler(Game game) 
    {
        if (_instance != null)
            throw new Exception("Puoi creare solo una istanza di Reconcilier!");
        _instance = this;
     
    }

    /// <summary>Resetta stato e buffer (utile a inizio partita o dopo reconnessione).</summary>
    public void Reset()
    {
        _pending.Clear();
        //_nextSeq = 1;
        _hasAuth = false;
        _ack = 0;
        _authPos = Vector2.Zero;
    }

    //dobbiamo usare localtick anzichè nextseq
    //public uint NextSeq() => _nextSeq++;

    public void Record(uint seq, StateList moveMask)
    {
        _pending.Add(new Pending { Seq = seq, MoveMask = moveMask });
        Debug.WriteLine($"Recorded input seq={seq} moveMask={moveMask} pending={_pending.Count}");
    }

    public void OnServerAuth(uint ack, Vector2 serverPos)
    {
        _ack = ack;
        _authPos = serverPos;
        _hasAuth = true;
    }

    public void GetLatest<T>(ConcurrentQueue<T> queue, Action<T> action)
    {
        T? latest = default;
        bool hasData = false;

        while (queue.TryDequeue(out T? item))
        {
            latest = item;
            hasData = true;
        }
        if (hasData && latest != null) action(latest);
    }

    /// <summary>
    /// Applica reconcile alla posizione locale.
    /// Va chiamato sul main thread (Update), dopo aver eventualmente ricevuto auth.
    /// </summary>
    public void Apply(
    ref Vector2 pos,
    float moveSpeed,
    float moveDt,
    Func<Vector2, bool>? isBlockedAt = null)
    {
        if (!_hasAuth)
            return;

        _hasAuth = false;

        // Scarta gli input vecchi che il server ha già processato e confermato
        _pending.RemoveAll(p => p.Seq <= _ack);
        Debug.WriteLine($"ack={_ack} pending={_pending.Count}");

        // 1) Calcola il "vero presente": parti dal passato sicuro (_authPos)
        // e ri-simula tutti i movimenti che il server non ha ancora visto.
        Vector2 replayPos = _authPos;

        foreach (var p in _pending)
        {
            // Calcola prima una posizione candidata
            Vector2 candidatePos = replayPos;
            PhysicsWrapper.StepFromState(ref candidatePos, p.MoveMask, moveSpeed, moveDt);

            // Se la posizione candidata collide, non committare lo step
            if (isBlockedAt != null && isBlockedAt(candidatePos))
            {
                Debug.WriteLine(
                    $"[RECONCILE BLOCKED] seq={p.Seq} " +
                    $"replay=({replayPos.X}, {replayPos.Y}) " +
                    $"candidate=({candidatePos.X}, {candidatePos.Y})");
                continue;
            }

            // Solo se non collide aggiorni davvero replayPos
            replayPos = candidatePos;
        }

        // 2) Confronta la posizione attuale con il "vero presente"
        float err = PhysicsAPI.Distance(pos, replayPos);

        const float Eps = 2f;
        const float SnapThreshold = 12f;
        const float SoftLerp = 0.25f;

        // Se hai predetto bene, il reconciler non tocca il pinguino
        if (err <= Eps)
            return;

        if (err <= SnapThreshold)
        {
            Debug.WriteLine("SoftLerp");
            Debug.WriteLine($"PosX : {pos.X}, PosY: {pos.Y}");
            Debug.WriteLine($"ReplayPosX : {replayPos.X}, ReplayPosY : {replayPos.Y}");

            pos = PhysicsAPI.Lerp(pos, replayPos, SoftLerp);
            return;
        }

        Debug.WriteLine("HARD RECONCILE");
        Debug.WriteLine($"After if, PosX : {pos.X}, PosY: {pos.Y}");
        Debug.WriteLine($"After if, ReplayPosX : {replayPos.X}, ReplayPosY : {replayPos.Y}");

        // Errore grave (es. il server ti ha visto sbattere contro un muro)
        pos = replayPos;
    }


}