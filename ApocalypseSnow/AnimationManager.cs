using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class AnimationManager
{
    private Texture2D[] _textures;
    private static readonly float _frameSpeed;
    public Texture2D this[int index]
    {
        get => _textures[index];
    }


    static AnimationManager()
    {
        _frameSpeed = 0.1f;
    }

    public AnimationManager()
    {
        _textures = new Texture2D[4];
    }
    
    public void load_texture(GraphicsDevice gd, int index,string path)
    {
        using (var stream = System.IO.File.OpenRead(path))
        {
            // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
            this._textures[index] = Texture2D.FromStream(gd, stream);
        }
    }

    public void changeTexture(Rectangle _sourceRect, SpriteBatch spriteBatch, Texture2D _texture, int _ammo, bool isReloading, bool isShooting, Vector2 _position)
    {
        if (_ammo == 0 &&  !isReloading && !isShooting)
        {
            _texture = _textures[0];
        }
        else if (isReloading && !isShooting)
        {
            _texture = _textures[2];
        }
        else if(!isReloading && isShooting)
        {
            _texture = _textures[3];
        }
        else
        {
            _texture = _textures[1];
        }
        spriteBatch.Draw(_texture, _position, _sourceRect, Color.White);
    }
    
    public void walking_animation(Texture2D _texture, ref Rectangle _sourceRect, float gameTime, float temp_time, bool isReloading, bool isMoving, int _currentFrame)
    {
        if (isMoving || isReloading)
        {
            temp_time += gameTime;
            if (temp_time > _frameSpeed)
            {
                _currentFrame++;
                // Dato che la tua texture ha 3 colonne (_texture.Width / 3)
                if (_currentFrame >= 3) 
                    _currentFrame = 0;
                temp_time = 0f;
            }
        }
        else
        {
            _currentFrame = 1; // Frame di riposo (solitamente quello centrale)
        }
        // Applichiamo il calcolo della X nel rettangolo di ritaglio
        _sourceRect.X = _currentFrame * (_texture.Width / 3);
    }


}