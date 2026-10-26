using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class BasePlatform:DrawableGameComponent
{
    public Texture2D _texture;
    public string _tag;
    public Vector2 _position;
    public string _path;
    
    public BasePlatform(Game game, Vector2 startPosition, string tag, string path) : base(game)
    {
        _position = startPosition;
        _tag = tag;
        _path = path;
    }
    
    private void load_texture(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
        this._texture = Texture2D.FromStream(GraphicsDevice, stream);
    }

    protected override void LoadContent()
    {
        load_texture(_path);
        CollisionManager.Instance.addObject(_tag, _position.X, _position.Y, _texture.Width, _texture.Height );
        //Console.WriteLine($"{_texture.Width}, {_texture.Height}");
        base.LoadContent();
    }
    

}