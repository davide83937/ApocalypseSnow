using System;
using System.Runtime.InteropServices;

internal static class PhysicsAPI
{
    private const string DllName = "libPhysicsDll.dll";

    // ------------------ BUILD INFO ------------------
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern IntPtr PhysicsBuildInfo();

    // ------------------ MOTION ------------------
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void uniform_rectilinear_motion(ref float position, float velocity, float deltaTime);
}