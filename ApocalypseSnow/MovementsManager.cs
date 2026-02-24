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
    
    public void UpdateInput(ref StateStruct inputList, bool isFreezing,  bool isWithEgg)
    {
        inputList.Update();
        _newState = Keyboard.GetState();
        _mouseState = Mouse.GetState();
        
        if (_newState.IsKeyDown(Keys.W)) inputList.Current |= StateList.Up;
        if (_newState.IsKeyDown(Keys.S)) inputList.Current |= StateList.Down;
        if (_newState.IsKeyDown(Keys.A)) inputList.Current |= StateList.Left;
        if (_newState.IsKeyDown(Keys.D)) inputList.Current |= StateList.Right;
        if (_newState.IsKeyDown(Keys.R) && !isFreezing) inputList.Current |= StateList.Reload;
        if (_newState.IsKeyDown(Keys.E)&& !isFreezing) inputList.Current |= StateList.TakingEgg;
        if(isWithEgg) inputList.Current |= StateList.WithEgg;
        if (_mouseState.LeftButton == ButtonState.Pressed&& !isFreezing) inputList.Current |= StateList.Shoot;
        // Calcolo automatico di IsMoving
        movementKeyPressed =
            _newState.IsKeyDown(Keys.W) || _newState.IsKeyDown(Keys.S) ||
            _newState.IsKeyDown(Keys.A) || _newState.IsKeyDown(Keys.D);
        if (movementKeyPressed && !isFreezing) inputList.Current |= StateList.Moving;
        if (isFreezing) inputList.Current |= StateList.Freezing;
    }
}