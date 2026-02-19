using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class MovementsManager:IMovements
{
    private KeyboardState _newState = Keyboard.GetState();
    private MouseState _mouseState = Mouse.GetState();


    public void CheckPressMouse(ref bool isLeft)
    {
        _mouseState = Mouse.GetState();
        if(_mouseState.LeftButton == ButtonState.Pressed){ isLeft = true;}else { isLeft = false; }
    }

    public Vector2 GetMousePosition()
    {
        Vector2 mousePosition = new Vector2(_mouseState.X, _mouseState.Y);
        return mousePosition;
    }
    
    public void moveOn(ref bool isW)
    {
        _newState = Keyboard.GetState();
        isW = _newState.IsKeyDown(Keys.W);
    }

    public void MoveBack(ref bool isS)
    {
        _newState = Keyboard.GetState();
        isS = _newState.IsKeyDown(Keys.S);
    }
    
    public void MoveRight(ref bool isD)
    {
        _newState = Keyboard.GetState();
        isD = _newState.IsKeyDown(Keys.D);
    }
    
    public void MoveLeft(ref bool isA)
    {
        _newState = Keyboard.GetState();
        isA = _newState.IsKeyDown(Keys.A);
    }

    public void MoveReload(ref bool isR)
    {
        _newState = Keyboard.GetState();
        isR = _newState.IsKeyDown(Keys.R);
    }
}