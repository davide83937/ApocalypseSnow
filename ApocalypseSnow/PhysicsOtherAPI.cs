using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

internal static class ShotPhysicsAPI
{
    private const string DllName = "libPhysicsDll.dll"; // Puntiamo alla DLL unificata

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float PredictFullFlightSeconds(int chargeMs, float maxChargeSeconds, float desiredFlightTimeLow, float thetaDeg, float thetaLowDeg);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float ComputeGravityForLowShot(float maxWorldRange, float desiredFlightTimeLow, float thetaLowRad);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float ComputeMaxV0(float gravity, float desiredFlightTimeLow, float thetaLowRad);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float ComputeVerticalSpeed(float chargeSeconds, float maxChargeSeconds, float maxV0, float thetaDeg, float thetaLowDeg);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float ComputeMaxWorldRange(Vector2 muzzleBase, float gameWorldWidth, float gameWorldHeight);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern PenguinShotHandler.ShotLaunch ComputeLaunch(
        Vector2 penguinPosition,
        Vector2 target,
        float chargeSeconds,
        float maxChargeSeconds,
        float desiredFlightTimeLow,
        float thetaDeg,
        float thetaLowDeg,
        float muzzleOffsetY,
        float muzzleDistance,
        float gameWorldWidth,
        float gameWorldHeight
    );
}