using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class MovementsManager:IMovements
{
    KeyboardState newState = Keyboard.GetState();
    MouseState mouseState = Mouse.GetState();
 
    
    public void moveOn(ref bool isW){
        if (newState.IsKeyDown(Keys.W)) { isW = true; }else { isW = false; }
    }

    public void moveBack()
    {
      
    }
    
    public void moveRight()
    {
      
        
    }
    
    public void moveLeft()
    {
       
    }

    
    
}