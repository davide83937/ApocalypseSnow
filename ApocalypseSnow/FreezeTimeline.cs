using System.Collections.Generic;

namespace ApocalypseSnow;

// Gestisce la prediction e la scadenza autoritativa del congelamento locale.
public sealed class FreezeTimeline
{
    private readonly Dictionary<ShotKey, (uint Start, uint EndExclusive)> _predictedIntervals = new();
    private readonly List<(uint Start, uint EndExclusive)> _confirmedIntervals = new();

    // La durata del volo e' relativa: ogni client usa il proprio tick di comparsa.
    public static uint ComputeImpactTick(uint observedStartTick, uint serverFlightTicks)
        => checked(observedStartTick + serverFlightTicks);

    // Se manca il tick di partenza, un HIT confermato dal server va comunque applicato.
    // Usiamo il tick locale corrente invece di perdere il congelamento.
    public static uint ComputeConfirmedImpactTick(bool hasObservedStartTick, uint observedStartTick,
        uint serverFlightTicks, uint currentLocalTick)
        => hasObservedStartTick
            ? ComputeImpactTick(observedStartTick, serverFlightTicks)
            : currentLocalTick;

    // Anticipa il freeze usando il tick locale della collisione prevista.
    public void Predict(ShotKey key, uint localHitTick)
    {
        _predictedIntervals.TryAdd(key, (localHitTick, checked(localHitTick + 90)));
    }

    public bool TryGetPredictedHitTick(ShotKey key, out uint localHitTick)
    {
        if (_predictedIntervals.TryGetValue(key, out var interval))
        {
            localHitTick = interval.Start;
            return true;
        }
        localHitTick = 0;
        return false;
    }

    // Una prediction smentita viene soltanto annullata.
    // Gli ACK del server non sono tick della timeline locale.
    public void RejectPrediction(ShotKey key)
    {
        _predictedIntervals.Remove(key);
    }

    public void ConfirmLocal(ShotKey key, uint localHitTick, uint durationTicks = 90)
    {
        _predictedIntervals.Remove(key);
        _confirmedIntervals.Add((localHitTick, checked(localHitTick + durationTicks)));
    }

    // Indica se un input locale appartiene ancora al freeze.
    public bool IsFrozen(uint localTick)
    {
        foreach (var interval in _confirmedIntervals)
            if (localTick >= interval.Start && localTick < interval.EndExclusive)
                return true;

        foreach (var interval in _predictedIntervals.Values)
            if (localTick >= interval.Start && localTick < interval.EndExclusive)
                return true;

        return false;
    }
}
