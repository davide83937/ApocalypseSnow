using System.Runtime.InteropServices;

namespace ApocalypseSnow;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public class Penguin: DrawableGameComponent
{
    private Texture2D _texture;
    private Vector2 _position;
    private Vector2 _speed;
    private Rectangle _sourceRect;
    private KeyboardState _oldState;
    
    public Penguin(Game game, Texture2D texture, Vector2 startPosition, Vector2 startSpeed) : base(game)
    {
        _texture = texture;
        _position = startPosition;
        _speed = startSpeed;
        
        // CALCOLO DEL RITAGLIO
        // Dividiamo la larghezza totale per 3 colonne
        int width = _texture.Width / 3; 
        // Dividiamo l'altezza totale per 4 righe
        int height = _texture.Height / 4; 

        // Creiamo il rettangolo: (X iniziale, Y iniziale, Larghezza, Altezza)
        // Partiamo da (0,0) per prendere il primo in alto a sinistra
        _sourceRect = new Rectangle(0, 0, width, height);
    }
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern float uniform_rectilinear_motion(float position, float velocity, float deltaTime);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void normalizeVelocity(ref float velocityX, ref float velocityY);
    
    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(_texture, _position, _sourceRect, Color.White);
    }

    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        KeyboardState newState = Keyboard.GetState();
        
        if (newState.IsKeyDown(Keys.S))
        {
            this._position.Y = uniform_rectilinear_motion(_position.Y, 100, deltaTime);
            _sourceRect.Y = 0 * (_texture.Height / 4);
        }
        
        if (newState.IsKeyDown(Keys.W))
        {
            this._position.Y = uniform_rectilinear_motion(_position.Y, -100, deltaTime);
            _sourceRect.Y = 3 * (_texture.Height / 4);
        }
        
        if (newState.IsKeyDown(Keys.D))
        {
            this._position.X = uniform_rectilinear_motion(_position.X, 100, deltaTime);
            _sourceRect.Y = 2 * (_texture.Height / 4);
        }
        
        if (newState.IsKeyDown(Keys.A))
        {
            this._position.X = uniform_rectilinear_motion(_position.X, -100, deltaTime);
            _sourceRect.Y = 1 * (_texture.Height / 4);
        }
        
        if (newState.IsKeyUp(Keys.S) && _oldState.IsKeyDown(Keys.S))
        {
            this._speed.Y = 0;
        }
        
        if (newState.IsKeyUp(Keys.D) && _oldState.IsKeyDown(Keys.D))
        {
            this._speed.X = 0;
        }
        
        if (newState.IsKeyUp(Keys.A) && _oldState.IsKeyDown(Keys.A))
        {
            this._speed.X = 0;
        }
        
        if (newState.IsKeyUp(Keys.W) && _oldState.IsKeyDown(Keys.W))
        {
            this._speed.Y = 0;
        }
        
        normalizeVelocity(ref this._speed.X, ref this._speed.Y);
        _oldState = newState;
   
    }
}