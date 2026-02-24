using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;
using Microsoft.Xna.Framework;

public class MovementsManagerRed:IMovements
{
    private KeyboardState _newState = Keyboard.GetState();
    private MouseState _mouseState = Mouse.GetState();
    private bool movementKeyPressed = false;
    public Vector2 GetMousePosition()
    {
        Vector2 mousePosition = new Vector2(_mouseState.X, _mouseState.Y);
        return mousePosition;
    }
    
    public void UpdateInput(ref StateStruct inputList, bool isFreezing, bool  isWithEgg)
    {
        inputList.Update();
        _newState = Keyboard.GetState();
        _mouseState = Mouse.GetState();
        if (_newState.IsKeyDown(Keys.Up)) inputList.Current |= StateList.Up;
        if (_newState.IsKeyDown(Keys.Down)) inputList.Current |= StateList.Down;
        if (_newState.IsKeyDown(Keys.Left)) inputList.Current |= StateList.Left;
        if (_newState.IsKeyDown(Keys.Right)) inputList.Current |= StateList.Right;
        if (_newState.IsKeyDown(Keys.F)&& !isFreezing) inputList.Current |= StateList.Reload;
        if (_newState.IsKeyDown(Keys.T)&& !isFreezing) inputList.Current |= StateList.TakingEgg;
        if(isWithEgg) inputList.Current |= StateList.WithEgg;
        if (_mouseState.RightButton == ButtonState.Pressed&& !isFreezing) inputList.Current |= StateList.Shoot;
        // Calcolo automatico di IsMoving
        movementKeyPressed = 
            _newState.IsKeyDown(Keys.Up) || _newState.IsKeyDown(Keys.Down) || 
            _newState.IsKeyDown(Keys.Left) || _newState.IsKeyDown(Keys.Right);
        if (movementKeyPressed && !isFreezing) inputList.Current |= StateList.Moving;
        if (isFreezing) inputList.Current |= StateList.Freezing;
    }
}