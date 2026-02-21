using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class AnimationManager:IAnimation
{
    public Texture2D Texture { get; set; }
    private readonly Texture2D[] _textures = new Texture2D[4];
    private static readonly float FrameSpeed;
    private float _tempTime;
    private int _currentFrame;
    private Rectangle _sourceRect;
    public Rectangle SourceRect{ get => _sourceRect;}
    public Texture2D this[int index] => _textures[index];

    static AnimationManager()
    {
        FrameSpeed = 0.1f;
    }

    private void load_texture(GraphicsDevice gd, int index, string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
        _textures[index] = Texture2D.FromStream(gd, stream);
    }

    private void ChangeTexture(SpriteBatch spriteBatch, int ammo, bool isReloading, bool isShooting, ref Vector2 position)
    {
        if (ammo == 0 &&  !isReloading && !isShooting)
        {
            Texture = _textures[0];
        }
        else if (isReloading && !isShooting)
        {
            Texture = _textures[2];
        }
        else if(!isReloading && isShooting)
        {
            Texture = _textures[3];
        }
        else
        {
            Texture = _textures[1];
        }
        spriteBatch.Draw(Texture, position, _sourceRect, Color.White);
    }

    private void walking_animation(ref float gameTime, ref bool isReloading, ref bool isMoving)
    {
        if (isMoving || isReloading)
        {
            _tempTime += gameTime;
            if (_tempTime > FrameSpeed)
            {
                _currentFrame++;
                // Dato che la tua texture ha 3 colonne (_texture.Width / 3)
                if (_currentFrame >= 3) 
                    _currentFrame = 0;
                _tempTime = 0f;
            }
        }
        else
        {
            _currentFrame = 1; // Frame di riposo (solitamente quello centrale)
        }
        // Applichiamo il calcolo della X nel rettangolo di ritaglio
        _sourceRect.X = _currentFrame * (Texture.Width / 3);
    }


    public void Load_Content(GraphicsDevice graphicsDevice)
    {
        load_texture(graphicsDevice, 0, "Content/images/penguin_blue_walking.png");
        load_texture(graphicsDevice, 1, "Content/images/penguin_blue_walking_snowball.png");
        load_texture(graphicsDevice, 2, "Content/images/penguin_blue_gathering.png");
        load_texture(graphicsDevice, 3, "Content/images/penguin_blue_launch2.png");
        Texture = _textures[1];
        _sourceRect = new Rectangle(0, 0, Texture.Width / 3, Texture.Height/4);
    }

   

    public void Update(float gameTime, bool isMoving, bool isReloading)
    {
        walking_animation(ref gameTime, ref isReloading, ref isMoving);
    }
    

    public void Draw(SpriteBatch spriteBatch, ref Vector2 position, int ammo, bool isReloading, bool isShooting)
    {
        ChangeTexture(spriteBatch, ammo, isReloading, isShooting, ref position);
    }

    public void MoveRect(int posRect)
    {
        _sourceRect.Y = posRect;
    }
}