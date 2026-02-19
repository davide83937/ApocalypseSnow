using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public interface IMovements
{
    public void moveOn(ref bool isW);
    public void MoveBack(ref bool isS);
    public void MoveRight(ref bool isD);
    public void MoveLeft(ref bool isA);
    
    public void MoveReload(ref bool isR);
    public void CheckPressMouse (ref bool isLeft);
    public Vector2 GetMousePosition();
}