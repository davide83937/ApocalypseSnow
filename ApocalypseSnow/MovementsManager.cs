using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class MovementsManager:IMovements
{
    private KeyboardState _newState = Keyboard.GetState();
    private MouseState _mouseState = Mouse.GetState();
    
    public Vector2 GetMousePosition()
    {
        Vector2 mousePosition = new Vector2(_mouseState.X, _mouseState.Y);
        return mousePosition;
    }
    
    public void UpdateInput(ref StateStruct inputList, bool isFreezing)
    {
        inputList.Update();
        _newState = Keyboard.GetState();
        _mouseState = Mouse.GetState();
        if (_newState.IsKeyDown(Keys.W)) inputList.Current |= StateList.Up;
        if (_newState.IsKeyDown(Keys.S)) inputList.Current |= StateList.Down;
        if (_newState.IsKeyDown(Keys.A)) inputList.Current |= StateList.Left;
        if (_newState.IsKeyDown(Keys.D)) inputList.Current |= StateList.Right;
        if (_newState.IsKeyDown(Keys.R)) inputList.Current |= StateList.Reload;
        if (_mouseState.LeftButton == ButtonState.Pressed) inputList.Current |= StateList.Shoot;
        // Calcolo automatico di IsMoving
        if (_newState.GetPressedKeys().Length > 0) inputList.Current |= StateList.Moving;
        if (isFreezing) inputList.Current |= StateList.Freezing;
    }
}