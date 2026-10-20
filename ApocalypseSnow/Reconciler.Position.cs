using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace ApocalypseSnow;

/// <summary>
/// Reconciler.Position: conserva gli input non confermati,
/// riceve posizione e ACK del server e corregge il movimento con:
/// - dead-zone (ignora errori piccolissimi)
/// - soft correction (lerp) per errori medi
/// - snap + replay per errori grandi
/// </summary>
public sealed partial class Reconciler
{
    private struct PendingMovementInput
    {
        public uint SequenceTick;
        public StateList ReplayStateMask; // Direzioni e stati che bloccano il movimento nel replay.
    }

    private readonly List<PendingMovementInput> _unacknowledgedMovementInputs = new(capacity: 256);

    private bool _hasUnappliedAuthoritativePosition;
    private uint _authoritativeInputAck;
    private bool _hasAcceptedPositionAck;
    private uint _latestAcceptedPositionAck;
    private Vector2 _authoritativePosition;

    private static Reconciler _instance;


    public static Reconciler Instance
    {
        get
        {
            if (_instance == null)
                throw new Exception("Reconciler deve essere inizializzato in Game1 prima dell'uso!");
            return _instance;
        }
    }

    // I due file partial condividono questa unica istanza.
    public Reconciler(Game game)
    {
        if (_instance != null)
            throw new Exception("Puoi creare solo una istanza di Reconciler!");
        _instance = this;

    }

    /// <summary>Resetta stato e buffer (utile a inizio partita o dopo reconnessione).</summary>
    public void Reset()
    {
        _unacknowledgedMovementInputs.Clear();
        _hasUnappliedAuthoritativePosition = false;
        _authoritativeInputAck = 0;
        _hasAcceptedPositionAck = false;
        _latestAcceptedPositionAck = 0;
        _authoritativePosition = Vector2.Zero;
        ClearMatchContext();
    }


    // Contesto di partita condiviso dai due file partial.
    private Events _matchEvents;
    private Penguin _localPenguin;
    private Penguin _opponentPenguin;
    private uint _localPlayerId;
    private uint _nextLocalInputTick;
    private bool _hasLatestPositionSnapshot;
    private Events.AuthPositionSnapshot _latestPositionSnapshot;
    private readonly List<(uint HitTick, uint Ack, Vector2 Position, ShotKey Key)> _scheduledLocalHitCorrections = new();
    private readonly List<(uint HitTick, Vector2 Position)> _scheduledOpponentHitEffects = new();

    private uint LastSentLocalInputTick => _nextLocalInputTick == 0 ? 0 : _nextLocalInputTick - 1;

    public void AttachMatchContext(uint localPlayerId, Penguin localPenguin, Penguin opponentPenguin, Events matchEvents)
    {
        _localPlayerId = localPlayerId;
        _localPenguin = localPenguin;
        _opponentPenguin = opponentPenguin;
        _matchEvents = matchEvents;
    }

    private void ClearMatchContext()
    {
        _scheduledLocalHitCorrections.Clear();
        _scheduledOpponentHitEffects.Clear();
        _localPenguin = null;
        _opponentPenguin = null;
        _matchEvents = null;
        _localPlayerId = 0;
        _nextLocalInputTick = 0;
        _hasLatestPositionSnapshot = false;
        _latestPositionSnapshot = default;
    }

    public void ProcessAuthoritativeUpdates(uint localTick)
    {
        _nextLocalInputTick = localTick;
        if (_matchEvents == null)
            return;

        ConsumeLatestSnapshot(_matchEvents._authPositionQueue, auth =>
        {
            if (!_hasLatestPositionSnapshot || auth.Ack >= _latestPositionSnapshot.Ack)
            {
                _latestPositionSnapshot = auth;
                _hasLatestPositionSnapshot = true;
                ReceiveAuthoritativePosition(auth.Ack, auth.Position);
            }
        });
        ReconcileFreezeFromShotResults();
    }

    public void ApplyScheduledHitEffects(uint localTick)
    {
        _nextLocalInputTick = localTick;
        if (_matchEvents == null)
            return;
        ApplyScheduledLocalHitCorrections();
        ApplyScheduledOpponentHitEffects();
    }


    // Ripristina la posizione remota solo quando un HIT predetto viene smentito.
    private void RestoreOpponentPositionAfterRejectedHit(bool remoteWasFrozen)
    {
        if (remoteWasFrozen && !_opponentPenguin.LocalFreeze.IsFrozen(_nextLocalInputTick) &&
            _matchEvents.HasRemotePosition)
            _opponentPenguin._position = _matchEvents.LatestRemotePosition;
    }

    // Il Reconciler.State stabilisce quando c'e' un HIT; qui si gestiscono
    // le conseguenze posizionali e gli effetti di gameplay al tick d'impatto.
    private void ScheduleOrApplyOpponentHit(uint localHitTick, Vector2 position)
    {
        if (localHitTick > LastSentLocalInputTick)
            _scheduledOpponentHitEffects.Add((localHitTick, position));
        else
            ApplyOpponentHitEffect(localHitTick, position);
    }

    private void ScheduleOrApplyLocalHit(ShotResolutionMessage resolution,
        uint localHitTick, uint freezeEndExclusive)
    {
        // Gli input di movimento del bersaglio locale sono bloccati esattamente
        // per [localHitTick, localHitTick + 90), anche durante il replay.
        MarkInputsAsFrozen(localHitTick, freezeEndExclusive - 1);
        uint ack = resolution.HitAck;
        Vector2 position = resolution.HitPosition;
        if (_hasLatestPositionSnapshot && _latestPositionSnapshot.Ack >= ack)
        {
            ack = _latestPositionSnapshot.Ack;
            position = _latestPositionSnapshot.Position;
        }

        // Se l'HIT e' ancora futuro sulla timeline locale, non anticipare lo snap.
        if (localHitTick > LastSentLocalInputTick)
        {
            _scheduledLocalHitCorrections.Add((localHitTick, ack, position, resolution.Key));
            ShotTrace.Log($"[HIT_RECON] deferred key={resolution.Key} hitTick={localHitTick} now={_nextLocalInputTick} ack={ack}");
        }
        else
        {
            _localPenguin._penguinColliderHandler.HandleHitByBall(ref _localPenguin._myEgg);
            if (_nextLocalInputTick < freezeEndExclusive)
                AcceptAuthoritativeHitPosition(resolution.Key, ack, position);
        }
    }

    private void ApplyScheduledLocalHitCorrections()
    {
        for (int i = _scheduledLocalHitCorrections.Count - 1; i >= 0; --i)
        {
            var hit = _scheduledLocalHitCorrections[i];
            if (hit.HitTick > LastSentLocalInputTick)
                continue;
            _scheduledLocalHitCorrections.RemoveAt(i);
            _localPenguin._penguinColliderHandler.HandleHitByBall(ref _localPenguin._myEgg);
            if (_nextLocalInputTick < checked(hit.HitTick + 90))
                AcceptAuthoritativeHitPosition(hit.Key, hit.Ack, hit.Position);
        }
    }

    // Il bersaglio remoto subisce gli effetti solo al proprio impatto ricostruito.
    private void ApplyScheduledOpponentHitEffects()
    {
        for (int i = _scheduledOpponentHitEffects.Count - 1; i >= 0; --i)
        {
            var hit = _scheduledOpponentHitEffects[i];
            if (hit.HitTick > LastSentLocalInputTick)
                continue;
            _scheduledOpponentHitEffects.RemoveAt(i);
            ApplyOpponentHitEffect(hit.HitTick, hit.Position);
        }
    }

    private void ApplyOpponentHitEffect(uint hitTick, Vector2 position)
    {
        // Una notifica ormai scaduta non riporta il remoto alla posizione passata.
        if (_nextLocalInputTick < checked(hitTick + 90))
            _opponentPenguin._position = position;
        _opponentPenguin._penguinColliderHandler.HandleHitByBall(ref _opponentPenguin._myEgg);
    }

    private void AcceptAuthoritativeHitPosition(ShotKey key, uint ack, Vector2 position)
    {
        // Un ACK piu' recente vale piu' di una vecchia posizione d'impatto.
        if (_hasLatestPositionSnapshot && _latestPositionSnapshot.Ack >= ack)
        {
            ack = _latestPositionSnapshot.Ack;
            position = _latestPositionSnapshot.Position;
        }
        Vector2 before = _localPenguin._position;
        ReceiveAuthoritativePosition(ack, position);
        // Il replay degli input pendenti e' eseguito nel normale Update.
        ShotTrace.Log($"[HIT_RECON] key={key} ack={ack} before={before} auth={position} now={_nextLocalInputTick}");
    }

    public void RecordInputForReplay(uint inputTick, StateList replayStateMask)
    {
        _unacknowledgedMovementInputs.Add(new PendingMovementInput { SequenceTick = inputTick, ReplayStateMask = replayStateMask });
        Debug.WriteLine($"Recorded input seq={inputTick} moveMask={replayStateMask} pending={_unacknowledgedMovementInputs.Count}");
    }

    // Segna come congelati anche gli input gia predetti, senza cambiare il replay.
    public void MarkInputsAsFrozen(uint firstFrozenInputTick, uint lastFrozenInputTick)
    {
        for (int i = 0; i < _unacknowledgedMovementInputs.Count; i++)
        {
            PendingMovementInput input = _unacknowledgedMovementInputs[i];
            if (input.SequenceTick < firstFrozenInputTick || input.SequenceTick > lastFrozenInputTick)
                continue;
            input.ReplayStateMask |= StateList.Freezing;
            _unacknowledgedMovementInputs[i] = input;
        }
    }

    public void ReceiveAuthoritativePosition(uint acknowledgedInputTick, Vector2 serverPosition)
    {
        if (_hasAcceptedPositionAck && acknowledgedInputTick < _latestAcceptedPositionAck)
            return;
        _hasAcceptedPositionAck = true;
        _latestAcceptedPositionAck = acknowledgedInputTick;
        _authoritativeInputAck = acknowledgedInputTick;
        _authoritativePosition = serverPosition;
        _hasUnappliedAuthoritativePosition = true;
    }

    public void ConsumeLatestSnapshot<T>(ConcurrentQueue<T> queue, Action<T> action)
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
    /// Ricostruisce la posizione dagli input non ancora confermati dal server.
    /// Va chiamato sul main thread (Update), dopo aver ricevuto lo stato autoritativo.
    /// </summary>
    public void ReconcileLocalPosition(
    ref Vector2 predictedPosition,
    float movementSpeed,
    float fixedDeltaTime,
    Func<Vector2, bool>? isBlockedAtPosition = null)
    {
        if (!_hasUnappliedAuthoritativePosition)
            return;

        _hasUnappliedAuthoritativePosition = false;

        // Scarta gli input vecchi che il server ha già processato e confermato
        _unacknowledgedMovementInputs.RemoveAll(pendingInput => pendingInput.SequenceTick <= _authoritativeInputAck);
        Debug.WriteLine($"ack={_authoritativeInputAck} pending={_unacknowledgedMovementInputs.Count}");

        // 1) Calcola il "vero presente": parti dal passato sicuro (_authoritativePosition)
        // e ri-simula tutti i movimenti che il server non ha ancora visto.
        Vector2 replayedPosition = _authoritativePosition;

        foreach (var pendingInput in _unacknowledgedMovementInputs)
        {
            // Come sul server, durante questi stati non si applica movimento.
            if ((pendingInput.ReplayStateMask & (StateList.Reload | StateList.Freezing |
                               StateList.TakingEgg | StateList.PuttingEgg)) != StateList.None)
                continue;

            // Calcola prima una posizione candidata
            Vector2 candidatePosition = replayedPosition;
            PhysicsWrapper.StepFromState(ref candidatePosition, pendingInput.ReplayStateMask, movementSpeed, fixedDeltaTime);

            // Se la posizione candidata collide, non committare lo step
            if (isBlockedAtPosition != null && isBlockedAtPosition(candidatePosition))
            {
                Debug.WriteLine(
                    $"[RECONCILE BLOCKED] seq={pendingInput.SequenceTick} " +
                    $"replay=({replayedPosition.X}, {replayedPosition.Y}) " +
                    $"candidate=({candidatePosition.X}, {candidatePosition.Y})");
                continue;
            }

            // Solo se non collide aggiorni davvero replayedPosition
            replayedPosition = candidatePosition;
        }

        // 2) Confronta la posizione attuale con il "vero presente"
        float positionError = PhysicsAPI.Distance(predictedPosition, replayedPosition);

        const float PositionTolerance = 2f;
        const float HardCorrectionThreshold = 12f;
        const float SoftCorrectionFactor = 0.25f;

        // Se hai predetto bene, il reconciler non tocca il pinguino
        if (positionError <= PositionTolerance)
            return;

        if (positionError <= HardCorrectionThreshold)
        {
            Debug.WriteLine("SOFT RECONCILE");
            Debug.WriteLine($"PosX : {predictedPosition.X}, PosY: {predictedPosition.Y}");
            Debug.WriteLine($"ReplayPosX : {replayedPosition.X}, ReplayPosY : {replayedPosition.Y}");

            predictedPosition = PhysicsAPI.Lerp(predictedPosition, replayedPosition, SoftCorrectionFactor);
            return;
        }

        Debug.WriteLine("HARD RECONCILE");
        Debug.WriteLine($"After if, PosX : {predictedPosition.X}, PosY: {predictedPosition.Y}");
        Debug.WriteLine($"After if, ReplayPosX : {replayedPosition.X}, ReplayPosY : {replayedPosition.Y}");

        // Errore grave (es. il server ti ha visto sbattere contro un muro)
        predictedPosition = replayedPosition;
    }
}