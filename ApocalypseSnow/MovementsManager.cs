using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

/// Gestisce tutto l'input locale del player:
/// - tastiera (movimento + azioni)
/// - mouse (FSM del tiro con lock del primo click)
///
/// Logica del tiro:
/// 1. Se nessun tiro è attivo, il primo bottone mouse premuto prende il lock.
/// 2. Finché quel bottone resta premuto, continuiamo a emettere solo quel bit di shoot.
/// 3. Quando il bottone che possiede il lock viene rilasciato, il lock viene chiuso.
/// 4. Dopo il rilascio entriamo in "waiting neutral":
///    nessun nuovo tiro può partire finché ENTRAMBI i bottoni mouse non tornano rilasciati.
public class MovementsManager : IMovements
{
    private enum ChargeMode
    {
        None,
        Left,
        Right
    }

    private KeyboardState _newKeyboardState;
    private MouseState _mouseState;
    private MouseState _previousMouseState;

    private readonly Game _game;

    private ChargeMode _chargeMode = ChargeMode.None;
    private bool _waitingNeutral = false;

    public MovementsManager(Game game)
    {
        _game = game;

        _newKeyboardState = Keyboard.GetState();
        _mouseState = Mouse.GetState();
        _previousMouseState = _mouseState;
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

        _newKeyboardState = Keyboard.GetState();
        _mouseState = Mouse.GetState();

        if (isWithEgg)
            stateStruct.Current |= StateList.WithEgg;

        if (isFreezing)
            stateStruct.Current |= StateList.Freezing;

        if (_game.IsActive)
        {
            ProcessKeyboard(ref stateStruct, isFreezing, isWithEgg);
            ProcessMouseFSM(ref stateStruct, isFreezing, isWithEgg);
        }
        else
        {
            ResetMouseFSM();
        }

        _previousMouseState = _mouseState;
    }

    private void ProcessKeyboard(ref StateStruct stateStruct, bool isFreezing, bool isWithEgg)
    {
        if (_newKeyboardState.IsKeyDown(Keys.W))
            stateStruct.Current |= StateList.Up;

        if (_newKeyboardState.IsKeyDown(Keys.S))
            stateStruct.Current |= StateList.Down;

        if (_newKeyboardState.IsKeyDown(Keys.A))
            stateStruct.Current |= StateList.Left;

        if (_newKeyboardState.IsKeyDown(Keys.D))
            stateStruct.Current |= StateList.Right;

        if (_newKeyboardState.IsKeyDown(Keys.R) && !isFreezing && !isWithEgg)
            stateStruct.Current |= StateList.Reload;

        if (_newKeyboardState.IsKeyDown(Keys.E) && !isFreezing)
            stateStruct.Current |= StateList.TakingEgg;

        if (_newKeyboardState.IsKeyDown(Keys.Space) && !isFreezing && isWithEgg)
            stateStruct.Current |= StateList.PuttingEgg;

        bool movementKeyPressed =
            _newKeyboardState.IsKeyDown(Keys.W) ||
            _newKeyboardState.IsKeyDown(Keys.S) ||
            _newKeyboardState.IsKeyDown(Keys.A) ||
            _newKeyboardState.IsKeyDown(Keys.D);

        if (movementKeyPressed && !isFreezing)
            stateStruct.Current |= StateList.Moving;
    }

    private void ProcessMouseFSM(ref StateStruct stateStruct, bool isFreezing, bool isWithEgg)
    {
        bool canShoot = !isFreezing && !isWithEgg && !stateStruct.IsPressed(StateList.Reload);

        if (!canShoot)
        {
            ResetMouseFSM();
            return;
        }

        bool leftPressed = _mouseState.LeftButton == ButtonState.Pressed;
        bool rightPressed = _mouseState.RightButton == ButtonState.Pressed;

        bool leftJustPressed =
            leftPressed &&
            _previousMouseState.LeftButton == ButtonState.Released;

        bool rightJustPressed =
            rightPressed &&
            _previousMouseState.RightButton == ButtonState.Released;

        if (_waitingNeutral)
        {
            if (!leftPressed && !rightPressed)
                _waitingNeutral = false;

            return;
        }

        if (_chargeMode == ChargeMode.None)
        {
            if (leftJustPressed)
            {
                _chargeMode = ChargeMode.Left;
            }
            else if (rightJustPressed)
            {
                _chargeMode = ChargeMode.Right;
            }
        }

        switch (_chargeMode)
        {
            case ChargeMode.Left:
                if (leftPressed)
                    stateStruct.Current |= StateList.ShootLeft;
                else
                    ReleaseLock();
                break;

            case ChargeMode.Right:
                if (rightPressed)
                    stateStruct.Current |= StateList.ShootRight;
                else
                    ReleaseLock();
                break;
        }
    }

    private void ReleaseLock()
    {
        _chargeMode = ChargeMode.None;
        _waitingNeutral = true;
    }

    private void ResetMouseFSM()
    {
        _chargeMode = ChargeMode.None;
        _waitingNeutral = false;
    }
}