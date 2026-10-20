using System;

namespace ApocalypseSnow;

// Reconciler.State: conferma o annulla il freeze previsto dopo il risultato del server.
// Posizione, ACK, replay ed effetti differiti sono gestiti in Reconciler.Position.cs.
public sealed partial class Reconciler
{
    private void ReconcileFreezeFromShotResults()
    {
        while (_matchEvents.PendingShotResolutions.Count > 0)
        {
            ShotResolutionMessage resolution = _matchEvents.PendingShotResolutions.Dequeue();
            ShotTrace.Log($"[SHOT_TRACE] role=CLIENT event=SERVER_RESULT key={resolution.Key} outcome={resolution.Outcome} localTick={_nextLocalInputTick} serverTick={resolution.ServerTick} hitAck={resolution.HitAck} hitPlayer={resolution.HitPlayerId} utc={DateTime.UtcNow:O}");

            bool hitLocalPenguin = resolution.Outcome == ShotOutcome.Penguin && resolution.HitPlayerId == _localPlayerId;
            bool hitOpponentPenguin = resolution.Outcome == ShotOutcome.Penguin && !hitLocalPenguin;
            bool opponentWasFrozenBeforeResult = _opponentPenguin.LocalFreeze.IsFrozen(_nextLocalInputTick);

            // Ogni client usa il tick di comparsa della propria palla:
            // il proprietario lo ha in ShotKey, il destinatario in Events.
            uint shotStartTickOnThisClient = 0;
            bool hasShotStartTick = resolution.Key.PlayerId == _localPlayerId;
            if (hasShotStartTick)
                shotStartTickOnThisClient = resolution.Key.ShotTick;
            else
                hasShotStartTick = _matchEvents.TryTakeRemoteShotStart(resolution.Key, out shotStartTickOnThisClient);

            // Un MISS annulla sempre la prediction, anche se manca il tick iniziale.
            // ShotResolved non modifica il ciclo grafico locale della palla.
            if (!hitLocalPenguin && !hitOpponentPenguin)
            {
                _localPenguin.LocalFreeze.RejectPrediction(resolution.Key);
                _opponentPenguin.LocalFreeze.RejectPrediction(resolution.Key);
                RestoreOpponentPositionAfterRejectedHit(opponentWasFrozenBeforeResult);
                continue;
            }

            // Un HIT confermato dal server non puo' essere ignorato perche'
            // il client non ha registrato il tick iniziale della palla.
            // In quel caso recuperiamo dal tick corrente, come nel vecchio GameSession.
            uint impactTickOnThisClient = FreezeTimeline.ComputeConfirmedImpactTick(
                hasShotStartTick, shotStartTickOnThisClient, resolution.FlightTicks, _nextLocalInputTick);
            if (!hasShotStartTick)
                ShotTrace.Log($"[SHOT_ALIGN] missing_start key={resolution.Key}; fallback=current_tick hit={impactTickOnThisClient}");

            uint freezeEndTickExclusive = checked(impactTickOnThisClient + 90);
            ShotTrace.Log($"[SHOT_ALIGN] key={resolution.Key} start={shotStartTickOnThisClient} flight={resolution.FlightTicks} hit={impactTickOnThisClient} now={_nextLocalInputTick} remaining={Math.Max(0L, (long)freezeEndTickExclusive - _nextLocalInputTick)} hitAck={resolution.HitAck}");

            FreezeTimeline hitPenguinFreezeTimeline = hitLocalPenguin ? _localPenguin.LocalFreeze : _opponentPenguin.LocalFreeze;
            if (hasShotStartTick && hitPenguinFreezeTimeline.TryGetPredictedHitTick(resolution.Key, out uint predictedImpactTick))
            {
                long predictedFlightTicks = (long)predictedImpactTick - shotStartTickOnThisClient;
                long flightTickDifference = predictedFlightTicks - resolution.FlightTicks;
                ShotTrace.Log($"[SHOT_FLIGHT_CMP] key={resolution.Key} " +
                              $"role={(resolution.Key.PlayerId == _localPlayerId ? "OWNER_A" : "REMOTE_B")} " +
                              $"predictedFlightTicks={predictedFlightTicks} serverFlightTicks={resolution.FlightTicks} deltaTicks={flightTickDifference} " +
                              $"predictedImpactTick={predictedImpactTick} reconstructedHitTick={impactTickOnThisClient}");
            }

            if (hitLocalPenguin)
                _localPenguin.LocalFreeze.ConfirmLocal(resolution.Key, impactTickOnThisClient);
            else
                _localPenguin.LocalFreeze.RejectPrediction(resolution.Key);

            if (hitOpponentPenguin)
                _opponentPenguin.LocalFreeze.ConfirmLocal(resolution.Key, impactTickOnThisClient);
            else
                _opponentPenguin.LocalFreeze.RejectPrediction(resolution.Key);

            if (hitOpponentPenguin)
            {
                ScheduleOrApplyOpponentHit(impactTickOnThisClient, resolution.HitPosition);
                continue;
            }

            ScheduleOrApplyLocalHit(resolution, impactTickOnThisClient, freezeEndTickExclusive);
        }
    }
}
