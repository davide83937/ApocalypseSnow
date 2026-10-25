using System;
using System.Collections.Generic; // Aggiunto per l'uso delle Liste
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class Penguin : CollisionExtensions //, DrawableGameComponent
{
    private readonly Game _gameContext;
    private readonly IMovements _movementsManager;
    public readonly FreezeTimeline LocalFreeze = new();
    public PenguinColliderHandler _penguinColliderHandler;
    public PenguinShotHandler _penguinShotHandler;
    public PenguinInputHandler _penguinInputHandler;

    private float _deltaTime;
    private float _physicsDeltaTime;

    private float _accumulator = 0f;
    
    private ShotStruct _shotStruct;
    private int _textureFractionWidth;
    private int _textureFractionHeight;
    private int _halfTextureFractionWidth;
    private int _halfTextureFractionHeight;
    public string _myEgg = null;

    // --- NUOVA LISTA PER GESTIRE LE COLLISIONI MULTIPLE DELLE UOVA ---
    private List<string> _collidingEggs = new List<string>();
    private string _collidingPlatformTag;


    public Penguin(Game game, string tag, Vector2 startPosition, Vector2 startSpeed,
        IMovements movements, float physicsDeltaTime, uint ownerPlayerId) : base(game, tag, startPosition)
    {
        _gameContext = game;
        _movementsManager = movements;
        _shotStruct = new ShotStruct();
        _penguinColliderHandler = new PenguinColliderHandler(_tag);
        _penguinShotHandler = new PenguinShotHandler(_gameContext, _tag, ownerPlayerId);
        _penguinInputHandler = new PenguinInputHandler(_tag);
        _physicsDeltaTime = physicsDeltaTime;
    }
    

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
            _penguinInputHandler._animationManager.Texture.Width,
            _penguinInputHandler._animationManager.Texture.Height);
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
            _position,
            _penguinShotHandler.Ammo,
            _penguinInputHandler._stateStruct.IsPressed(StateList.Reload),
            _penguinInputHandler._stateStruct.IsPressed(StateList.ShootLeft)|| _penguinInputHandler._stateStruct.IsPressed(StateList.ShootRight),
            _penguinInputHandler._stateStruct.IsPressed(StateList.Freezing),
            // Il remoto usa il possesso realmente ricostruito, non un WithEgg di rete obsoleto.
            _tag.EndsWith("Red")
                ? _penguinColliderHandler.isWithEgg
                : _penguinInputHandler._stateStruct.IsPressed(StateList.WithEgg),
            LocalFreeze.GetVisualOpacity(GameSession.LastSentInputTick)
        );
    }

    // Il server ha confermato il colpo: il pinguino gestisce l'eventuale uovo.
    // La collisione grafica e la prediction restano nel normale gameplay locale.
    public void HandleConfirmedHit()
    {
        // Diagnostica: distingue un HIT confermato dal semplice freeze predetto.
        ShotTrace.Log($"[EGG_HIT] penguin={_tag} tick={GameSession.LocalTick} beforeEgg={_myEgg ?? "none"} beforeWithEgg={_penguinColliderHandler.isWithEgg}");
        _penguinColliderHandler.HandleHitByBall(ref _myEgg);
        ShotTrace.Log($"[EGG_HIT] penguin={_tag} tick={GameSession.LocalTick} afterEgg={_myEgg ?? "none"} afterWithEgg={_penguinColliderHandler.isWithEgg}");
    }

    public void HandleRemoteShot(Vector2 target, int charge, ShotType shotType, ShotKey shotKey)
    {
        _penguinShotHandler.pressedTime = charge/1000f; 

        _penguinShotHandler.Shot(
            _penguinInputHandler._stateStruct,
            target,
            _position,
            _penguinInputHandler._animationManager._ballTag,
            shotType,
            shotKey
        );
    }


    protected override void OnCollisionEnter(string otherTag, CollisionRecordOut collisionRecordOut)
    {
        // La penetrazione con gli ostacoli viene risolta nello stesso tick del movimento,
        // non nell'evento di collisione, che puo' arrivare dopo.
        switch (otherTag)
        {
            case string t when t.StartsWith("egg"):
                // Invece di gestire subito la raccolta, inseriamo l'uovo nella nostra lista
                if (!_collidingEggs.Contains(t))
                {
                    _collidingEggs.Add(t);
                }
                break;
            case string t when _penguinColliderHandler.IsEnemyBall(t):
                Ball ball = _gameContext.Components.OfType<Ball>().FirstOrDefault(b => b._tag == t);
                if (ball != null && ball._currentHeight < 30f)
                {
                    uint hitTick = GameSession.LocalTick;
                    ShotTrace.Log($"[SHOT_TRACE] role={(_tag.EndsWith("Red") ? "REMOTE_TARGET" : "LOCAL_TARGET")} event=PRED_HIT key={ball.ShotKey} tick={hitTick} startTick={ball.ObservedStartTick} flightTicks={hitTick - ball.ObservedStartTick} utc={DateTime.UtcNow:O}");
                    bool predictedFreeze = LocalFreeze.Predict(ball.ShotKey, hitTick);
                    ball.PredictPenguinImpact();
                    if (predictedFreeze)
                        _penguinColliderHandler.isFrozen = true;
                }
                break;
            case "blueP" or "redP":
                if ((_tag == "penguin" && otherTag == "blueP") ||
                    (_tag == "penguinRed" && otherTag == "redP"))
                    _collidingPlatformTag = otherTag;
                break;
        }
    }

    // La geometria della hitbox appartiene al pinguino: prediction e replay
    // usano così le stesse dimensioni, senza duplicare numeri in GameSession.
    public Vector2 ResolveObstacleOverlaps(Vector2 position)
    {
        Vector2 half = new(_halfTextureFractionWidth, _halfTextureFractionHeight);
        Vector2 center = CollisionManager.Instance.ResolveObstacleOverlaps(
            position + half, _textureFractionWidth, _halfTextureFractionHeight);
        return center - half;
    }

    public override void Update(GameTime gameTime)
    {
        _deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _accumulator += _deltaTime;
       
        if (_accumulator >= _physicsDeltaTime)
        {
            _penguinColliderHandler.isFrozen = LocalFreeze.IsFrozen(GameSession.LastSentInputTick);

            _movementsManager.UpdateInput(
                ref _penguinInputHandler._stateStruct,
                _penguinColliderHandler.isFrozen,
                _penguinColliderHandler.isWithEgg,
                _physicsDeltaTime,
                _position);

            // Il remoto usa esclusivamente gli snapshot di posizione del server:
            // non simulare di nuovo gli input gia' applicati dal server.
            if (_tag.EndsWith("Red"))
                _penguinInputHandler.UpdateRemoteFacing();
            else
            {
                // Movimento e risoluzione della penetrazione avvengono nello stesso tick.
                _penguinInputHandler.UpdateMovement(_physicsDeltaTime, ref _position);
                _position = ResolveObstacleOverlaps(_position);
            }


            // --- ESEGUIAMO LA TUA LOGICA CON LA LISTA COMPLETA ---
            _penguinColliderHandler.HandleEggPickup(_collidingEggs, _penguinInputHandler._stateStruct, _physicsDeltaTime, ref _myEgg);
            
            // Svuotiamo la lista. Verrà riempita di nuovo da OnCollisionEnter nel frame successivo
            _collidingEggs.Clear();
            // -----------------------------------------------------

            // Deposito: un solo avanzamento per tick, anche quando si esce dalla piattaforma.
            _penguinColliderHandler.HandleEggDelivery(_collidingPlatformTag,
                ref _penguinInputHandler._stateStruct, _physicsDeltaTime, ref _myEgg);
            _collidingPlatformTag = null;

            resetTakingTimer();
            resetPuttingTimer();

            if (!_tag.EndsWith("Red"))
                _penguinColliderHandler.putEgg(_penguinInputHandler._stateStruct, ref _myEgg);

            _penguinShotHandler.Reload(_penguinInputHandler._stateStruct, _physicsDeltaTime);

            // ChargeShot azzera la carica anche quando la finestra perde il focus.
            if (!_tag.EndsWith("Red"))
            {
                _penguinShotHandler.ChargeShot(_penguinInputHandler._stateStruct, _physicsDeltaTime);
            }

            if (_tag.EndsWith("Red"))
            {
                bool serverFrozen = _movementsManager is MovementsManagerRed remote && remote.IsServerFrozen;
                _penguinColliderHandler.SyncStateWithNetwork(
                    _penguinInputHandler._stateStruct, ref _myEgg, serverFrozen);
            }
            
            if (!_tag.EndsWith("Red") && _gameContext.IsActive)
            {
                KeyboardState _newState = Keyboard.GetState();

                if (_newState.IsKeyDown(Keys.P))
                {
                    PhysicsWrapper.UniformMotion(ref _position.X, 200f, _physicsDeltaTime);
                    //_penguinInputHandler.getMotion(ref _position.X, 200f, _physicsDeltaTime);
                }
                
                Vector2 mousePosition = _movementsManager.GetMousePosition();
                _shotStruct.mouseX = (int)mousePosition.X;
                _shotStruct.mouseY = (int)mousePosition.Y;

                _penguinShotHandler.Shot(_penguinInputHandler._stateStruct, mousePosition,
                    _position, _penguinInputHandler._animationManager._ballTag);
            }

            CollisionManager.Instance.modifyObject(_tag,
                _position.X + _halfTextureFractionWidth,
                _position.Y + _halfTextureFractionHeight,
                _textureFractionWidth, _halfTextureFractionHeight);

            _accumulator -= _physicsDeltaTime;
        }

        _penguinInputHandler._animationManager.Update(
            _deltaTime,
            _penguinInputHandler._stateStruct.IsPressed(StateList.Moving),
            _penguinInputHandler._stateStruct.IsPressed(StateList.Reload)
        );
    }
}