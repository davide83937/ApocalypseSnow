using System;

namespace ApocalypseSnow;

// Identifica una palla tramite giocatore e tick del tiro.
public readonly record struct ShotKey(uint PlayerId, uint ShotTick)
{
    // Crea un tag unico per la DLL, senza superare i 15 caratteri.
    public string CollisionTag(string prefix)
    {
        if (prefix != "Ball" && prefix != "RedBall")
            throw new ArgumentException("Unexpected ball collision prefix.", nameof(prefix));

        return $"{prefix}{ShotTick:x8}";
    }
}
