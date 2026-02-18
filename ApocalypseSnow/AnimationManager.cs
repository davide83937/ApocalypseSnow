using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class AnimationManager:IAnimation
{
    public Texture2D _texture { get; set; }
    public Texture2D[] _textures;
    private static readonly float _frameSpeed;
    private float temp_time;
    private int _currentFrame;
    private Rectangle _sourceRect;

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
    
    public void load_texture(GraphicsDevice gd, int index, string path)
    {
        using (var stream = System.IO.File.OpenRead(path))
        {
            // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
            this._textures[index] = Texture2D.FromStream(gd, stream);
        }
    }

    public void changeTexture(Rectangle _sourceRect, SpriteBatch spriteBatch, Texture2D _texture, ref int _ammo, bool isReloading, bool isShooting, ref Vector2 _position)
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
    
    public void walking_animation(Texture2D _texture, ref Rectangle _sourceRect, ref float gameTime, bool isReloading, bool isMoving)
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


    public void Load_Content(GraphicsDevice graphicsDevice)
    {
        load_texture(graphicsDevice, 0, "Content/images/penguin_blue_walking.png");
        load_texture(graphicsDevice, 1, "Content/images/penguin_blue_walking_snowball.png");
        load_texture(graphicsDevice, 2, "Content/images/penguin_blue_gathering.png");
        load_texture(graphicsDevice, 3, "Content/images/penguin_blue_launch1.png");
        _texture = _textures[1];
        _sourceRect = new Rectangle(0, 0, _texture.Width / 3, _texture.Height/4);
    }

    public void Update(float gameTime, bool isMoving, bool isReloading)
    {
        walking_animation( _texture, ref _sourceRect, ref gameTime, isReloading, isMoving);

        
    }
    

    public void Draw(SpriteBatch spriteBatch, Vector2 position, int ammo, bool isReloading, bool isShooting)
    {
        changeTexture(_sourceRect, spriteBatch, _texture, ref ammo, isReloading, isShooting, ref position);
    }

    public void moveRect(int posRect)
    {
        _sourceRect.Y = posRect;
    }
}