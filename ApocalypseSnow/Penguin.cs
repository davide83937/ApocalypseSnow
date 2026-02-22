
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
    //private InputList _inputList;
    private StateStruct _stateStruct;
    private ShotStruct _shotStruct;
    private static readonly int FrameReload;
    private int _textureFractionWidth;
    private int _textureFractionHeight;
    private int _halfTextureFractionWidth;
    private int _halfTextureFractionHeight;
    private NetworkManager  _networkManager;
    

    static Penguin()
    {
        FrameReload = 3;
    }
    public Penguin(Game game, Vector2 startPosition, Vector2 startSpeed, IAnimation animation, IMovements movements, NetworkManager networkManager) : base(game)
    {
        _tag = "penguin";
        _gameContext = game;
        _position = startPosition;
        _speed = startSpeed;
        _ammo = 100;
        _animationManager = animation;
        _movementsManager = movements;
        //_inputList = new InputList();
        _stateStruct = new StateStruct();
        _shotStruct = new ShotStruct();
        _countBall = 0;
        _networkManager = new NetworkManager("127.0.0.1", 8080);
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
        if (!_stateStruct.IsPressed(StateList.Reload)) return;
        _reloadTime += gameTime;
        
        if (_reloadTime > FrameReload) 
        {
            _ammo++;
            _reloadTime = 0f;
        }
    }
    
    private void ChargeShot(ref float pressedTime, float deltaTime)
    {
        if (!_stateStruct.IsPressed(StateList.Shoot) || _ammo <= 0) return;
        
        deltaTime *= 100;
        pressedTime += deltaTime;
        
        if (pressedTime > 500)
        {
            pressedTime = 500;
        }
    }

    
 
    
    private void Shot(float pressedTime)
    {
        // 1. Verifichiamo se il tasto di sparo è stato rilasciato in questo frame (JustReleased)
        // 2. Verifichiamo se ci sono munizioni
        if (!_stateStruct.JustReleased(StateList.Shoot) || _ammo <= 0) return;
        
        Vector2 mousePosition = _movementsManager.GetMousePosition();
        _shotStruct.mouseX = (int)mousePosition.X;
        _shotStruct.mouseY = (int)mousePosition.Y;
        
        float differenceX = _position.X - mousePosition.X;
        float differenceY = _position.Y - mousePosition.Y;
    
  
        float coX = (differenceX / 100) * (-1);
        Vector2 startSpeed = new Vector2(coX, differenceY / 100) * pressedTime;
        
        Vector2 finalPosition = FinalPoint(startSpeed, _position);
        
        string tagBall = "Palla" + _countBall;
        Ball b = new Ball(_gameContext, _position, startSpeed, finalPosition, tagBall);
        _gameContext.Components.Add(b);
        //_networkManager.SendShot(_shotStruct);-------------------------------------------------------------------------

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
        
        
        Vector2 pointFinale = new Vector2(impatto.X, impatto.Y);
        return pointFinale;
    }

    
    private void MoveOn(float deltaTime)
    {
        
        if (_stateStruct.IsPressed(StateList.Up) && !_stateStruct.IsPressed(StateList.Reload))
        {
            uniform_rectilinear_motion(ref _position.Y, -100, deltaTime);
            _animationManager.MoveRect(3 * _animationManager.SourceRect.Height);
        }
    
      
        if (_stateStruct.JustReleased(StateList.Moving))
        {
            _speed.Y = 0;
        }
    }
    
  

    private void MoveBack(float deltaTime)
    {
        if (_stateStruct.IsPressed(StateList.Down) && !_stateStruct.IsPressed(StateList.Reload))
        {
            uniform_rectilinear_motion(ref _position.Y, 100, deltaTime);
            _animationManager.MoveRect(0 * _animationManager.SourceRect.Height);
        }
        if (_stateStruct.JustReleased(StateList.Down))
        {
            _speed.Y = 0;
        }
    }
    

    private void MoveRight(float deltaTime)
    {
        if (_stateStruct.IsPressed(StateList.Right) && !_stateStruct.IsPressed(StateList.Reload))
        {
            uniform_rectilinear_motion(ref _position.X, 100, deltaTime);
            _animationManager.MoveRect(2 * _animationManager.SourceRect.Height);
        }
        if (_stateStruct.JustReleased(StateList.Right))
        {
            _speed.X = 0;
        }
    }
    
    
    private void MoveLeft(float deltaTime)
    {
        if (_stateStruct.IsPressed(StateList.Left) && !_stateStruct.IsPressed(StateList.Reload))
        {
            uniform_rectilinear_motion(ref _position.X, -100, deltaTime);
            _animationManager.MoveRect(1 * _animationManager.SourceRect.Height);
        }
    
        if (_stateStruct.JustReleased(StateList.Left))
        {
            _speed.X = 0;
        }
    }
    
    private void MoveReload()
    {
      
        if (_stateStruct.JustReleased(StateList.Reload))
        {
            _reloadTime = 0f;
        }
    }
    
    
    protected override void LoadContent()
    {
        _animationManager.Load_Content(GraphicsDevice);
        CollisionManager.Instance.addObject(_tag, _position.X, _position.Y, _animationManager.Texture.Width, _animationManager.Texture.Height);
        _textureFractionWidth = _animationManager.Texture.Width / 3;
        _textureFractionHeight = _animationManager.Texture.Height / 3;
        _halfTextureFractionWidth = _textureFractionWidth / 2;
        _halfTextureFractionHeight = _textureFractionHeight / 2;
    }
    

    public void Draw(SpriteBatch spriteBatch)
    {
        _animationManager.Draw(
            spriteBatch, 
            ref _position, 
            _ammo, 
            _stateStruct.IsPressed(StateList.Reload), 
            _stateStruct.IsPressed(StateList.Shoot)
        );
    }

    public void OnColliderEnter(object context, CollisionRecordOut collisionRecordOut)
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

            switch (collisionRecordOut._type)
            {
                case 1: //TOP
                    Console.WriteLine("Collisione");
                    if (_speed.Y>0)
                    {
                        
                        _speed.Y = 0;
                        
                    }
                    break;
            }
        }

    }
 
    
    public override void Update(GameTime gameTime)
    {
        _deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        _movementsManager.UpdateInput(ref _stateStruct);
        
        MoveBack(_deltaTime);
        MoveOn(_deltaTime);
        MoveRight(_deltaTime);
        MoveLeft(_deltaTime);
        MoveReload();
        normalizeVelocity(ref _speed.X, ref _speed.Y);

        _animationManager.Update(
            _deltaTime, 
            _stateStruct.IsPressed(StateList.Moving), 
            _stateStruct.IsPressed(StateList.Reload)
        );
        
        Reload(_deltaTime);
        ChargeShot(ref _pressedTime, _deltaTime);
        Shot(_pressedTime);
        
        //_networkManager.SendState(_stateStruct);------------------------------------------------------------------------------------
        
        int posCollX = (int)_position.X + _halfTextureFractionWidth;
        int posCollY = (int)_position.Y + _halfTextureFractionHeight;
        CollisionManager.Instance.modifyObject(_tag, posCollX, posCollY, _textureFractionWidth, _textureFractionHeight);
        
    }
    
}