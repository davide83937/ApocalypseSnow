using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;
using Microsoft.Xna.Framework;

public class MovementsManagerRed:IMovements
{
    //private KeyboardState _newState = Keyboard.GetState();
    private MouseState _mouseState = Mouse.GetState();
    private bool movementKeyPressed = false;
    private bool isFreezing = false;
    private bool isWithEgg = false;
    private StateList _remoteState = StateList.None;
 
    
    public MovementsManagerRed()
    {
        if (NetworkManager.Instance != null)
        {
            // Ci iscriviamo all'evento per ricevere i dati remoti
            NetworkManager.Instance.OnRemoteStateReceived += HandleRemoteState;
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

        // La prediction locale blocca il remoto anche prima della conferma server.
        if (isFreezing)
            inputList.Current |= StateList.Freezing;

        if (inputList.IsPressed(StateList.Freezing))
            inputList.Current &= ~(StateList.Moving | StateList.Up | StateList.Down |
                                   StateList.Left | StateList.Right | StateList.ShootLeft |
                                   StateList.ShootRight);
    }
}