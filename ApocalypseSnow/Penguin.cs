
using System;
using System.Runtime.InteropServices;

namespace ApocalypseSnow;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


public class Penguin: CollisionExtensions//, DrawableGameComponent
{
    //public readonly string _tag;
    private int _countBall;
    private readonly Game _gameContext;
    private readonly IAnimation  _animationManager;
    private readonly IMovements  _movementsManager;
    //public Vector2 _position;
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
    private bool isFreezing = false;
    private bool isWithEgg = false;
    private float timeFreezing = 0;
    private float timeTakingEgg = 0;
    private float timePuttingEgg = 0;
    public string _myEgg;
    public event EventHandler<string> eggTakenEvent;
    public event EventHandler<string> eggDeleteEvent;
    public event EventHandler eggPutEvent;
    
    static Penguin()
    {
        FrameReload = 3;
    }
    public Penguin(Game game, string tag, Vector2 startPosition, Vector2 startSpeed, IAnimation animation,
        IMovements movements, NetworkManager networkManager) : base(game, tag, startPosition)
    {
        //_tag = tag;
        _gameContext = game;
        //_position = startPosition;
        _speed = startSpeed;
        _ammo = 100;
        _animationManager = animation;
        _movementsManager = movements;
        //_inputList = new InputList();
        _stateStruct = new StateStruct();
        _shotStruct = new ShotStruct();
        _countBall = 0;
        _networkManager = new NetworkManager("127.0.0.1", 8080);
        this.DrawOrder = 100;
        
    }
    
    public Penguin(Game game, string tag, Vector2 startPosition, Vector2 startSpeed, IAnimation animation, 
        IMovements movements) : base(game, tag, startPosition)
    {
        //_tag = tag;
        _gameContext = game;
        //_position = startPosition;
        _speed = startSpeed;
        _ammo = 100;
        _animationManager = animation;
        _movementsManager = movements;
        //_inputList = new InputList();
        _stateStruct = new StateStruct();
        _shotStruct = new ShotStruct();
        _countBall = 0;
        //_networkManager = new NetworkManager("127.0.0.1", 8080);
        
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
        
        deltaTime *= 100000;
        pressedTime += deltaTime;
        
        if (pressedTime > 200000)
        {
            pressedTime = 200000;
        }
        //Console.WriteLine(pressedTime);
     
    }
 
    
    private void Shot(float pressedTime)
    {
        // 1. Verifichiamo se il tasto di sparo è stato rilasciato in questo frame (JustReleased)
        // 2. Verifichiamo se ci sono munizioni
        if (!_stateStruct.JustReleased(StateList.Shoot) || _ammo <= 0) return;
        
        Vector2 mousePosition = _movementsManager.GetMousePosition();
        _shotStruct.mouseX = (int)mousePosition.X;
        _shotStruct.mouseY = (int)mousePosition.Y;
        
        float differenceX = _position.X+48 - mousePosition.X;
        float differenceY = _position.Y - mousePosition.Y;
        
        normalizeVelocity(ref differenceX, ref differenceY);
        
        float coX = (differenceX / 150) * (-1);
        Vector2 startSpeed = new Vector2(coX, differenceY / 100) * pressedTime;
        
        Vector2 finalPosition = FinalPoint(startSpeed, _position);
        
        string tagBall =_animationManager._ballTag+ _countBall;
        Ball b = new Ball(_gameContext, _tag,_position, startSpeed, finalPosition, tagBall);
        _gameContext.Components.Add(b);
        //_networkManager.SendShot(_shotStruct);-------------------------------------------------------------------------

        _pressedTime = 0;
        _ammo--;
        _countBall++;
    }

    protected virtual void eggTakenEventFunction(string tagEgg)
    {
        eggTakenEvent?.Invoke(this, tagEgg);
    }
    
    private void putEgg()
    {
        if ((_stateStruct.IsPressed(StateList.WithEgg)&&_stateStruct.JustPressed(StateList.TakingEgg))
            || (_stateStruct.JustReleased(StateList.WithEgg)&&_stateStruct.JustPressed(StateList.Freezing)))
        {
            eggPutEvent?.Invoke(this, EventArgs.Empty);
            isWithEgg = false;
        }
    }

    private void deleteEgg(string tagEgg)
    {
        eggDeleteEvent?.Invoke(this, tagEgg);
    }

    private void resetTakingTimer()
    {
        if (_stateStruct.JustReleased(StateList.TakingEgg))
        {
            timeTakingEgg = 0;
        }
    }
    private void resetPuttingTimer()
    {
        if (_stateStruct.JustReleased(StateList.PuttingEgg))
        {
            timePuttingEgg = 0;
        }
    }
    
    private Vector2 FinalPoint(Vector2 startSpeed, Vector2 startPosition)
    {
        parabolic_motion(100f,
            startPosition.X + 48, 
            startPosition.Y, 
            out float x, out float y,
            startSpeed.X, 
            -startSpeed.Y, 
            0.5f // Il "tempo" finale desiderato
        );
        
        Vector2 pointFinale = new Vector2(x, y);
        return pointFinale;
    }

    
    
    private void MoveOn(float deltaTime)
    {
        if (_stateStruct.IsPressed(StateList.Up) && !_stateStruct.IsPressed(StateList.Reload) && !_stateStruct.IsPressed(StateList.Freezing))
        {
            _speed.Y = 100;
            uniform_rectilinear_motion(ref _position.Y, -_speed.Y, deltaTime);
            _animationManager.MoveRect(3 * _animationManager.SourceRect.Height);
        }
    }
    
  

    private void MoveBack(float deltaTime)
    {
        if (_stateStruct.IsPressed(StateList.Down) && !_stateStruct.IsPressed(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            _speed.Y = 100;
            uniform_rectilinear_motion(ref _position.Y, _speed.Y, deltaTime);
            _animationManager.MoveRect(0 * _animationManager.SourceRect.Height);
        }
    }
    

    private void MoveRight(float deltaTime)
    {
        if (_stateStruct.IsPressed(StateList.Right) && !_stateStruct.IsPressed(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            _speed.X = 100;
            uniform_rectilinear_motion(ref _position.X, _speed.X, deltaTime);
            _animationManager.MoveRect(2 * _animationManager.SourceRect.Height);
        }
    }
    
    
    private void MoveLeft(float deltaTime)
    {
        if (_stateStruct.IsPressed(StateList.Left) && !_stateStruct.IsPressed(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            _speed.X = 100;
            uniform_rectilinear_motion(ref _position.X, -_speed.X, deltaTime);
            _animationManager.MoveRect(1 * _animationManager.SourceRect.Height);
        }
    }
    
    private void MoveReload()
    {
        if (_stateStruct.JustReleased(StateList.Reload)&& !_stateStruct.IsPressed(StateList.Freezing))
        {
            _reloadTime = 0f;
        }
    }
    
    protected override void LoadContent()
    {
        _animationManager.Load_Content(GraphicsDevice);
        CollisionManager.Instance.addObject(_tag, _position.X, _position.Y, _animationManager.Texture.Width, _animationManager.Texture.Height);
        //CollisionManager.Instance.sendCollisionEvent += OnColliderEnter;
        base.LoadContent();
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
            _stateStruct.IsPressed(StateList.Shoot),
            _stateStruct.IsPressed(StateList.Freezing),
            _stateStruct.IsPressed(StateList.WithEgg)
        );
    }
    
   
    
    protected override void OnCollisionEnter(string otherTag, CollisionRecordOut collisionRecordOut)
    {
        //if (!collisionRecordOut.Involves(_tag)) return;
       // string otherTag = collisionRecordOut.GetOtherTag(_tag);

        switch (otherTag)
        {
            case string t when t.StartsWith("egg"):
                HandleEggPickup(t);
                break;
            case string t when IsEnemyBall(t):
                HandleHitByBall();
                break;
            case "blueP" or "redP":
                HandleEggDelivery(otherTag);
                break;
            case "obstacle":
                HandleObstacleCollision(collisionRecordOut._type);
                break;
        }
    }
 
    
    private void HandleEggPickup(string eggTag)
    {
        if (_stateStruct.IsPressed(StateList.TakingEgg) && !_stateStruct.IsPressed(StateList.WithEgg))
        {
            timeTakingEgg += _deltaTime;
            //Console.WriteLine(t);
            if (timeTakingEgg > 1)
            {
                isWithEgg = true;
                timeTakingEgg = 0;
                _myEgg = eggTag;
                eggTakenEventFunction(eggTag);
            }
        }
    }
 
    private void HandleHitByBall()
    {
        isFreezing = true;
        timeTakingEgg = 0;
        timePuttingEgg = 0;
        isWithEgg = false;
    }
    
    private bool IsEnemyBall(string otherTag)
    {
        // Determina se la palla colpita è nemica in base al tag del pinguino corrente
        return (_tag == "penguinRed" && otherTag.StartsWith("Ball")) ||
               (_tag == "penguin" && otherTag.StartsWith("RedBall"));
    }
    
    private void HandleEggDelivery(string platformTag)
    {
        // Verifica se il pinguino sta consegnando l'uovo alla piattaforma corretta
        bool isCorrectPlatform = (_tag == "penguin" && platformTag == "blueP") || 
                                 (_tag == "penguinRed" && platformTag == "redP");

        if (isCorrectPlatform && _stateStruct.IsPressed(StateList.WithEgg) && _stateStruct.IsPressed(StateList.PuttingEgg))
        {
            timePuttingEgg += _deltaTime;
            Console.WriteLine(timePuttingEgg);
            if (timePuttingEgg > 1)
            {
                deleteEgg(_myEgg);
                timePuttingEgg = 0;
                isWithEgg = false;
            }
        }
    }
    
    private void HandleObstacleCollision(int collisionType)
    {
        const float bounceDistance = 5f;
        switch (collisionType)
        {
            case 1: _position.Y -= bounceDistance; break; // TOP
            case 2: _position.Y += bounceDistance; break; // BOTTOM
            case 3: _position.X += bounceDistance; break; // LEFT
            case 4: _position.X -= bounceDistance; break; // RIGHT
        }
    }
    
    public override void Update(GameTime gameTime)
    {
        _deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        _movementsManager.UpdateInput(ref _stateStruct, isFreezing, isWithEgg);
        
        MoveBack(_deltaTime);
        MoveOn(_deltaTime);
        MoveRight(_deltaTime);
        MoveLeft(_deltaTime);
        MoveReload();
        normalizeVelocity(ref _speed.X, ref _speed.Y);
        putEgg();
        resetTakingTimer();
        resetPuttingTimer();

        //Console.WriteLine(_speed.X);
        //Console.WriteLine(_speed.Y);
        if (isFreezing)
        {
            timeFreezing += _deltaTime;
            if (timeFreezing >= 3)
            {isFreezing = false; timeFreezing = 0;}
        }
        
        _animationManager.Update(
            _deltaTime, 
            _stateStruct.IsPressed(StateList.Moving), 
            _stateStruct.IsPressed(StateList.Reload),
            _stateStruct.IsPressed(StateList.WithEgg)
        );
        
        Reload(_deltaTime);
        ChargeShot(ref _pressedTime, _deltaTime);
        Shot(_pressedTime);
        
        //_networkManager.SendState(_stateStruct);------------------------------------------------------------------------------------

        int posCollX = (int)_position.X+ _halfTextureFractionWidth;
        int posCollY = (int)_position.Y+ _halfTextureFractionHeight;
        CollisionManager.Instance.modifyObject(_tag, posCollX, posCollY-60, _textureFractionWidth, _halfTextureFractionHeight);
        
    }
    
}