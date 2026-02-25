using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public interface IMovements
{
    
    public Vector2 GetMousePosition();
    //public void increaseTimeFreezing(float _deltaTime);
    //public bool isTakingEggJustReleased();
    //public bool isPuttingEggJustReleased();

    public void UpdateInput(ref StateStruct stateStruct, bool isFreezing, bool isWithEgg);
    
}