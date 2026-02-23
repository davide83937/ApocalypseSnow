using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public interface IMovements
{
  
    public Vector2 GetMousePosition();
    public void UpdateInput(ref StateStruct inputList, bool isFreezing);
    
}