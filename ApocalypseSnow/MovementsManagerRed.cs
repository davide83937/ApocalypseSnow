namespace ApocalypseSnow;
using Microsoft.Xna.Framework;

public class MovementsManagerRed:IMovements
{
    
    public Vector2 GetMousePosition()
    {
        Vector2 mousePosition = new Vector2(0, 0);
        return mousePosition;
    }
    
    public void UpdateInput(ref StateStruct inputList, bool isFreezing)
    {
        inputList.Update();
        if (false) inputList.Current |= StateList.Up;
        if (false) inputList.Current |= StateList.Down;
        if (false) inputList.Current |= StateList.Left;
        if (false) inputList.Current |= StateList.Right;
        if (false) inputList.Current |= StateList.Reload;
        if (false) inputList.Current |= StateList.Shoot;
    
        // Calcolo automatico di IsMoving
        if (false) inputList.Current |= StateList.Moving;
        if (isFreezing) inputList.Current |= StateList.Freezing;
    }
}