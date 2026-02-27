using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class PenguinInputHandler
{
    public StateStruct _stateStruct;
    private Vector2 _speed = new Vector2(200, 200);
    //private float timeTakingEgg = 0;
    //private float timePuttingEgg = 0;
    private float timeFreezing = 0;
    public AnimationManager _animationManager;
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void normalizeVelocity(ref float velocityX, ref float velocityY);
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

    private void MoveOn(float deltaTime, ref float positionY)
    {
        if (_stateStruct.IsPressed(StateList.Up) && !_stateStruct.IsPressed(StateList.Reload) && !_stateStruct.IsPressed(StateList.Freezing))
        {
            _speed.Y = 200;
 
            //Console.WriteLine("UPDATE INPUT");
            uniform_rectilinear_motion(ref positionY, -_speed.Y, deltaTime);
            _animationManager.MoveRect(3 * _animationManager.SourceRect.Height);
        }
    }
    
  

    private void MoveBack(float deltaTime, ref float positionY)
    {
        if (_stateStruct.IsPressed(StateList.Down) && !_stateStruct.IsPressed(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            _speed.Y = 200;
   
            //Console.WriteLine("UPDATE INPUT");
            uniform_rectilinear_motion(ref positionY, _speed.Y, deltaTime);
            _animationManager.MoveRect(0 * _animationManager.SourceRect.Height);
        }
    }
    

    private void MoveRight(float deltaTime, ref float positionX)
    {
        if (_stateStruct.IsPressed(StateList.Right) && !_stateStruct.IsPressed(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            _speed.X = 200;
          
            //Console.WriteLine("UPDATE INPUT");
            uniform_rectilinear_motion(ref positionX, _speed.X, deltaTime);
            _animationManager.MoveRect(2 * _animationManager.SourceRect.Height);
        }
    }
    
    
    private void MoveLeft(float deltaTime, ref float positionX)
    {
        if (_stateStruct.IsPressed(StateList.Left) && !_stateStruct.IsPressed(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            _speed.X = 200;

            //Console.WriteLine("UPDATE INPUT");
            uniform_rectilinear_motion(ref positionX, -_speed.X, deltaTime);
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

    public void UpdatePositionX(float deltaTime, ref float positionX)
    {
        normalizeVelocity(ref _speed.X, ref _speed.Y);
        MoveLeft(deltaTime, ref positionX);
        MoveRight(deltaTime, ref positionX);
    }

    public void UpdatePositionY(float deltaTime, ref float positionY)
    {
        normalizeVelocity(ref _speed.X, ref _speed.Y);
        MoveBack(deltaTime, ref positionY);
        MoveOn(deltaTime, ref positionY);
    }
}