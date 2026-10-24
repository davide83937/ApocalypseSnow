using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ApocalypseSnow;

/// <summary>
/// Reconciler.Movement: conserva gli input non confermati,
/// riceve posizione e ACK del server, riesegue gli input in attesa
/// e corregge eventuali errori della posizione predetta
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
        _authoritativePosition = Vector2.Zero;
        ClearMatchContext();
    }

    // Contesto di partita condiviso dai due file partial.
    private Events _matchEvents;
    private Penguin _localPenguin;
    private Penguin _opponentPenguin;
    private uint _localPlayerId;
    private bool _hasLatestPositionSnapshot;
    private Events.AuthPositionSnapshot _latestPositionSnapshot;

    public void AttachMatchContext(uint localPlayerId, Penguin localPenguin, Penguin opponentPenguin, Events matchEvents)
    {
        _localPlayerId = localPlayerId;
        _localPenguin = localPenguin;
        _opponentPenguin = opponentPenguin;
        _matchEvents = matchEvents;
    }

    private void ClearMatchContext()
    {
        _pendingHits.Clear();
        _localPenguin = null;
        _opponentPenguin = null;
        _matchEvents = null;
        _localPlayerId = 0;
        _hasLatestPositionSnapshot = false;
        _latestPositionSnapshot = default;
    }

    // Acquisisce l'ultimo snapshot: il replay resta esclusivamente in ReconcilePosition.
    public void ReadAuthPosition()
    {
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
    }

    public void RecordPendingInput(uint inputTick, StateList replayStateMask)
    {
        _unacknowledgedMovementInputs.Add(new PendingMovementInput { SequenceTick = inputTick, ReplayStateMask = replayStateMask });
    }

    public void ReceiveAuthoritativePosition(uint acknowledgedInputTick, Vector2 serverPosition)
    {
        if (_hasAcceptedPositionAck && acknowledgedInputTick < _authoritativeInputAck)
            return;
        _hasAcceptedPositionAck = true;
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
    public void ReconcilePosition(
    ref Vector2 predictedPosition,
    float movementSpeed,
    float fixedDeltaTime,
    Func<Vector2, Vector2>? resolveOverlap = null,
    bool isCurrentlyFrozen = false)
    {
        if (!_hasUnappliedAuthoritativePosition)
            return;

        _hasUnappliedAuthoritativePosition = false;

        // Scarta gli input vecchi che il server ha già processato e confermato
        _unacknowledgedMovementInputs.RemoveAll(pendingInput => pendingInput.SequenceTick <= _authoritativeInputAck);

        // Durante il freeze la posizione dell'HIT resta fissa: aggiorniamo gli ACK,
        // ma non applichiamo movimenti pendenti né correggiamo la posizione.
        if (isCurrentlyFrozen)
            return;

        // 1) Calcola il "vero presente": parti dal passato sicuro (_authoritativePosition)
        // e ri-simula tutti i movimenti che il server non ha ancora visto.
        Vector2 replayedPosition = _authoritativePosition;

        foreach (var pendingInput in _unacknowledgedMovementInputs)
        {
            // Come nella prediction e nel server: solo Reload e Freezing fermano il passo.
            if (StateMasks.BlocksMovement(pendingInput.ReplayStateMask))
                continue;

            PhysicsWrapper.StepFromState(ref replayedPosition,
                pendingInput.ReplayStateMask, movementSpeed, fixedDeltaTime);

            // Stessa risoluzione delle collisioni della prediction e del server.
            if (resolveOverlap != null)
                replayedPosition = resolveOverlap(replayedPosition);
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

            predictedPosition = PhysicsAPI.Lerp(predictedPosition, replayedPosition, SoftCorrectionFactor);
            return;
        }

        // Errore grave (es. il server ti ha visto sbattere contro un muro)
        predictedPosition = replayedPosition;
    }
}
