using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class MovementsManager:IMovements
{
    private KeyboardState _newState = Keyboard.GetState();
    private MouseState _mouseState = Mouse.GetState();
    private bool movementKeyPressed = false;
    //private StateStruct _stateStruct;
    //private const int speed = 100;
    //private bool isFreezing = false;
    //private bool isWithEgg = false;
    //private float timeTakingEgg = 0;
    //private float timePuttingEgg = 0;
    //private float timeFreezing = 0;

    
    //[DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    //private static extern void uniform_rectilinear_motion(ref float position, float velocity, float deltaTime);

    public Vector2 GetMousePosition()
    {
        Vector2 mousePosition = new Vector2(_mouseState.X, _mouseState.Y);
        return mousePosition;
    }

    /*
    private void MoveOn(float deltaTime, ref float positionY)
    {
        if (_stateStruct.IsPressed(StateList.Up) && !_stateStruct.IsPressed(StateList.Reload) && !_stateStruct.IsPressed(StateList.Freezing))
        {
            //_speed.Y = 100;
            uniform_rectilinear_motion(ref positionY, -speed, deltaTime);
            //_animationManager.MoveRect(3 * _animationManager.SourceRect.Height);
        }
    }
    
  

    private void MoveBack(float deltaTime, ref float positionY)
    {
        if (_stateStruct.IsPressed(StateList.Down) && !_stateStruct.IsPressed(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            //_speed.Y = 100;
            uniform_rectilinear_motion(ref positionY, speed, deltaTime);
            //_animationManager.MoveRect(0 * _animationManager.SourceRect.Height);
        }
    }
    

    private void MoveRight(float deltaTime, ref float positionX)
    {
        if (_stateStruct.IsPressed(StateList.Right) && !_stateStruct.IsPressed(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            //_speed.X = 100;
            uniform_rectilinear_motion(ref positionX, speed, deltaTime);
            //_animationManager.MoveRect(2 * _animationManager.SourceRect.Height);
        }
    }
    
    
    private void MoveLeft(float deltaTime, ref float positionX)
    {
        if (_stateStruct.IsPressed(StateList.Left) && !_stateStruct.IsPressed(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            //_speed.X = 100;
            uniform_rectilinear_motion(ref positionX, -speed, deltaTime);
            //_animationManager.MoveRect(1 * _animationManager.SourceRect.Height);
        }
    }
    
    private void MoveReload(ref float reloadTime)
    {
        if (_stateStruct.JustReleased(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            reloadTime = 0f;
        }
    }*/
/*
    public void UpdatePositionX(float deltaTime, ref float positionX)
    {
        MoveLeft(deltaTime, ref positionX);
        MoveRight(deltaTime, ref positionX);
    }

    public void UpdatePositionY(float deltaTime, ref float positionY)
    {
        MoveBack(deltaTime, ref positionY);
        MoveOn(deltaTime, ref positionY);
    }*/
    

    public void UpdateInput(ref StateStruct stateStruct, bool isFreezing, bool isWithEgg)
    {
        
        stateStruct.Update();
        
        _newState = Keyboard.GetState();
        _mouseState = Mouse.GetState();
  
        if (_newState.IsKeyDown(Keys.W)) stateStruct.Current |= StateList.Up;
        if (_newState.IsKeyDown(Keys.S)) stateStruct.Current |= StateList.Down;
        if (_newState.IsKeyDown(Keys.A)) stateStruct.Current |= StateList.Left;
        if (_newState.IsKeyDown(Keys.D)) stateStruct.Current |= StateList.Right;
        if (_newState.IsKeyDown(Keys.R) && !isFreezing && !isWithEgg) stateStruct.Current |= StateList.Reload;
        if (_newState.IsKeyDown(Keys.E) && !isFreezing) stateStruct.Current |= StateList.TakingEgg;
        if(isWithEgg) stateStruct.Current |= StateList.WithEgg;
        if (_newState.IsKeyDown(Keys.Space) && !isFreezing && isWithEgg) stateStruct.Current |= StateList.PuttingEgg;
       
        if (_mouseState.LeftButton == ButtonState.Pressed && !isFreezing && !isWithEgg) stateStruct.Current |= StateList.Shoot;
      
        movementKeyPressed =
            _newState.IsKeyDown(Keys.W) || _newState.IsKeyDown(Keys.S) ||
            _newState.IsKeyDown(Keys.A) || _newState.IsKeyDown(Keys.D);
        if (movementKeyPressed && !isFreezing) stateStruct.Current |= StateList.Moving;
        if (isFreezing) stateStruct.Current |= StateList.Freezing;
    }
    
}