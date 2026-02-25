namespace ApocalypseSnow;

using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


public class Penguin: CollisionExtensions//, DrawableGameComponent
{
    //public readonly string _tag;
    //private int _countBall;
    private readonly Game _gameContext;
    private readonly IAnimation  _animationManager;
    private readonly IMovements  _movementsManager;

    public PenguinColliderHandler _penguinColliderHandler;
    public PenguinShotHandler _penguinShotHandler;
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
        //_countBall = 0;
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
        //_countBall = 0;
        _penguinColliderHandler = new PenguinColliderHandler(_tag);
        _penguinShotHandler = new PenguinShotHandler(_gameContext, _tag);
        //_networkManager = new NetworkManager("127.0.0.1", 8080);

    }
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void uniform_rectilinear_motion(ref float position, float velocity, float deltaTime);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void normalizeVelocity(ref float velocityX, ref float velocityY);
    
    

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
                _penguinColliderHandler.HandleEggPickup(t, _stateStruct, 
                    ref isWithEgg, ref timeTakingEgg, 
                    _deltaTime, ref _myEgg);
                break;
            case string t when _penguinColliderHandler.IsEnemyBall(t):
                _penguinColliderHandler.HandleHitByBall(ref isWithEgg, ref timeTakingEgg, ref timePuttingEgg, ref isFreezing);
                break;
            case "blueP" or "redP":
                _penguinColliderHandler.HandleEggDelivery(otherTag, ref isWithEgg, ref timePuttingEgg,
                    _stateStruct, _deltaTime, _myEgg);
                break;
            case "obstacle":
                _penguinColliderHandler.HandleObstacleCollision(collisionRecordOut._type, ref _position);
                break;
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
        _penguinColliderHandler.putEgg(_stateStruct, ref isWithEgg);
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
        
        _penguinShotHandler.Reload(_stateStruct, _deltaTime, ref _reloadTime, ref _ammo, FrameReload);
        _penguinShotHandler.ChargeShot(_stateStruct, ref _pressedTime, _deltaTime, _ammo);
        Vector2 MousePosition = _movementsManager.GetMousePosition();
        _shotStruct.mouseX = (int)MousePosition.X;
        _shotStruct.mouseY = (int)MousePosition.Y;
        string tagBall = _animationManager._ballTag;//+ _countBall;
        _penguinShotHandler.Shot(_stateStruct, MousePosition,  
            _position, ref _pressedTime, ref _ammo, tagBall);
        
        //_networkManager.SendState(_stateStruct);------------------------------------------------------------------------------------

        int posCollX = (int)_position.X+ _halfTextureFractionWidth;
        int posCollY = (int)_position.Y+ _halfTextureFractionHeight;
        CollisionManager.Instance.modifyObject(_tag, posCollX, posCollY-60, _textureFractionWidth, _halfTextureFractionHeight);
        
    }
    
}