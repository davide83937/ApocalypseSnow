using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

// Non simula collisioni: confronta la prediction locale con gli esiti del server.
public sealed partial class Reconciler
{
    // HIT confermati dal server in attesa di applicare le conseguenze sul client.
    private readonly List<(uint Tick, bool LocalVictim, uint Ack, Vector2 Position)> _pendingHits = new();

    public void ReadAuthShot(uint nextLocalTick)
    {
        if (_matchEvents == null)
            return;

        while (_matchEvents.PendingShotResolutions.Count > 0)
        {
            ShotResolutionMessage result = _matchEvents.PendingShotResolutions.Dequeue();

            ShotTrace.Log($"[SHOT_TRACE] role=CLIENT event=SERVER_RESULT key={result.Key} outcome={result.Outcome} localTick={nextLocalTick} serverTick={result.ServerTick} hitAck={result.HitAck} hitPlayer={result.HitPlayerId} utc={DateTime.UtcNow:O}");

            bool hitLocal = result.Outcome == ShotOutcome.Penguin && result.HitPlayerId == _localPlayerId;
            bool hitOpponent = result.Outcome == ShotOutcome.Penguin && !hitLocal;
            bool opponentWasFrozen = _opponentPenguin.LocalFreeze.IsFrozen(nextLocalTick);

            // Il server invia RemoteShot prima di ShotResolved sulla stessa TCP.
            uint startTick = result.Key.ShotTick;
            if (result.Key.PlayerId != _localPlayerId &&
                !_matchEvents.TryTakeRemoteShotStart(result.Key, out startTick))
                throw new InvalidOperationException("RemoteShot mancante prima di ShotResolved.");

            // La prediction non e' mai autoritativa: la cancelliamo in ogni caso.
            _localPenguin.LocalFreeze.RejectPrediction(result.Key);
            _opponentPenguin.LocalFreeze.RejectPrediction(result.Key);

            if (!hitLocal && !hitOpponent)
            {
                // Falso HIT sul remoto: riprende l'ultima posizione ricevuta.
                if (opponentWasFrozen && !_opponentPenguin.LocalFreeze.IsFrozen(nextLocalTick)
                    && _matchEvents.HasRemotePosition)
                    _opponentPenguin._position = _matchEvents.LatestRemotePosition;
                continue;
            }

            // L'istante autoritativo dipende dal tick di creazione di QUESTA palla.
            uint impactTick = FreezeTimeline.ComputeImpactTick(startTick, result.FlightTicks);

            Penguin victim = hitLocal ? _localPenguin : _opponentPenguin;
            victim.LocalFreeze.ConfirmLocal(result.Key, impactTick);

            // La lettura del verdetto registra l'HIT: solo ReconcileShot applica
            // le conseguenze, anche quando l'impatto e' gia' avvenuto.
            _pendingHits.Add((impactTick, hitLocal, result.HitAck, result.HitPosition));
        }
    }

    // Chiamato dopo il ciclo dei tick: esegue solo le conferme diventate dovute.
    public void ReconcileShot(uint nextLocalTick)
    {
        if (_matchEvents == null)
            return;

        for (int i = _pendingHits.Count - 1; i >= 0; i--)
        {
            var hit = _pendingHits[i];
            if (hit.Tick >= nextLocalTick)
                continue;

            _pendingHits.RemoveAt(i);
            ApplyConfirmedHit(hit.LocalVictim, hit.Tick, hit.Ack, hit.Position, nextLocalTick);
        }
    }

    private void ApplyConfirmedHit(bool localVictim, uint impactTick, uint ack,
        Vector2 impactPosition, uint nextLocalTick)
    {
        // Un HIT ormai scaduto non deve modificare lo stato presente,
        // neppure facendo cadere un uovo raccolto successivamente.
        if (nextLocalTick >= checked(impactTick + 90))
            return;

        Penguin victim = localVictim ? _localPenguin : _opponentPenguin;

        // L'uovo deve cadere nella posizione autoritativa dell'impatto,
        // non nella posizione predetta (diversa sui due client).
        victim._position = impactPosition;
        victim.HandleConfirmedHit();

        if (!localVictim)
            return;

        // Per il locale il replay del movimento gestisce la posizione finale.
        // Uno snapshot con ACK piu' recente prevale sulla posizione dell'HIT.
        if (_hasLatestPositionSnapshot && _latestPositionSnapshot.Ack >= ack)
        {
            ack = _latestPositionSnapshot.Ack;
            impactPosition = _latestPositionSnapshot.Position;
        }
        ReceiveAuthoritativePosition(ack, impactPosition);
    }
}
