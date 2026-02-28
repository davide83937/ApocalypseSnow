using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class MovementsManager:IMovements
{
    private KeyboardState _newState = Keyboard.GetState();
    private MouseState _mouseState = Mouse.GetState();
    private bool movementKeyPressed = false;
    private Game _game;
    ShotStruct _shotStruct;

    
    public MovementsManager(Game game)
    {
        //this.networkManager = networkManager;
        _game = game;
    }

    public Vector2 GetMousePosition()
    {
        Vector2 mousePosition = new Vector2(_mouseState.X, _mouseState.Y);
        return mousePosition;
    }
    

    public void UpdateInput(ref StateStruct stateStruct, bool isFreezing, bool isWithEgg, 
        float _deltaTime, Vector2 _position)
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

        if (_game.IsActive)
        {
            if (_mouseState.LeftButton == ButtonState.Pressed && !isFreezing && !isWithEgg)
                stateStruct.Current |= StateList.Shoot;
            if (stateStruct.JustReleased(StateList.Shoot))
            {
                Vector2 mousePosition = GetMousePosition();
                _shotStruct.mouseX= (int)mousePosition.X;
                _shotStruct.mouseY= (int)mousePosition.Y;
                //NetworkManager.Instance.SendShot(_shotStruct);
            }
        }

        movementKeyPressed =
            _newState.IsKeyDown(Keys.W) || _newState.IsKeyDown(Keys.S) ||
            _newState.IsKeyDown(Keys.A) || _newState.IsKeyDown(Keys.D);
        if (movementKeyPressed && !isFreezing) stateStruct.Current |= StateList.Moving;
        if (isFreezing) stateStruct.Current |= StateList.Freezing;
        //Vector2 vector2 = Vector2.Zero;
        //_networkManager.SendState(stateStruct, _deltaTime);
        //_networkManager.Receive();
        //Console.WriteLine($"X after Normalization: {_position.X}, Y after Normalization: {_position.Y}");
        NetworkManager.Instance.SendState(stateStruct, _deltaTime, _position);
        // 2. Ricevi gli aggiornamenti dal server
       // return vector2;
    }
    
}