using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public interface IAnimation
{
    Texture2D _texture { get; set; }
    Texture2D this[int index] { get; }
    
    public void Load_Content(GraphicsDevice graphicsDevice);
    void Update(float gameTime, bool isMoving, bool isReloading);
    void Draw(SpriteBatch spriteBatch, Vector2 position, int ammo, bool isReloading, bool isShooting);

    void moveRect(int posRect);
}
