using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public interface IMovements
{
    
    public Vector2 GetMousePosition();

    public Vector2 UpdateInput(ref StateStruct stateStruct, bool isFreezing, bool isWithEgg, NetworkManager _networkManager, float _deltaTime
    );
    
}