
using System;
using System.Runtime.InteropServices;

namespace ApocalypseSnow;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


public class Penguin: DrawableGameComponent
{
    private readonly string _tag;
    private int _countBall;
    private readonly Game _gameContext;
    private readonly IAnimation  _animationManager;
    private readonly IMovements  _movementsManager;
    private Vector2 _position;
    private Vector2 _speed;
    private float _pressedTime;
    private float _deltaTime;
    private int _ammo;
    public int Ammo{ get => _ammo; set => _ammo = value; }
    private float _reloadTime;
    private InputList _inputList;
    private static readonly int FrameReload;

    static Penguin()
    {
        FrameReload = 3;
    }
    public Penguin(Game game, Vector2 startPosition, Vector2 startSpeed, IAnimation animation, IMovements movements) : base(game)
    {
        _tag = "penguin";
        _gameContext = game;
        _position = startPosition;
        _speed = startSpeed;
        _ammo = 100;
        _animationManager = animation;
        _movementsManager = movements;
        _inputList = new InputList();
        _countBall = 0;
    }
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void uniform_rectilinear_motion(ref float position, float velocity, float deltaTime);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void normalizeVelocity(ref float velocityX, ref float velocityY);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parabolic_motion(float gravity, float startPositionX, float startPositionY, 
        out float positionX, out float positionY, float startVelocityX,
        float startVelocityY, float gameTime);

    

    private void Reload(float gameTime)
    {
        if (!_inputList.IsReloading) return;
        _reloadTime += gameTime;
        if (!(_reloadTime > FrameReload)) return;
        _ammo++;
        _reloadTime = 0f;
    }
    
    

    private void ChargeShot(bool isLeft, ref float pressedTime, float deltaTime)
    {
        _movementsManager.CheckPressMouse(ref isLeft);
        if (!isLeft || _ammo <= 0) return;
        //Console.WriteLine($"Valore click:X = {mouseState.X}, Y = {mouseState.Y}");
        _inputList.IsShooting = true;
        //Console.WriteLine($"Valore delta: {deltaTime}");
        deltaTime *= 100;
        pressedTime += deltaTime;
        //Console.WriteLine($"Valore delta: {pressedTime}");
        if (pressedTime > 500)
        {
            pressedTime = 500;
        }
        
    }

    private void Shot( float pressedTime)
    {
        _movementsManager.CheckPressMouse(ref _inputList.IsLeft);
        if (_inputList is not { IsLeft: false, IsLeftOld: true } || _ammo <= 0) return;
        Vector2 mouseState = _movementsManager.GetMousePosition();
        float differenceX = _position.X - mouseState.X;
        float differenceY = _position.Y - mouseState.Y;
        float coX = (differenceX/100)* (-1);
            
        //Console.WriteLine("La differenza e': X = "+differenceX+",  Y = "+differenceY);
        Vector2 startSpeed = new Vector2(coX, differenceY / 100) * pressedTime;
        Vector2 finalPosition = FinalPoint(startSpeed, _position);
        string tagBall = "P" + _countBall;
        Ball b = new Ball(_gameContext, _position, startSpeed, finalPosition, tagBall);
        _gameContext.Components.Add(b);
        _inputList.IsShooting = false;
        //Console.WriteLine($"Valore: {_pressedTime}");
        _pressedTime = 0;
        _ammo--;
        _countBall++;
    }

    private Vector2 FinalPoint(Vector2 startSpeed, Vector2 startPosition)
    {
        parabolic_motion(150f,
            startPosition.X + 20, 
            startPosition.Y, 
            out float x, out float y,
            startSpeed.X, 
            -startSpeed.Y, 
            1.5f // Il "tempo" finale desiderato
        );

        Vector2 impatto = new Vector2(x, y);
        
        // Salviamo il punto di impatto completo (X e Y)
        Vector2 pointFinale = new Vector2(impatto.X, impatto.Y);
        return pointFinale;
    }

    private void MoveOn(ref float deltaTime)
    {
        _movementsManager.moveOn(ref _inputList.IsW);
        if (_inputList is { IsW: true, IsReloading: false })
        {
            uniform_rectilinear_motion(ref _position.Y, -100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.MoveRect( 3 * (_animationManager.SourceRect.Height));
            _inputList.IsMoving = true;
        }
        if (_inputList is { IsW: false, IsWold: true })
        {
            _speed.Y = 0;
        }
    }

    private void MoveBack(ref float deltaTime)
    {
        _movementsManager.MoveBack(ref _inputList.IsS);
        if (_inputList is { IsS: true, IsReloading: false })
        {
            uniform_rectilinear_motion(ref _position.Y, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.MoveRect( 0 * (_animationManager.SourceRect.Height));
            _inputList.IsMoving = true;
        }
        
        if (_inputList is { IsS: false, IsSold: true })
        {
            _speed.Y = 0;
        }
    }

    private void MoveRight(ref float deltaTime)
    {
        _movementsManager.MoveRight(ref _inputList.IsD);
        if (_inputList is { IsD: true, IsReloading: false })
        {
            uniform_rectilinear_motion(ref _position.X, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.MoveRect( 2 * (_animationManager.SourceRect.Height));
            _inputList.IsMoving = true;
        }
        if (_inputList is { IsD: false, IsDold: true })
        {
            _speed.X = 0;
        }
        
    }

    private void MoveLeft(ref float deltaTime)
    {
        _movementsManager.MoveLeft(ref _inputList.IsA);
        if (_inputList is { IsA: true, IsReloading: false })
        {
            uniform_rectilinear_motion(ref _position.X, -100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.MoveRect(1 * (_animationManager.SourceRect.Height));
            _inputList.IsMoving = true;
        }
        if (_inputList is { IsA: false, IsAold: true })
        {
            _speed.X = 0;
        }
    }

    private void MoveReload()
    {
        _movementsManager.MoveReload(ref _inputList.IsR);
        _inputList.IsReloading = _inputList.IsR;

        if (_inputList is not { IsR: false, IsRold: true }) return;
        _inputList.IsReloading = false; _reloadTime = 0f;
    }
    
    
    protected override void LoadContent()
    {
        _animationManager.Load_Content(GraphicsDevice);
        CollisionManager.Instance.addObject(_tag, _position.X, _position.Y, _animationManager.Texture.Width, _animationManager.Texture.Height);
    }
    

    public void Draw(SpriteBatch spriteBatch)
    {
        _animationManager.Draw(spriteBatch, ref _position, ref _ammo, ref _inputList.IsReloading, ref _inputList.IsShooting);
    }

    public override void Update(GameTime gameTime)
    {
        _deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
     
        _inputList.IsMoving = false;
        
        MoveBack(ref _deltaTime);
        
        MoveOn(ref _deltaTime);
        
        MoveRight(ref _deltaTime);
        
        MoveLeft(ref _deltaTime);
        
        MoveReload();
        
        normalizeVelocity(ref _speed.X, ref _speed.Y);
        _animationManager.Update(ref _deltaTime, ref _inputList.IsMoving, ref _inputList.IsReloading);
        Reload(_deltaTime);
        //_pressedTime *= 10;
        ChargeShot(_inputList.IsLeft, ref _pressedTime, _deltaTime);
        Shot(_pressedTime);
        CollisionManager.Instance.modifyObject(_tag, _position.X+_animationManager.Texture.Width/6, _position.Y+_animationManager.Texture.Height/6, _animationManager.Texture.Width/3, _animationManager.Texture.Height/3);
        Console.WriteLine("X = "+_position.X+", Y = "+_position.Y);
        _inputList.IsAold = _inputList.IsA;
        _inputList.IsDold = _inputList.IsD;
        _inputList.IsSold = _inputList.IsS;
        _inputList.IsWold = _inputList.IsW;
        _inputList.IsLeftOld = _inputList.IsLeft;
        
    }
}