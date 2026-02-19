using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class Obstacle:DrawableGameComponent
{
    private Texture2D _texture;
    private Rectangle _sourceRect;
    private Vector2 _position;
    
    public Obstacle(Game game, Vector2 position) : base(game)
    {
        _position = position;
    }
    
    private void load_texture(GraphicsDevice gd, string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
        _texture = Texture2D.FromStream(gd, stream);
    }

    protected override void LoadContent()
    {
        load_texture(GraphicsDevice, "Content/images/ostacoli.png");
        _sourceRect = new Rectangle(0, 0, _texture.Width / 2, _texture.Height/2);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        
        spriteBatch.Draw(_texture, _position, _sourceRect, Color.White);
    }
}