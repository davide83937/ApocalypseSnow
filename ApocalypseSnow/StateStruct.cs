namespace ApocalypseSnow;

public struct StateStruct
{
    public StateList Current;
    public StateList Old;
    public int? mouseX;
    public int? mouseY;


    public void Update()
    {
        Old = Current;
        Current = StateList.None;
    }

    public void resetMouse()
    {
        mouseX = null;
        mouseY = null;
    }
    
    public bool IsPressed(StateList action)
    {
        return Current.HasFlag(action);
    }
    
    public bool JustPressed(StateList action)
    {
        return Current.HasFlag(action) && !Old.HasFlag(action);
    }
    
    public bool JustReleased(StateList action)
    {
        return !Current.HasFlag(action) && Old.HasFlag(action);
    }
}