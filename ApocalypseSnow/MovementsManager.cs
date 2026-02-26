using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class MovementsManager:IMovements
{
    private KeyboardState _newState = Keyboard.GetState();
    private MouseState _mouseState = Mouse.GetState();
    private bool movementKeyPressed = false;


    public Vector2 GetMousePosition()
    {
        Vector2 mousePosition = new Vector2(_mouseState.X, _mouseState.Y);
        return mousePosition;
    }
    

    public void UpdateInput(ref StateStruct stateStruct, bool isFreezing, bool isWithEgg, 
        float _deltaTime)
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
        //Vector2 vector2 = Vector2.Zero;
        //_networkManager.SendState(stateStruct, _deltaTime);
        //_networkManager.Receive();
        // 2. Ricevi gli aggiornamenti dal server
       // return vector2;
    }
    
}