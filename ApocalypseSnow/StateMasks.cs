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

    // Direzioni e stati che il Reconciler deve conoscere.
    public const StateList PositionReconciliationInputs =
        Directions | ReloadAndEggActions | StateList.Freezing;
}