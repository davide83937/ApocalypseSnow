using System;

namespace ApocalypseSnow;

// Converte secondi in tick di simulazione senza amplificare il rumore float
// in un tick aggiuntivo quando la durata e' praticamente un multiplo esatto.
public static class ShotFlightTiming
{
    private const double TickTolerance = 0.0001;

    public static uint LandingTicks(float durationSeconds, uint tickRateHz)
    {
        if (!float.IsFinite(durationSeconds) || durationSeconds < 0f || tickRateHz == 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));

        return checked((uint)Math.Ceiling((double)durationSeconds * tickRateHz - TickTolerance));
    }

    public static bool IsFutureImpact(uint impactTick, uint lastSentInputTick)
        => impactTick > lastSentInputTick;

    public static bool HasLanded(uint observedStartTick, uint localTick, float durationSeconds, uint tickRateHz)
        => localTick >= checked(observedStartTick + LandingTicks(durationSeconds, tickRateHz));
}
