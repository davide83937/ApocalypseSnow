using System.Collections.Generic;

namespace ApocalypseSnow;

// Gestisce la prediction e la scadenza autoritativa del congelamento locale.
public sealed class FreezeTimeline
{
    private readonly Dictionary<ShotKey, uint> _predictedUntil = new();
    private readonly List<(uint Start, uint End)> _confirmedIntervals = new();

    // Anticipa il freeze usando il tick locale della collisione prevista.
    public void Predict(ShotKey key, uint localHitTick)
    {
        _predictedUntil.TryAdd(key, localHitTick + 90);
    }

    // Conferma un HIT oppure annulla la prediction smentita dal server.
    public void Resolve(ShotKey key, bool hit, uint authoritativeEndSeq)
    {
        _predictedUntil.Remove(key);
        if (hit)
            _confirmedIntervals.Add((0, authoritativeEndSeq));
    }

    public void ConfirmLocal(ShotKey key, uint localHitTick, uint durationTicks = 90)
    {
        _predictedUntil.Remove(key);
        _confirmedIntervals.Add((localHitTick, localHitTick + durationTicks));
    }

    // Indica se un input locale appartiene ancora al freeze.
    public bool IsFrozen(uint localTick)
    {
        foreach (var interval in _confirmedIntervals)
            if (localTick >= interval.Start && localTick <= interval.End)
                return true;

        foreach (uint end in _predictedUntil.Values)
            if (localTick <= end)
                return true;

        return false;
    }
}
