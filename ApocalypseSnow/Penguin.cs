namespace ApocalypseSnow;

using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


public class Penguin: CollisionExtensions//, DrawableGameComponent
{
    private readonly Game _gameContext;
    //private readonly IAnimation  _animationManager;
    private readonly IMovements  _movementsManager;
    public PenguinColliderHandler _penguinColliderHandler;
    public PenguinShotHandler _penguinShotHandler;
    public PenguinInputHandler _penguinInputHandler;
    private Vector2 _speed;
    private float _deltaTime;
    //private float _reloadTime;
    //private StateStruct _stateStruct;
    private ShotStruct _shotStruct;
    private int _textureFractionWidth;
    private int _textureFractionHeight;
    private int _halfTextureFractionWidth;
    private int _halfTextureFractionHeight;
    private NetworkManager  _networkManager;

    public string _myEgg;
    
    
    public Penguin(Game game, string tag, Vector2 startPosition, Vector2 startSpeed,
        IMovements movements, NetworkManager networkManager) : base(game, tag, startPosition)
    {
        _gameContext = game;
        _speed = startSpeed;
        //_animationManager = animation;
        _movementsManager = movements;
        //_stateStruct = new StateStruct();
        _shotStruct = new ShotStruct();
        _penguinColliderHandler = new PenguinColliderHandler(_tag);
        _penguinShotHandler = new PenguinShotHandler(_gameContext, _tag);
        _penguinInputHandler = new PenguinInputHandler(_tag);
        _networkManager = networkManager;
    }
    
    public Penguin(Game game, string tag, Vector2 startPosition, Vector2 startSpeed, 
        IMovements movements) : base(game, tag, startPosition)
    {
        _gameContext = game;
        _speed = startSpeed;
        //_animationManager = animation;
        _movementsManager = movements;
        //_stateStruct = new StateStruct();
        _shotStruct = new ShotStruct();
        _penguinColliderHandler = new PenguinColliderHandler(_tag);
        _penguinShotHandler = new PenguinShotHandler(_gameContext, _tag);
        _penguinInputHandler = new PenguinInputHandler(_tag);
        //_networkManager = new NetworkManager("127.0.0.1", 8080);

    }
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void uniform_rectilinear_motion(ref float position, float velocity, float deltaTime);
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void normalizeVelocity(ref float velocityX, ref float velocityY);
    
    

    private void resetTakingTimer()
    {
        if (_penguinInputHandler.isTakingEggJustReleased())
        {
            _penguinColliderHandler.resetTakingTimer();
        }
    }
    
    private void resetPuttingTimer()
    {
        if (_penguinInputHandler.isPuttingEggJustReleased())
        {
            _penguinColliderHandler.resetPuttingTimer();
        }
    }
    
    
    protected override void LoadContent()
    {
        _penguinInputHandler._animationManager.Load_Content(GraphicsDevice);
        CollisionManager.Instance.addObject(_tag, _position.X, _position.Y, 
            _penguinInputHandler._animationManager.Texture.Width, _penguinInputHandler._animationManager.Texture.Height);
        //CollisionManager.Instance.sendCollisionEvent += OnColliderEnter;
        base.LoadContent();
        _textureFractionWidth = _penguinInputHandler._animationManager.Texture.Width / 3;
        _textureFractionHeight = _penguinInputHandler._animationManager.Texture.Height / 3;
        _halfTextureFractionWidth = _textureFractionWidth / 2;
        _halfTextureFractionHeight = _textureFractionHeight / 2;
    }



    public void Draw(SpriteBatch spriteBatch)
    {
        _penguinInputHandler._animationManager.Draw(
            spriteBatch, 
            ref _position, 
            _penguinShotHandler.Ammo, 
            _penguinInputHandler._stateStruct.IsPressed(StateList.Reload), 
            _penguinInputHandler._stateStruct.IsPressed(StateList.Shoot),
            _penguinInputHandler._stateStruct.IsPressed(StateList.Freezing),
            _penguinInputHandler._stateStruct.IsPressed(StateList.WithEgg)
        );
    }
    
   
    
    protected override void OnCollisionEnter(string otherTag, CollisionRecordOut collisionRecordOut)
    {
        //if (!collisionRecordOut.Involves(_tag)) return;
       // string otherTag = collisionRecordOut.GetOtherTag(_tag);

        switch (otherTag)
        {
            case string t when t.StartsWith("egg"):
                _penguinColliderHandler.HandleEggPickup(t, _penguinInputHandler._stateStruct, 
                     
                    _deltaTime, ref _myEgg);
                break;
            case string t when _penguinColliderHandler.IsEnemyBall(t):
                _penguinColliderHandler.HandleHitByBall();
                break;
            case "blueP" or "redP":
                _penguinColliderHandler.HandleEggDelivery(otherTag,
                    _penguinInputHandler._stateStruct, _deltaTime, _myEgg);
                break;
            case "obstacle":
                _penguinColliderHandler.HandleObstacleCollision(collisionRecordOut._type, ref _position);
                break;
        }
    }
    
    public override void Update(GameTime gameTime)
    {
        _deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
       
        _movementsManager.UpdateInput(ref _penguinInputHandler._stateStruct, 
            _penguinColliderHandler.isFrozen, _penguinColliderHandler.isWithEgg, _deltaTime);
    
        _penguinInputHandler.UpdatePositionX(_deltaTime, ref _position.X);
        _penguinInputHandler.UpdatePositionY(_deltaTime, ref _position.Y);
            // 2. Ricevi gli aggiornamenti dal server
        _networkManager.SendState(_penguinInputHandler._stateStruct, _deltaTime, _position);
        
        //_penguinInputHandler.UpdatePositionX(_deltaTime, ref _position.X);
        //_penguinInputHandler.UpdatePositionY(_deltaTime, ref _position.Y);
        _penguinInputHandler.MoveReload(ref _penguinShotHandler._reloadTime);
        normalizeVelocity(ref _speed.X, ref _speed.Y);
        _penguinColliderHandler.putEgg(_penguinInputHandler._stateStruct);
        resetTakingTimer();
        resetPuttingTimer();

        //Console.WriteLine(_speed.X);
        //Console.WriteLine(_speed.Y);
        _penguinInputHandler.increaseTimeFreezing(_deltaTime, ref _penguinColliderHandler.isFrozen);
        
        _penguinInputHandler._animationManager.Update(
            _deltaTime, 
            _penguinInputHandler._stateStruct.IsPressed(StateList.Moving), 
            _penguinInputHandler._stateStruct.IsPressed(StateList.Reload),
            _penguinInputHandler._stateStruct.IsPressed(StateList.WithEgg)
        );
        
        _penguinShotHandler.Reload(_penguinInputHandler._stateStruct, _deltaTime);
        _penguinShotHandler.ChargeShot(_penguinInputHandler._stateStruct, _deltaTime);
        Vector2 mousePosition = _movementsManager.GetMousePosition();
        _shotStruct.mouseX = (int)mousePosition.X;
        _shotStruct.mouseY = (int)mousePosition.Y;
        _penguinShotHandler.Shot(_penguinInputHandler._stateStruct, mousePosition,  
            _position, _penguinInputHandler._animationManager._ballTag);
        
        //_networkManager.SendState(_movementsManager.State);------------------------------------------------------------------------------------

        int posCollX = (int)_position.X+ _halfTextureFractionWidth;
        int posCollY = (int)_position.Y+ _halfTextureFractionHeight;
        CollisionManager.Instance.modifyObject(_tag, posCollX, posCollY-60, _textureFractionWidth, _halfTextureFractionHeight);
    }
    
}