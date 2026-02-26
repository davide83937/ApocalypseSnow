using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;
using Microsoft.Xna.Framework;

public class MovementsManagerRed:IMovements
{
    private KeyboardState _newState = Keyboard.GetState();
    private MouseState _mouseState = Mouse.GetState();
    private bool movementKeyPressed = false;
    //private StateStruct _stateStruct;
    private bool isFreezing = false;
    private bool isWithEgg = false;
    private StateList _remoteState = StateList.None;
    //private float timeTakingEgg = 0;
   // private float timePuttingEgg = 0;
    //private float timeFreezing = 0;
    
    public MovementsManagerRed()
    {
        if (NetworkManager.Instance != null)
        {
            // Ci iscriviamo all'evento per ricevere i dati remoti
            NetworkManager.Instance.OnRemoteReceived += HandleRemoteState;
        }
    }
    private void HandleRemoteState(float x, float y, int mask)
    {
        _remoteState = (StateList)mask;
    }
    
    public Vector2 GetMousePosition()
    {
        Vector2 mousePosition = new Vector2(_mouseState.X, _mouseState.Y);
        return mousePosition;
    }

    public void UpdateInput(ref StateStruct inputList, bool isFreezing, bool isWithEgg, float deltaTime, Vector2 position)
    {
        // 1. Spostiamo lo stato attuale in quello vecchio
        inputList.Update();

        // 2. Applichiamo la maschera di bit che il server ci ha inviato dietro le quinte
        inputList.Current = _remoteState;

        // 3. Forziamo gli stati di gioco locali
        if (isWithEgg) inputList.Current |= StateList.WithEgg;
        if (isFreezing) inputList.Current |= StateList.Freezing;
    }
    
    /*public Vector2 UpdateInput(ref StateStruct inputList, bool isFreezing, bool isWithEgg,
        NetworkManager _networkManager, float _deltaTime)
    {
        inputList.Update();
        _newState = Keyboard.GetState();
        _mouseState = Mouse.GetState();
        if (_newState.IsKeyDown(Keys.Up)) inputList.Current |= StateList.Up;
        if (_newState.IsKeyDown(Keys.Down)) inputList.Current |= StateList.Down;
        if (_newState.IsKeyDown(Keys.Left)) inputList.Current |= StateList.Left;
        if (_newState.IsKeyDown(Keys.Right)) inputList.Current |= StateList.Right;
        if (isWithEgg) inputList.Current |= StateList.WithEgg;
        if (_newState.IsKeyDown(Keys.F) && !isFreezing && !isWithEgg) inputList.Current |= StateList.Reload;
        if (_newState.IsKeyDown(Keys.T) && !isFreezing) inputList.Current |= StateList.TakingEgg;

        if (_newState.IsKeyDown(Keys.M) && !isFreezing && isWithEgg) inputList.Current |= StateList.PuttingEgg;

        if (_mouseState.RightButton == ButtonState.Pressed && !isFreezing && !isWithEgg)
            inputList.Current |= StateList.Shoot;
        // Calcolo automatico di IsMoving
        movementKeyPressed =
            _newState.IsKeyDown(Keys.Up) || _newState.IsKeyDown(Keys.Down) ||
            _newState.IsKeyDown(Keys.Left) || _newState.IsKeyDown(Keys.Right);
        if (movementKeyPressed && !isFreezing) inputList.Current |= StateList.Moving;
        if (isFreezing) inputList.Current |= StateList.Freezing;
        Vector2 vector2 = Vector2.Zero;
        _networkManager.Receive(
            (ackSeq, x, y) =>
            {
                // Qui il server ti dà la tua posizione "vera"
                // Per ora sovrascriviamo, poi potrai fare interpolazione
                //vector2 = new Vector2(x, y);
            },
            (rx, ry, rMask) =>
            {
                // Aggiorna il pinguino rosso
                vector2 = new Vector2(rx, ry);
                // Opzionale: aggiorna anche l'animazione dell'avversario
                // _redPenguin._penguinInputHandler._stateStruct.Current = (StateList)rMask;
            })
            ;
        return vector2;
    
}*/
}