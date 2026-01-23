using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ApocalypseSnow;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public class Penguin: DrawableGameComponent
{
    private Game gameContext;
    // Dizionario per contenere tutte le texture
    private Dictionary<int, Texture2D> _textures;
    private string _currentKey; // La chiave della texture attiva
    private Texture2D _texture;
    private Vector2 _position;
    private Vector2 _speed;
    private Rectangle _sourceRect;
    private KeyboardState _oldState;
    private MouseState _oldMouseState;
    private float _pressedTime = 0.0f;
    public int _ammo;
    private float temp_time;
    private float reload_time;
    private bool isMoving = false;
    private bool isReloading = false;
    private bool isShooting = false;
    private int _currentFrame;     // L'indice del frame attuale (0, 1 o 2)
    private float _frameSpeed = 0.1f; // Velocità dell'animazione (più basso = più veloce)
    private int _frameReload = 3;
    public event Action<Vector2> OnSpawnBall;
    
    public Penguin(Game game, Vector2 startPosition, Vector2 startSpeed) : base(game)
    {
        gameContext = game;
        //_texture = texture;
        _position = startPosition;
        _speed = startSpeed;
        _ammo = 0;
        
        // Creiamo il rettangolo: (X iniziale, Y iniziale, Larghezza, Altezza)
        // Partiamo da (0,0) per prendere il primo in alto a sinistra
        _sourceRect = new Rectangle(0, 0, 0, 0);
        _textures = new Dictionary<int, Texture2D>();
    }
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern float uniform_rectilinear_motion(float position, float velocity, float deltaTime);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void normalizeVelocity(ref float velocityX, ref float velocityY);

    private void load_texture(int key,string path)
    {
        using (var stream = System.IO.File.OpenRead(path))
        {
            // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
            this._textures[key] = Texture2D.FromStream(GraphicsDevice, stream);
        }
    }

    private void reload(float gameTime)
    {
        if (isReloading)
        {
            reload_time += gameTime;
            if (reload_time > _frameReload)
            {
                _ammo++;
                reload_time = 0f;
            }
        }
    }


    
    
    private void walking_animation(float gameTime)
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

    private void chargeShot(MouseState mouseState, MouseState lastMouseState, ref float pressedTime, float deltaTime)
    {
        if (mouseState.LeftButton == ButtonState.Pressed)
        {
            isShooting = true;
            pressedTime += deltaTime;
            if (pressedTime > 5)
            {
                pressedTime = 5;
            }
        }
    }

    private void shot(MouseState mouseState, MouseState lastMouseState, float pressedTime)
    {
        if (mouseState.LeftButton == ButtonState.Released && lastMouseState.LeftButton == ButtonState.Pressed)
        {
            Ball b = new Ball(gameContext, _position, new Vector2(1,1)* pressedTime);
            gameContext.Components.Add(b);
            isShooting = false;
            _pressedTime = 0;
        }
    }
    
    
    protected override void LoadContent()
    {
        load_texture(1, "Content/images/penguin_blue_walking.png");
        load_texture(2, "Content/images/penguin_blue_walking_snowball.png");
        load_texture(3, "Content/images/penguin_blue_gathering.png");
        load_texture(4, "Content/images/penguin_blue_launch1.png");
        _texture = _textures[1];
        // CALCOLO DEL RITAGLIO
        // Dividiamo la larghezza totale per 3 colonne
        int width = _texture.Width / 3; 
        // Dividiamo l'altezza totale per 4 righe
        int height = _texture.Height / 4;
        _sourceRect.Width = width;
        _sourceRect.Height = height;
    }
    
    
    
    public void Draw(SpriteBatch spriteBatch)
    {
        if (_ammo == 0 &&  !isReloading && !isShooting)
        {
            this._texture = _textures[1];
        }
        else if (isReloading && !isShooting)
        {
             this._texture = _textures[3];
        }
        else if(!isReloading && isShooting)
        {
            this._texture = _textures[4];
        }
        else
        {
            this._texture = _textures[2];
        }
        
        spriteBatch.Draw(_texture, _position, _sourceRect, Color.White);
    }

    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        KeyboardState newState = Keyboard.GetState();
        MouseState mouseState = Mouse.GetState();
        isMoving = false;

        
        
        if (newState.IsKeyDown(Keys.S) && isReloading == false)
        {
            this._position.Y = uniform_rectilinear_motion(_position.Y, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _sourceRect.Y = 0 * (_texture.Height / 4);
            isMoving = true;
        }
        
        if (newState.IsKeyDown(Keys.W) && isReloading == false)
        {
            this._position.Y = uniform_rectilinear_motion(_position.Y, -100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _sourceRect.Y = 3 * (_texture.Height / 4);
            isMoving = true;
        }
        
        if (newState.IsKeyDown(Keys.D) && isReloading == false)
        {
            this._position.X = uniform_rectilinear_motion(_position.X, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _sourceRect.Y = 2 * (_texture.Height / 4);
            isMoving = true;
        }
        
        if (newState.IsKeyDown(Keys.A) && isReloading == false)
        {
            this._position.X = uniform_rectilinear_motion(_position.X, -100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _sourceRect.Y = 1 * (_texture.Height / 4);
            isMoving = true;
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

        if (newState.IsKeyDown(Keys.R))
        {
            isReloading = true;
        }
        
        if (newState.IsKeyUp(Keys.R) && _oldState.IsKeyDown(Keys.R))
        {
            isReloading = false;
            reload_time = 0f;
        }
        
        normalizeVelocity(ref this._speed.X, ref this._speed.Y);
        walking_animation(deltaTime);
        reload(deltaTime);
        _pressedTime *= 10;
        chargeShot(mouseState, _oldMouseState, ref _pressedTime, deltaTime);
        shot(mouseState, _oldMouseState, _pressedTime);
        _oldState = newState;
        _oldMouseState = mouseState;

    }
}