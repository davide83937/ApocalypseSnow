using System;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

// Regole/parametri del tiro di ApocalypseSnow, NON fisica generica.
// Questi valori vanno mantenuti coerenti con Server/shot_gameplay.go;
// i fixture di traiettoria C# e Go servono anche a verificarne la parita'.
internal static class ShotGameplay
{
    private const float HandOffsetX = 48f;
    private const float HandOffsetY = 18f;
    private const float BallSpawnDistance = 22f;
    private const float PlanePerspectiveY = 0.70f;
    internal const float MaxChargeSeconds = 1.5f;
    private const float MaxInitialSpeed = 1900f;
    private const float ShotGravity = 3800f;
    private const float MinDirectionLengthSquared = 0.0001f;

    public static PenguinShotHandler.ShotParameters BuildShotParameters(
        Vector2 penguinPosition, Vector2 target, float chargeSeconds, ShotType shotType)
    {
        Vector2 handPosition = new(penguinPosition.X + HandOffsetX, penguinPosition.Y + HandOffsetY);
        Vector2 screenDelta = target - handPosition;

        float lenSq = screenDelta.X * screenDelta.X + screenDelta.Y * screenDelta.Y;
        Vector2 screenDirection = lenSq <= MinDirectionLengthSquared
            ? new Vector2(1f, 0f)
            : screenDelta * (1f / MathF.Sqrt(lenSq));

        // Solo la logica di gioco decide da dove nasce il proiettile.
        Vector2 origin = handPosition + screenDirection * BallSpawnDistance;
        Vector2 worldDirection = new(screenDelta.X, screenDelta.Y / PlanePerspectiveY);
        float power = MathF.Sqrt(Math.Clamp(chargeSeconds / MaxChargeSeconds, 0f, 1f));
        float speed = power * MaxInitialSpeed;
        float angleDegrees = shotType == ShotType.Right ? 60f : 30f;

        // C++ riceve solo parametri fisici, senza conoscere il pinguino.
        return ShotPhysicsAPI.ComputeShotParameters(origin, worldDirection, speed, angleDegrees, ShotGravity);
    }
}
