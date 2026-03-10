using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class MovementsManager : IMovements
{
    private enum ChargeMode
    {
        None,
        Left,
        Right
    }

    private KeyboardState _newState = Keyboard.GetState();
    private MouseState _mouseState = Mouse.GetState();
    private MouseState _previousMouseState = Mouse.GetState();

    private readonly Game _game;
    private ChargeMode _chargeMode = ChargeMode.None;

    public MovementsManager(Game game)
    {
        _game = game;
    }

    public Vector2 GetMousePosition()
    {
        return new Vector2(_mouseState.X, _mouseState.Y);
    }

    public void UpdateInput(
        ref StateStruct stateStruct,
        bool isFreezing,
        bool isWithEgg,
        float deltaTime,
        Vector2 position)
    {
        stateStruct.Update();

        _newState = Keyboard.GetState();
        _mouseState = Mouse.GetState();

        bool isActive = _game.IsActive;

        // Stati derivati dal gameplay: questi possono restare anche fuori da IsActive
        if (isWithEgg)
            stateStruct.Current |= StateList.WithEgg;

        if (isFreezing)
            stateStruct.Current |= StateList.Freezing;

        if (isActive)
        {
            // Movimento
            if (_newState.IsKeyDown(Keys.W))
                stateStruct.Current |= StateList.Up;

            if (_newState.IsKeyDown(Keys.S))
                stateStruct.Current |= StateList.Down;

            if (_newState.IsKeyDown(Keys.A))
                stateStruct.Current |= StateList.Left;

            if (_newState.IsKeyDown(Keys.D))
                stateStruct.Current |= StateList.Right;

            // Azioni tastiera
            if (_newState.IsKeyDown(Keys.R) && !isFreezing && !isWithEgg)
                stateStruct.Current |= StateList.Reload;

            if (_newState.IsKeyDown(Keys.E) && !isFreezing)
                stateStruct.Current |= StateList.TakingEgg;

            if (_newState.IsKeyDown(Keys.Space) && !isFreezing && isWithEgg)
                stateStruct.Current |= StateList.PuttingEgg;

            // Moving
            bool movementKeyPressed =
                _newState.IsKeyDown(Keys.W) ||
                _newState.IsKeyDown(Keys.S) ||
                _newState.IsKeyDown(Keys.A) ||
                _newState.IsKeyDown(Keys.D);

            if (movementKeyPressed && !isFreezing)
                stateStruct.Current |= StateList.Moving;

            // ===== SHOOT LOCK: first press wins =====
            bool canShoot = !isFreezing && !isWithEgg;

            bool leftPressed = _mouseState.LeftButton == ButtonState.Pressed;
            bool righPressed = _mouseState.RightButton == ButtonState.Pressed;

            bool leftJustPressed =
                _mouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released;

            bool rightJustPressed =
                _mouseState.RightButton == ButtonState.Pressed &&
                _previousMouseState.RightButton == ButtonState.Released;

            if (!canShoot)
            {
                _chargeMode = ChargeMode.None;
            }
            else
            {
                // Acquisizione lock solo sul primo click
                if (_chargeMode == ChargeMode.None)
                {
                    if (leftJustPressed)
                        _chargeMode = ChargeMode.Left;
                    else if (rightJustPressed)
                        _chargeMode = ChargeMode.Right;
                }

                switch (_chargeMode)
                {
                    case ChargeMode.Left:
                        if (leftPressed)
                        {
                            stateStruct.Current |= StateList.ShootLeft;
                        }
                        else
                        {
                            _chargeMode = ChargeMode.None;
                        }
                        break;

                    case ChargeMode.Right:
                        if (righPressed)
                        {
                            stateStruct.Current |= StateList.ShootRight;
                            bool rightPressed = _mouseState.RightButton == ButtonState.Pressed;
                        }
                        else
                        {
                            _chargeMode = ChargeMode.None;
                        }
                        break;
                }
            }
        }
        else
        {
            // Se stiamo cliccando fuori dalla finestra, resettiamo lo stato di shoot
            _chargeMode = ChargeMode.None;
        }

        //aggiorniamo sempre lo stato precedente del mouse, così da poter rilevare i click al frame successivo
        _previousMouseState = _mouseState;
    }
}