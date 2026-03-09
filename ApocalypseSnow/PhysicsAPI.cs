using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

internal static class PhysicsAPI
{
    private const string DllName = "libPhysicsDll.dll";

    // ------------------ BUILD INFO ------------------
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr PhysicsBuildInfo();

    // ------------------ MOTION ------------------
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void uniform_rectilinear_motion(ref float position, float velocity, float deltaTime);

    //------------------ NORMALIZZAZIONE DELLA VELOCITA' (per evitare che in diagonale vada più veloce) ------------------
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void normalizeVelocity(ref float velocityX, ref float velocityY);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void parabolic_motion(
        float gravity,
        float start_positionX,
        float start_positionY,
        ref float positionX,
        ref float positionY,
        float start_velocityX,
        float start_velocityY,
        float gameTime
    );
    
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float calculate_ball_scale_only(
        float startX, float startY, float finalX, float finalY, float posX, 
        float startSpeedX, float L, float K, float currentScale);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float Distance(Vector2 a, Vector2 b);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Vector2 Lerp(Vector2 a, Vector2 b, float t);
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float LerpFloat(float a, float b, float t);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Vector2 calculateScreenPosition(Vector2 startPos, float worldDeltaX, float worldDeltaY, float z);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float calculateVisualScale(float z, float maxHeight, float scaleMin, float scaleMax, out float outAlpha);
}