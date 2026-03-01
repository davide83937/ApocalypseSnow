using ApocalypseSnow;
using Microsoft.Xna.Framework;
using System;
using System.Runtime.InteropServices;

public static class PhysicsWrapper
{
    private static int i = 0;
    // Build string della DLL (comoda e pronta all'uso)
    public static string BuildInfo =>
        Marshal.PtrToStringAnsi(PhysicsAPI.PhysicsBuildInfo()) ?? "(null)";

    public static void UniformMotion(ref float position, float velocity, float dt)
    {
        PhysicsAPI.uniform_rectilinear_motion(ref position, velocity, dt);
    }

    public static void StepFromState(ref Vector2 pos, StateList state, float speed, float dt, ref float dx, ref float dy)
    {
        //float dx = 0;
        //float dy = 0;

        // 1. Rilevazione direzioni (input grezzo: -1, 0, 1)
        if ((state & StateList.Up) != 0) dy -= 1f;
        if ((state & StateList.Down) != 0) dy += 1f;
        if ((state & StateList.Left) != 0) dx -= 1f;
        if ((state & StateList.Right) != 0) dx += 1f;

        // --- IL CONTROLLO FONDAMENTALE ---
        // Verifichiamo se c'è almeno un input direzionale attivo.
        // Se dx e dy sono entrambi 0, non dobbiamo normalizzare né muoverci.
        if (dx == 0 && dy == 0) 
        {
            return; 
        }

        // 3. Normalizzazione
        // Se dx=1 e dy=1 (diagonale), dopo questa chiamata dx e dy diventeranno circa 0.707
        PhysicsAPI.normalizeVelocity(ref dx, ref dy);

        // 4. Calcolo velocità finale e applicazione del moto
        //float speed = 200f;
        float finalVx = dx * speed;
        float finalVy = dy * speed;
        //Console.WriteLine("CHIAMATO "+i);
        UniformMotion(ref pos.X, finalVx, dt);
        UniformMotion(ref pos.Y, finalVy, dt);
        //i++;
    }

    public static Vector2 ParabolicMotion(
        float gravity,
        Vector2 startPosition,
        Vector2 startVelocity,
        float time)
    {
        PhysicsAPI.parabolic_motion(
            gravity,
            startPosition.X,
            startPosition.Y,
            out float x,
            out float y,
            startVelocity.X,
            startVelocity.Y,
            time
        );

        return new Vector2(x, y);
    }
}