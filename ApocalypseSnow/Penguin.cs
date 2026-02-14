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
    private Texture2D[] _textures;
    private string _currentKey; // La chiave della texture attiva
    private Texture2D _texture;
    private Vector2 _position;
    private Vector2 _speed;
    private Rectangle _sourceRect;
    private KeyboardState _oldState;
    private MouseState _oldMouseState;
    private float _pressedTime = 0.0f;
    private int _ammo;
    public int Ammo { get { return _ammo; } set { _ammo = value; } }
    private float temp_time;
    private float reload_time;
    private bool isMoving = false;
    private bool isReloading = false;
    private bool isShooting = false;
    private int _currentFrame;     // L'indice del frame attuale (0, 1 o 2)
    private static readonly float _frameSpeed = 0.1f; // Velocità dell'animazione (più basso = più veloce)
    private static readonly int _frameReload = 3;
    //public event Action<Vector2> OnSpawnBall;
    
    public Penguin(Game game, Vector2 startPosition, Vector2 startSpeed) : base(game)
    {
        gameContext = game;
        //_texture = texture;
        _position = startPosition;
        _speed = startSpeed;
        _ammo = 100;
        
        // Creiamo il rettangolo: (X iniziale, Y iniziale, Larghezza, Altezza)
        // Partiamo da (0,0) per prendere il primo in alto a sinistra
        _sourceRect = new Rectangle(0, 0, 0, 0);
        _textures = new Texture2D[4];
    }
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void uniform_rectilinear_motion(ref float position, float velocity, float deltaTime);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void normalizeVelocity(ref float velocityX, ref float velocityY);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parabolic_motion(float gravity, float startPositionX, float startPositionY, out float positionX, out float positionT, float startVelocityX,
        float startVelocityY, float gameTime);

    private void load_texture(int index,string path)
    {
        using (var stream = System.IO.File.OpenRead(path))
        {
            // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
            this._textures[index] = Texture2D.FromStream(GraphicsDevice, stream);
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

    private void chargeShot(MouseState mouseState, ref float pressedTime, float deltaTime)
    {
        if (mouseState.LeftButton == ButtonState.Pressed && _ammo > 0)
        {
            //Console.WriteLine($"Valore click:X = {mouseState.X}, Y = {mouseState.Y}");
            isShooting = true;
            //Console.WriteLine($"Valore delta: {deltaTime}");
            deltaTime *= 100;
            pressedTime += deltaTime;
            //Console.WriteLine($"Valore delta: {pressedTime}");
            if (pressedTime > 500)
            {
                pressedTime = 500;
            }
        }
    }

    private void shot(MouseState mouseState, MouseState lastMouseState, float pressedTime)
    {
        if (mouseState.LeftButton == ButtonState.Released && lastMouseState.LeftButton == ButtonState.Pressed && _ammo > 0)
        {
            float differenceX = _position.X - mouseState.X;
            float differenceY = _position.Y - mouseState.Y;
            float coX = (differenceX/100)* (-1);
            
            //Console.WriteLine("La differenza e': X = "+differenceX+",  Y = "+differenceY);
            Vector2 startSpeed = new Vector2(coX, differenceY / 100) * pressedTime;
            Vector2 finalPosition = finalPoint(startSpeed, _position);
            Ball b = new Ball(gameContext, _position, startSpeed, finalPosition);
            gameContext.Components.Add(b);
            isShooting = false;
            //Console.WriteLine($"Valore: {_pressedTime}");
            _pressedTime = 0;
            _ammo--;
        }
    }

    private Vector2 finalPoint(Vector2 _start_speed, Vector2 _start_position)
    {
        // Punto X finale: X0 + (VelocitàX * Tempo)
        //float x_finale = (_start_position.X + 20) + (_start_speed.X )*1.5f;
  
        parabolic_motion(150f,
            _start_position.X + 20, 
            _start_position.Y, 
            out float x, out float y,
            _start_speed.X, 
            -_start_speed.Y, 
            1.5f // Il "tempo" finale desiderato
        );

        Vector2 impatto = new Vector2(x, y);
        
        // Salviamo il punto di impatto completo (X e Y)
        Vector2 point_finale = new Vector2(impatto.X, impatto.Y);
        return point_finale;
    }
    
    
    protected override void LoadContent()
    {
        load_texture(0, "Content/images/penguin_blue_walking.png");
        load_texture(1, "Content/images/penguin_blue_walking_snowball.png");
        load_texture(2, "Content/images/penguin_blue_gathering.png");
        load_texture(3, "Content/images/penguin_blue_launch1.png");
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
            this._texture = _textures[0];
        }
        else if (isReloading && !isShooting)
        {
             this._texture = _textures[2];
        }
        else if(!isReloading && isShooting)
        {
            this._texture = _textures[3];
        }
        else
        {
            this._texture = _textures[1];
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
            uniform_rectilinear_motion(ref _position.Y, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _sourceRect.Y = 0 * (_texture.Height / 4);
            isMoving = true;
        }
        
        if (newState.IsKeyDown(Keys.W) && isReloading == false)
        {
            uniform_rectilinear_motion(ref _position.Y, -100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _sourceRect.Y = 3 * (_texture.Height / 4);
            isMoving = true;
        }
        
        if (newState.IsKeyDown(Keys.D) && isReloading == false)
        {
            uniform_rectilinear_motion(ref _position.X, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _sourceRect.Y = 2 * (_texture.Height / 4);
            isMoving = true;
        }
        
        if (newState.IsKeyDown(Keys.A) && isReloading == false)
        {
            uniform_rectilinear_motion(ref _position.X, -100, deltaTime);
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
        //_pressedTime *= 10;
        chargeShot(mouseState, ref _pressedTime, deltaTime);
        shot(mouseState, _oldMouseState, _pressedTime);
        _oldState = newState;
        _oldMouseState = mouseState;

    }
}