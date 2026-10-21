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

    // Il bit del server serve a non anticipare il drop dell'uovo durante un HIT.
    // L'animazione del freeze continua a usare la timeline locale.
    public bool IsServerFrozen => (_remoteState & StateList.Freezing) != 0;

    
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

        // Il server decide l'HIT; il tick di freeze visualizzato dal remoto
        // si ricostruisce sulla timeline locale, senza copiare il suo bit Freezing.
        inputList.Current = _remoteState & ~StateList.Freezing;
        if (isFreezing)
            inputList.Current |= StateList.Freezing;

        // Il bit del server non allunga l'animazione del freeze locale, ma
        // impedisce di mostrare movimenti quando lo stato autoritativo e' fermo.
        if (inputList.IsPressed(StateList.Freezing) || IsServerFrozen)
            inputList.Current &= ~(StateList.Moving | StateList.Up | StateList.Down |
                                   StateList.Left | StateList.Right | StateList.ShootLeft |
                                   StateList.ShootRight);
    }
}