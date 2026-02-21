namespace ApocalypseSnow;

public struct StateStruct
{
    public StateList Current;
    public StateList Old;

    // Metodo per aggiornare lo stato (da chiamare a inizio frame)
    public void Update()
    {
        Old = Current;
        Current = StateList.None;
    }

    // Il tasto è tenuto premuto in questo frame?
    public bool IsPressed(StateList action)
    {
        return Current.HasFlag(action);
    }

    // Il tasto è stato premuto ESATTAMENTE in questo frame? (Utile per sparare un solo colpo)
    public bool JustPressed(StateList action)
    {
        return Current.HasFlag(action) && !Old.HasFlag(action);
    }

    // Il tasto è stato appena rilasciato in questo frame?
    public bool JustReleased(StateList action)
    {
        return !Current.HasFlag(action) && Old.HasFlag(action);
    }
}