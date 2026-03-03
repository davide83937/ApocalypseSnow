using System.Runtime.InteropServices;

namespace ApocalypseSnow;

internal static class PhysicsOtherAPI
{
    private const string DllName = "libPhysicsDll.dll";
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void parabolic_motion(float gravity, float startPositionX, float startPositionY, 
        out float positionX, out float positionY, float startVelocityX,
        float startVelocityY, float gameTime);
}