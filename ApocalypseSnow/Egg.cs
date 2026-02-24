using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpDX;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace ApocalypseSnow;

public class Egg:DrawableGameComponent
{
    public Texture2D _texture;
    public string _tag;
    public Vector2 _position;
    

    public Egg(Game game, Vector2 startPosition, string tag) : base(game)
    {
        _position = startPosition;
        _tag = tag;
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
        CollisionManager.Instance.sendCollisionEvent += OnColliderEnter;
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
    
  
    
    void OnColliderEnter(object context, CollisionRecordOut collisionRecordOut)
    {
        if (_tag == collisionRecordOut._myTag || _tag == collisionRecordOut._otherTag)
        {
            string myTag = "";
            string otherTag = "";
            if (_tag == collisionRecordOut._myTag)
            {
                myTag = collisionRecordOut._myTag;
                otherTag = collisionRecordOut._otherTag;
            }
            else if (_tag == collisionRecordOut._otherTag)
            {
                myTag = collisionRecordOut._otherTag;
                otherTag = collisionRecordOut._myTag;
            }
            if (otherTag == "obstacle" || otherTag.StartsWith("egg"))
            {
                int numero = myTag[myTag.Length - 1] - '0';
                _position.Y += numero;
                CollisionManager.Instance.modifyObject(_tag, _position.X, _position.Y, _texture.Width, _texture.Height);
            }
        }
    }
}