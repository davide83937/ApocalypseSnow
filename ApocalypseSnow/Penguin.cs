
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
    private StateStruct _stateList;
    private static readonly int FrameReload;
    private int _textureFractionWidth;
    private int _textureFractionHeight;
    private int _halfTextureFractionWidth;
    private int _halfTextureFractionHeight;
    

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
        //_inputList = new InputList();
        _stateList = new StateStruct();
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
        // Verifichiamo se il tasto ricarica è attualmente premuto
        if (!_stateList.IsPressed(StateList.Reload)) return;

        _reloadTime += gameTime;
    
        // Se il tempo di ricarica ha superato la soglia dei frame stabiliti
        if (_reloadTime > FrameReload) 
        {
            _ammo++;
            _reloadTime = 0f;
        }
    }

    // Abbiamo rimosso 'bool isLeft' perché ora lo leggiamo dall'enum
    private void ChargeShot(ref float pressedTime, float deltaTime)
    {
        // Verifichiamo se il tasto di sparo (Mouse Sinistro) è premuto e se c'è munizione
        if (!_stateList.IsPressed(StateList.Shoot) || _ammo <= 0) return;

        // Incrementiamo il tempo di caricamento del colpo
        deltaTime *= 100;
        pressedTime += deltaTime;

        // Limite massimo di potenza del colpo
        if (pressedTime > 500)
        {
            pressedTime = 500;
        }
    }

    
    /*private void Reload(float gameTime)
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
        _inputList.IsShooting = true;
        deltaTime *= 100;
        pressedTime += deltaTime;
        if (pressedTime > 500)
        {
            pressedTime = 500;
        }
    }*/

    /*private void Shot( float pressedTime)
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
        string tagBall = "Palla" + _countBall;
        Ball b = new Ball(_gameContext, _position, startSpeed, finalPosition, tagBall);
        _gameContext.Components.Add(b);
        _inputList.IsShooting = false;
        //Console.WriteLine($"Valore: {_pressedTime}");
        _pressedTime = 0;
        _ammo--;
        _countBall++;
    }*/
    
    private void Shot(float pressedTime)
    {
        // 1. Verifichiamo se il tasto di sparo è stato rilasciato in questo frame (JustReleased)
        // 2. Verifichiamo se ci sono munizioni
        if (!_stateList.JustReleased(StateList.Shoot) || _ammo <= 0) return;

        // Il calcolo della traiettoria rimane lo stesso, ma usiamo nomi più chiari
        Vector2 mousePosition = _movementsManager.GetMousePosition();
        float differenceX = _position.X - mousePosition.X;
        float differenceY = _position.Y - mousePosition.Y;
    
        // Calcolo velocità iniziale basato sul tempo di pressione (caricamento)
        float coX = (differenceX / 100) * (-1);
        Vector2 startSpeed = new Vector2(coX, differenceY / 100) * pressedTime;
    
        // Calcolo punto di impatto finale
        Vector2 finalPosition = FinalPoint(startSpeed, _position);
    
        // Creazione della palla e aggiunta ai componenti del gioco
        string tagBall = "Palla" + _countBall;
        Ball b = new Ball(_gameContext, _position, startSpeed, finalPosition, tagBall);
        _gameContext.Components.Add(b);

        // Reset degli stati: ora che il colpo è partito, azzeriamo il caricamento
        _pressedTime = 0;
        _ammo--;
        _countBall++;
    
        // Nota: IsShooting nella tua vecchia struct era un booleano. 
        // Se lo usi per l'animazione, ora puoi semplicemente controllare 
        // se InputAction.Shoot è premuto nel metodo Draw o Update.
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

    
    private void MoveOn(float deltaTime)
    {
        // Sostituisce _inputList.IsW
        if (_stateList.IsPressed(StateList.Up) && !_stateList.IsPressed(StateList.Reload))
        {
            uniform_rectilinear_motion(ref _position.Y, -100, deltaTime);
            _animationManager.MoveRect(3 * _animationManager.SourceRect.Height);
        }
    
        // Gestione rilascio tasto (Sostituisce IsWold)
        if (_stateList.JustReleased(StateList.Moving))
        {
            _speed.Y = 0;
        }
    }
    
    /*private void MoveOn(ref float deltaTime)
    {
        _movementsManager.moveOn(ref _inputList.IsW);
        if (_inputList is { IsW: true, IsReloading: false })
        {
            uniform_rectilinear_motion(ref _position.Y, -100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.MoveRect( 3 * (_animationManager.SourceRect.Height));
            _inputList.IsMoving = true;
        }
        if (_inputList is { IsW: false*//*, IsWold: true *//*})
        {
            _speed.Y = 0;
        }
    }*/

    private void MoveBack(float deltaTime)
    {
        // Sostituisce _inputList.IsS
        if (_stateList.IsPressed(StateList.Down) && !_stateList.IsPressed(StateList.Reload))
        {
            uniform_rectilinear_motion(ref _position.Y, 100, deltaTime);
            _animationManager.MoveRect(0 * _animationManager.SourceRect.Height);
        }
    
        if (_stateList.JustReleased(StateList.Down))
        {
            _speed.Y = 0;
        }
    }
    
    /*private void MoveBack(ref float deltaTime)
    {
        _movementsManager.MoveBack(ref _inputList.IsS);
        if (_inputList is { IsS: true, IsReloading: false })
        {
            uniform_rectilinear_motion(ref _position.Y, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.MoveRect( 0 * (_animationManager.SourceRect.Height));
            _inputList.IsMoving = true;
        }
        
        if (_inputList is { IsS: false*//*, IsSold: true*//*})
        {
            _speed.Y = 0;
        }
    }*/

    private void MoveRight(float deltaTime)
    {
        // Sostituisce _inputList.IsD
        if (_stateList.IsPressed(StateList.Right) && !_stateList.IsPressed(StateList.Reload))
        {
            uniform_rectilinear_motion(ref _position.X, 100, deltaTime);
            _animationManager.MoveRect(2 * _animationManager.SourceRect.Height);
        }
    
        if (_stateList.JustReleased(StateList.Right))
        {
            _speed.X = 0;
        }
    }
    
    /*private void MoveRight(ref float deltaTime)
    {
        _movementsManager.MoveRight(ref _inputList.IsD);
        if (_inputList is { IsD: true, IsReloading: false })
        {
            uniform_rectilinear_motion(ref _position.X, 100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.MoveRect( 2 * (_animationManager.SourceRect.Height));
            _inputList.IsMoving = true;
        }
        if (_inputList is { IsD: false*//*, IsDold: true*//*})
        {
            _speed.X = 0;
        }
        
    }*/

    /*private void MoveLeft(ref float deltaTime)
    {
        _movementsManager.MoveLeft(ref _inputList.IsA);
        if (_inputList is { IsA: true, IsReloading: false })
        {
            uniform_rectilinear_motion(ref _position.X, -100, deltaTime);
            //_sourceRect.X = 1 * (_texture.Width / 3);
            _animationManager.MoveRect(1 * (_animationManager.SourceRect.Height));
            _inputList.IsMoving = true;
        }
        if (_inputList is { IsA: false*//*, IsAold: true *//*})
        {
            _speed.X = 0;
        }
    }*/
    
    private void MoveLeft(float deltaTime)
    {
        // Sostituisce _inputList.IsA
        if (_stateList.IsPressed(StateList.Left) && !_stateList.IsPressed(StateList.Reload))
        {
            uniform_rectilinear_motion(ref _position.X, -100, deltaTime);
            _animationManager.MoveRect(1 * _animationManager.SourceRect.Height);
        }
    
        if (_stateList.JustReleased(StateList.Left))
        {
            _speed.X = 0;
        }
    }

    /*private void MoveReload()
    {
        _movementsManager.MoveReload(ref _inputList.IsR);
        _inputList.IsReloading = _inputList.IsR;

        if (_inputList is not { IsR: false*//*, IsRold: true *//*}) return;
        _inputList.IsReloading = false; _reloadTime = 0f;
    }*/
    
    private void MoveReload()
    {
        // Non serve più _movementsManager.MoveReload(ref _inputList.IsR)
        // Il flag di ricarica è già presente in _inputList.Current grazie a UpdateInput
    
        // Se il tasto Ricarica è stato appena rilasciato, resettiamo il timer
        if (_stateList.JustReleased(StateList.Reload))
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
        // Passiamo direttamente i controlli dei flag dell'enum
        _animationManager.Draw(
            spriteBatch, 
            ref _position, 
            _ammo, 
            _stateList.IsPressed(StateList.Reload), 
            _stateList.IsPressed(StateList.Shoot)
        );
    }
    /*public override void Update(GameTime gameTime)
    {
        _deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
     
        //_inputList.IsMoving = false;
        
        
        //MoveBack(ref _deltaTime);
        //MoveOn(ref _deltaTime);
        //MoveRight(ref _deltaTime);
        //MoveLeft(ref _deltaTime);
        //MoveReload();
        
        normalizeVelocity(ref _speed.X, ref _speed.Y);
        _animationManager.Update(ref _deltaTime, ref _inputList.IsMoving, ref _inputList.IsReloading);
        Reload(_deltaTime);
        //_pressedTime *= 10;
        ChargeShot(_inputList.IsLeft, ref _pressedTime, _deltaTime);
        Shot(_pressedTime);
        //int textureFractionWidth = _animationManager.Texture.Width / 3;
        //int textureFractionHeight = _animationManager.Texture.Height / 3;
        int posCollX = (int)_position.X + _halfTextureFractionWidth;
        int posCollY = (int)_position.Y + _halfTextureFractionHeight;
        CollisionManager.Instance.modifyObject(_tag, posCollX, posCollY, _textureFractionWidth, _textureFractionHeight);
        //Console.WriteLine("X = "+_position.X+", Y = "+_position.Y);
        //_inputList.IsAold = _inputList.IsA;
        //_inputList.IsDold = _inputList.IsD;
        //_inputList.IsSold = _inputList.IsS;
        //_inputList.IsWold = _inputList.IsW;
        _inputList.IsLeftOld = _inputList.IsLeft;
        
    }*/
    
    public override void Update(GameTime gameTime)
    {
        // 1. Calcolo del tempo trascorso
        _deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // 2. AGGIORNAMENTO INPUT: Il manager riempie _inputList.Current
        // (Presuppone che tu abbia aggiunto UpdateInput al MovementsManager)
        _movementsManager.UpdateInput(ref _stateList);

        // 3. LOGICA DI MOVIMENTO
        // Nota: i metodi ora leggono direttamente da _inputList
        MoveBack(_deltaTime);
        MoveOn(_deltaTime);
        MoveRight(_deltaTime);
        MoveLeft(_deltaTime);
    
        // 4. LOGICA DI RICARICA E FISICA
        MoveReload();
        normalizeVelocity(ref _speed.X, ref _speed.Y);

        // 5. ANIMAZIONE
        // Usiamo IsPressed per sapere se il pinguino si sta muovendo o ricaricando
        bool isMoving = _stateList.IsPressed(StateList.Moving);
        bool isReloading = _stateList.IsPressed(StateList.Reload);
        _animationManager.Update(
            _deltaTime, 
            _stateList.IsPressed(StateList.Moving), 
            _stateList.IsPressed(StateList.Reload)
        );

        // 6. AZIONE DI SPARO
        Reload(_deltaTime);
        ChargeShot(ref _pressedTime, _deltaTime);
    
        // Shot viene eseguito solo nel frame in cui il tasto viene rilasciato
        Shot(_pressedTime);

        // 7. AGGIORNAMENTO COLLISIONI
        int posCollX = (int)_position.X + _halfTextureFractionWidth;
        int posCollY = (int)_position.Y + _halfTextureFractionHeight;
        CollisionManager.Instance.modifyObject(_tag, posCollX, posCollY, _textureFractionWidth, _textureFractionHeight);
    
        // NOTA: Non serve più assegnare IsWold = IsW, ecc. 
        // perché il prossimo UpdateInput chiamerà BeginUpdate()
    }
    
}