using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class Obstacle:DrawableGameComponent
{
    private Texture2D _texture;
    private string _tag;
    private Rectangle _sourceRect;
    private Vector2 _position;
    private int _posX;
    private int _posY;
    
    
    public Obstacle(Game game, Vector2 position, int posX, int posY) : base(game)
    {
        _position = position;
        _posX = posX;
        _posY = posY;
        _tag = "obstacle";
    }

    private Vector2 GetPosition(int x, int y)
    {
        float posX = x * (_texture.Width / 2f);
        float posY = y * (_texture.Height / 2f);
        Vector2 pos = new Vector2(posX, posY);
        return pos;
    }
    
    private void load_texture(GraphicsDevice gd, string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
        _texture = Texture2D.FromStream(gd, stream);
    }

    protected override void LoadContent()
    {
        //Vector2 position = GetPosition(_posX,  _posY);
        load_texture(GraphicsDevice, "Content/images/ostacoli1.png");
        Vector2 position = GetPosition(_posX,  _posY);
        _sourceRect = new Rectangle((int)position.X, (int)position.Y, (_texture.Width / 2), _texture.Height/2);
        CollisionManager.Instance.addObject(_tag, _posX, _posY, _texture.Width/2, _texture.Height/2);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        
        spriteBatch.Draw(_texture, _position, _sourceRect, Color.White);
    }
}