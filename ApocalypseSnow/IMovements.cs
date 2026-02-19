using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public interface IMovements
{
    public void moveOn(ref bool isW);
    public void moveBack(ref bool isS);
    public void moveRight(ref bool isD);
    public void moveLeft(ref bool isA);
    
    public void moveReload(ref bool isR);
    public void checkPressMouse (ref bool isLeft);
    public Vector2 getMousePosition();
}