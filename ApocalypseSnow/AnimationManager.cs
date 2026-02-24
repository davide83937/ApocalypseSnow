using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class AnimationManager:IAnimation
{
    public string _ballTag  { get; set; }
    public Texture2D Texture { get; set; }
    private readonly Texture2D[] _textures = new Texture2D[6];
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

    public AnimationManager()
    {
        _ballTag = "Ball";
    }
    
    private void load_texture(GraphicsDevice gd, int index, string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
        _textures[index] = Texture2D.FromStream(gd, stream);
    }

    private void ChangeTexture(SpriteBatch spriteBatch, int ammo, bool isReloading, bool isShooting, bool isFreezing,bool isWithEgg, ref Vector2 position)
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
        else if(isFreezing)
        {
            Texture = _textures[4];
        }
        else if(isWithEgg)
        {
            Texture = _textures[5];
        }
        else
        {
            Texture = _textures[1];
        }
        spriteBatch.Draw(Texture, position, _sourceRect, Color.White);
    }

    private void walking_animation(ref float gameTime, ref bool isReloading, ref bool isMoving, bool isWithEgg)
    {
        if (isMoving || isReloading)
        {
            _tempTime += gameTime;
            if (_tempTime > FrameSpeed)
            {
                _currentFrame++;
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
        load_texture(graphicsDevice, 3, "Content/images/penguin_blue_launch.png");
        load_texture(graphicsDevice, 4, "Content/images/penguin_blue_freezed3.png");
        load_texture(graphicsDevice, 5, "Content/images/penguin_blue_walking_egg.png");
        Texture = _textures[1];
        _sourceRect = new Rectangle(0, 0, Texture.Width / 3, Texture.Height/4);
    }

   

    public void Update(float gameTime, bool isMoving, bool isReloading, bool isWithEgg)
    {
        walking_animation(ref gameTime, ref isReloading, ref isMoving, isWithEgg);
    }



    public void Draw(SpriteBatch spriteBatch, ref Vector2 position, int ammo, bool isReloading, bool isShooting, bool isFreezing, bool isWithEgg)
    {
        ChangeTexture(spriteBatch, ammo, isReloading, isShooting, isFreezing, isWithEgg, ref position);
    }

    public void MoveRect(int posRect)
    {
        _sourceRect.Y = posRect;
    }
}