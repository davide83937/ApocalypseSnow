
using System.Runtime.InteropServices;

namespace ApocalypseSnow;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

public class Penguin: DrawableGameComponent
{
    private Game gameContext;
    private IAnimation  _animationManager;
    private IMovements  _movementsManager;
    private Vector2 _position;
    private Vector2 _speed;
    private KeyboardState _oldState;
    private MouseState _oldMouseState;
    private float _pressedTime = 0.0f;
    private int _ammo;
    public int Ammo { get { return _ammo; } set { _ammo = value; } }
    private float reload_time;
    private bool isMoving = false;
    private bool isReloading = false;
    private bool isShooting = false;
    private bool isW = false;
    private bool isS = false;
    private bool isA = false;
    private bool isD = false;
    private bool isR = false;
    private bool isWold = false;
    private bool isSold = false;
    private bool isAold = false;
    private bool isDold = false;
    private bool isRold = false;
    private static readonly int _frameReload;

    static Penguin()
    {
        _frameReload = 3;
    }
    public Penguin(Game game, Vector2 startPosition, Vector2 startSpeed, IAnimation animation, IMovements movements) : base(game)
    {
        gameContext = game;
        _position = startPosition;
        _speed = startSpeed;
        _ammo = 100;
        _animationManager = animation;
        _movementsManager = movements;
    }
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void uniform_rectilinear_motion(ref float position, float velocity, float deltaTime);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void normalizeVelocity(ref float velocityX, ref float velocityY);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parabolic_motion(float gravity, float startPositionX, float startPositionY, 
        out float positionX, out float positionY, float startVelocityX,
        float startVelocityY, float gameTime);

    

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

    public void moveOn(float deltaTime)
    {
        _movementsManager.moveOn(ref isW);
        if (isW && isReloading == false)
        {
            uniform_rectilinear_motion(ref _position.Y, -100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.moveRect( 3 * (_animationManager._texture.Height / 4));
            isMoving = true;
        }
        if (!isW && isWold)
        {
            this._speed.Y = 0;
        }
    }

    public void moveBack(float deltaTime)
    {
        if (isS && isReloading == false)
        {
            uniform_rectilinear_motion(ref _position.Y, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.moveRect( 0 * (_animationManager._texture.Height / 4));
            isMoving = true;
        }
        
        if (!isS && isSold)
        {
            this._speed.Y = 0;
        }
    }
    
    public void moveRight(float deltaTime)
    {
        if (isD && isReloading == false)
        {
            uniform_rectilinear_motion(ref _position.X, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.moveRect( 2 * (_animationManager._texture.Height / 4));
            isMoving = true;
        }
        if (!isD && isDold)
        {
            this._speed.X = 0;
        }
        
    }
    
    public void moveLeft(float deltaTime)
    {
        if (isA && isReloading == false)
        {
            uniform_rectilinear_motion(ref _position.X, -100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.moveRect(1 * (_animationManager._texture.Height / 4));
            isMoving = true;
        }
        if (!isA && isAold)
        {
            this._speed.X = 0;
        }
    }
    
    
    protected override void LoadContent()
    {
        
        _animationManager.Load_Content(GraphicsDevice);
    }
    
    
    
    public void Draw(SpriteBatch spriteBatch)
    {
        _animationManager.Draw(spriteBatch, _position, _ammo, isReloading, isShooting);
    }

    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        KeyboardState newState = Keyboard.GetState();
        MouseState mouseState = Mouse.GetState();
        isMoving = false;

        //if (newState.IsKeyDown(Keys.W)) { isW = true; }else { isW = false; }
        if (newState.IsKeyDown(Keys.D)) { isD = true; }else { isD = false; }
        if (newState.IsKeyDown(Keys.A)) { isA = true; }else { isA = false; }
        if (newState.IsKeyDown(Keys.S)) { isS = true; }else { isS = false; }
        if (newState.IsKeyDown(Keys.R)) { isR = true; }else { isR = false; }
        
        
        
        moveBack(deltaTime);
        
        moveOn(deltaTime);
        
        moveRight(deltaTime);
        
        moveLeft(deltaTime);
        
        

        if (isR)
        {
            isReloading = true;
        }
        else
        {
            isReloading = false;
        }
        
        if (!isR && isRold)
        {
            isReloading = false;
            reload_time = 0f;
        }
        
        normalizeVelocity(ref this._speed.X, ref this._speed.Y);
        _animationManager.Update(deltaTime, isMoving, isReloading);
        reload(deltaTime);
        //_pressedTime *= 10;
        chargeShot(mouseState, ref _pressedTime, deltaTime);
        shot(mouseState, _oldMouseState, _pressedTime);
        isAold = isA;
        isDold = isD;
        isSold = isS;
        isWold = isW;
        _oldState = newState;
        _oldMouseState = mouseState;

    }
}