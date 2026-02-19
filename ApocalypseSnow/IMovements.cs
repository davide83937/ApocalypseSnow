using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public interface IMovements
{
    public void moveOn(ref bool isW);
    public void moveBack();
    public void moveRight();
    public void moveLeft();
}