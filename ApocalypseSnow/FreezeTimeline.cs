using System.Collections.Generic;

namespace ApocalypseSnow;

// Gestisce la prediction e la scadenza autoritativa del congelamento locale.
public sealed class FreezeTimeline
{
    private readonly Dictionary<ShotKey, (uint Start, uint EndExclusive)> _predictedIntervals = new();
    private readonly List<(uint Start, uint EndExclusive)> _confirmedIntervals = new();
    private readonly HashSet<ShotKey> _resolvedShots = new();

    // La durata del volo e' relativa: ogni client usa il proprio tick di comparsa.
    public static uint ComputeImpactTick(uint observedStartTick, uint serverFlightTicks)
        => checked(observedStartTick + serverFlightTicks);

    // Anticipa il freeze usando il tick locale della collisione prevista.
    public bool Predict(ShotKey key, uint localHitTick)
    {
        // Se il server ha gia' risolto questa palla, una prediction tardiva non prevale.
        if (_resolvedShots.Contains(key))
            return false;
        return _predictedIntervals.TryAdd(key, (localHitTick, checked(localHitTick + 90)));
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

    // Il server ha risolto il tiro: annulla la prediction e impedisce di ricrearla.
    // Gli ACK del server non sono tick della timeline locale.
    public void RejectPrediction(ShotKey key)
    {
        _resolvedShots.Add(key);
        _predictedIntervals.Remove(key);
    }

    public void ConfirmLocal(ShotKey key, uint localHitTick, uint durationTicks = 90)
    {
        _resolvedShots.Add(key);
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
