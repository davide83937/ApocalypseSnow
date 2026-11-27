using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

internal static class ShotPhysicsAPI
{
    private const string DllName = "libPhysicsDll.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern PenguinShotHandler.ShotParameters ComputeShotParameters(
        Vector2 origin, Vector2 direction, float speed, float angleDegrees, float gravity);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern float ShotFlightDuration(float startVerticalSpeed, float gravity);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern float ShotMaximumHeight(float startVerticalSpeed, float gravity);
}
