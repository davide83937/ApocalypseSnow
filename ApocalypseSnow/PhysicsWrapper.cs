using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

public static class PhysicsWrapper
{
    // Build string della DLL (comoda e pronta all'uso)
    public static string BuildInfo =>
        Marshal.PtrToStringAnsi(PhysicsAPI.PhysicsBuildInfo()) ?? "(null)";

    public static float UniformMotion(float position, float velocity, float dt)
    {
        PhysicsAPI.uniform_rectilinear_motion(ref position, velocity, dt);
        return position;
    }
}