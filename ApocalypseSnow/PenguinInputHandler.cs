using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class PenguinInputHandler
{
    public StateStruct _stateStruct;
    //private Vector2 _speed = new Vector2(200, 200);
    //private float timeTakingEgg = 0;
    //private float timePuttingEgg = 0;
    private float timeFreezing = 0;
    public AnimationManager _animationManager;
    
    
    public PenguinInputHandler(string tag)
    {
        _stateStruct = new StateStruct();
        _animationManager = new AnimationManager(tag);
    }
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void uniform_rectilinear_motion(ref float position, float velocity, float deltaTime);


    public void getMotion(ref float position, float velocity,float deltaTime)
    {
        uniform_rectilinear_motion(ref position, velocity, deltaTime);
    }
    
    public bool isTakingEggJustReleased()
    {
        return _stateStruct.JustReleased(StateList.TakingEgg);
    }
    
    public bool isPuttingEggJustReleased()
    {
        return _stateStruct.JustReleased(StateList.PuttingEgg);
    }

    public void increaseTimeFreezing(float _deltaTime, ref bool isFreezing)
    {
        if (isFreezing)
        {
            timeFreezing += _deltaTime;
            if (timeFreezing >= 3)
            {isFreezing = false; timeFreezing = 0;}
        }
    }
    
    public void UpdateMovement(float deltaTime, ref Vector2 position)
    {
        if (_stateStruct.IsPressed(StateList.Reload) || _stateStruct.IsPressed(StateList.Freezing)) { return; }
        PhysicsWrapper.StepFromStateRef(ref position, _stateStruct.Current, 200f, deltaTime, out float dx, out float dy);
        UpdateAnimationState(dx, dy);
    }
    

    
    private void UpdateAnimationState(float vx, float vy)
    {
        // Se il pinguino si muove principalmente in verticale
        if (Math.Abs(vy) > Math.Abs(vx))
        {
            if (vy < 0) // Sta andando su
                _animationManager.MoveRect(3 * _animationManager.SourceRect.Height);
            else // Sta andando giù
                _animationManager.MoveRect(0 * _animationManager.SourceRect.Height);
        }
        // Se il pinguino si muove principalmente in orizzontale
        else if (Math.Abs(vx) > 0)
        {
            if (vx > 0) // Sta andando a destra
                _animationManager.MoveRect(2 * _animationManager.SourceRect.Height);
            else // Sta andando a sinistra
                _animationManager.MoveRect(1 * _animationManager.SourceRect.Height);
        }
    }
 
    
    public void MoveReload(ref float reloadTime)
    {
        if (_stateStruct.JustReleased(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            reloadTime = 0f;
        }
    }

    
}