using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class MovementsManager:IMovements
{
    KeyboardState newState = Keyboard.GetState();
    MouseState mouseState = Mouse.GetState();


    public void checkPressMouse(ref bool isLeft)
    {
        mouseState = Mouse.GetState();
        if(mouseState.LeftButton == ButtonState.Pressed){ isLeft = true;}else { isLeft = false; }
    }

    public Vector2 getMousePosition()
    {
        Vector2 mousePosition = new Vector2(mouseState.X, mouseState.Y);
        return mousePosition;
    }
    
    public void moveOn(ref bool isW){
        newState = Keyboard.GetState();
        if (newState.IsKeyDown(Keys.W)) { isW = true;}else { isW = false; }
    }

    public void moveBack(ref bool isS)
    {
        newState = Keyboard.GetState();
        if (newState.IsKeyDown(Keys.S)) { isS = true; }else { isS = false; }
    }
    
    public void moveRight(ref bool isD)
    {
        newState = Keyboard.GetState();
        if (newState.IsKeyDown(Keys.D)) { isD = true; }else { isD = false; }
    }
    
    public void moveLeft(ref bool isA)
    {
        newState = Keyboard.GetState();
        if (newState.IsKeyDown(Keys.A)) { isA = true; }else { isA = false; }
    }

    public void moveReload(ref bool isR)
    {
        newState = Keyboard.GetState();
        if (newState.IsKeyDown(Keys.R)) { isR = true; }else { isR = false; }
    }
}