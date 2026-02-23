using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public interface IAnimation
{
    Texture2D Texture { get; set; }
    public string _ballTag  { get; set; }
    Texture2D this[int index] { get; }
    
    Rectangle SourceRect { get; }
    
    public void Load_Content(GraphicsDevice graphicsDevice);
    void Update(float gameTime, bool isMoving, bool isReloading);
    void Draw(SpriteBatch spriteBatch, ref Vector2 position, int ammo, bool isReloading, bool isShooting, bool isFreezing);

    void MoveRect(int posRect);
}
