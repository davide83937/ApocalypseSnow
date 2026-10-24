using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpDX;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace ApocalypseSnow;

public class Egg:CollisionExtensions
{
    public Texture2D _texture;
    //public string _tag;
    //public Vector2 _position;

    public Egg(Game game, Vector2 startPosition, string tag) : base(game, tag, startPosition)
    {
        //_position = startPosition;
        //_tag = tag;
    }

    private void load_texture(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
        this._texture = Texture2D.FromStream(GraphicsDevice, stream);
    }
    
    protected override void LoadContent()
    {
        load_texture("Content/images/egg.png");
        CollisionManager.Instance.addObject(_tag, _position.X + _texture.Width / 2f,
            _position.Y + _texture.Height / 2f, _texture.Width, _texture.Height);
        base.LoadContent();
        base.LoadContent();
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(_texture, _position,null, Color.White, 0f, 
            Vector2.Zero, 
            1, 
            SpriteEffects.None, 
            0f);
    }
    
  
    
    // L'uovo usa la stessa risoluzione geometrica dei pinguini.
    // Le collisioni con il muro non vanno corrette nell'evento (frame successivo).
    protected override void OnCollisionEnter(string otherTag, CollisionRecordOut collisionRecordOut) { }

    public void ResolveObstacleOverlaps()
    {
        Vector2 half = new(_texture.Width / 2f, _texture.Height / 2f);
        Vector2 center = CollisionManager.Instance.ResolveObstacleOverlaps(
            _position + half, _texture.Width, _texture.Height);
        _position = center - half;
        CollisionManager.Instance.modifyObject(_tag, center.X, center.Y,
            _texture.Width, _texture.Height);
    }

    public override void Update(GameTime gameTime)
    {
        ResolveObstacleOverlaps();
        base.Update(gameTime);
    }
}