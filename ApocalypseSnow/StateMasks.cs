namespace ApocalypseSnow;

// Gruppi di bit condivisi: evita di ripetere gli OR nel ciclo di gioco.
internal static class StateMasks
{
    private const StateList Directions =
        StateList.Up | StateList.Down | StateList.Left | StateList.Right;

    private const StateList ReloadAndEggActions =
        StateList.Reload | StateList.TakingEgg | StateList.PuttingEgg;

    // Input da neutralizzare prima dell'invio, quando il pinguino e' congelato.
    public const StateList InputsBlockedByFreeze =
        Directions | ReloadAndEggActions |
        StateList.Moving | StateList.ShootLeft | StateList.ShootRight;

    // Raccolta e deposito dipendono dalla collisione, non bloccano WASD.
    public static bool BlocksMovement(StateList state) =>
        (state & (StateList.Reload | StateList.Freezing)) != StateList.None;

    // Il replay riguarda soltanto gli stati che incidono sul movimento.
    public const StateList PositionReconciliationInputs =
        Directions | StateList.Reload | StateList.Freezing;
}