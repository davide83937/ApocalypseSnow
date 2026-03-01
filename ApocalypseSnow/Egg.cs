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
        CollisionManager.Instance.addObject(_tag, _position.X, _position.Y, _texture.Width, _texture.Height );
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
    
  
    
    protected override void OnCollisionEnter(string otherTag, CollisionRecordOut collisionRecordOut)
    {
        //if (!collisionRecordOut.Involves(_tag)) return;
        //string otherTag = collisionRecordOut.GetOtherTag(_tag);
        switch (otherTag)
        {
            case "obstacle":
                HandleObstacleCollision(collisionRecordOut._type);
                CollisionManager.Instance.modifyObject(_tag, _position.X, _position.Y, _texture.Width, _texture.Height);
                break;
        }
    }
    
    private void HandleObstacleCollision(int collisionType)
    {
        const int bounceDistance = 5;
        switch (collisionType)
        {
            case 1: _position.Y -= bounceDistance; break; // TOP
            case 2: _position.Y += bounceDistance; break; // BOTTOM
            case 3: _position.X += bounceDistance; break; // LEFT
            case 4: _position.X -= bounceDistance; break; // RIGHT
        }
    }

    public override void Update(GameTime gameTime)
    {
        CollisionManager.Instance.modifyObject(_tag, _position.X, _position.Y, _texture.Width, _texture.Height);
        base.Update(gameTime);
    }
}