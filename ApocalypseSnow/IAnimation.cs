using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public interface IAnimation
{
    Texture2D Texture { get; set; }
    Texture2D this[int index] { get; }
    
    Rectangle SourceRect { get; }
    
    public void Load_Content(GraphicsDevice graphicsDevice);
    void Update(ref float gameTime, ref bool isMoving, ref bool isReloading);
    void Draw(SpriteBatch spriteBatch, ref Vector2 position, ref int ammo, ref bool isReloading, ref bool isShooting);

    void MoveRect(int posRect);
}
