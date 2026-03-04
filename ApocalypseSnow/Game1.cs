using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Concurrent;
using System.Linq;

namespace ApocalypseSnow;

public class Game1: Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    //private Penguin _myPenguin;
    //private Penguin _redPenguin;
    //private Obstacle _obstacle;
    //private Obstacle _obstacle1;
    //private BasePlatform _bluePlatform;
    //private BasePlatform _redPlatform;
    private GameSession gameSession;
    private SpriteFont _uiFont;
    private int _width;
    private int _height;
    private Texture2D _backgroundTexture;
    private NetworkManager networkManager;
    private Reconciler _reconciler;
    //private float NetDt = 0;
    
    //private readonly ConcurrentQueue<JoinSnapshot> _joinQueue = new();
    //private readonly ConcurrentQueue<AuthSnapshot> _authQueue = new();
    //private readonly ConcurrentQueue<RemoteSnapshot> _remoteStateQueue = new();
    //private readonly ConcurrentQueue<ShotStruct> _shotQueue = new();
    //private EggsEvent _eggsEvent;
    //private Events _events;
    
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        _width = 0;
        _height = 0;
        IsMouseVisible = true;
    }
    
    protected override void Initialize()
    {
        //CONNESSIONE ------------------------------------------------------
        networkManager = new NetworkManager(this, "127.0.0.1", 8080);
        //networkManager = new NetworkManager(this, "192.168.1.27", 8080);
        //networkManager = new NetworkManager(this, "7.tcp.eu.ngrok.io", 13297);
        //networkManager = new NetworkManager(this, "3.125.188.168", 13297);
        //networkManager = new NetworkManager(this, "18.192.31.30", 11179);//
        
       // string playerName = "Davide";
        //JoinStruct joinStruct = new JoinStruct(playerName);
        //networkManager.SendJoin(joinStruct);
        // 2. Attendi la risposta (il gioco si fermerà qui finché non arriva il secondo player)
        //JoinAckStruct ack = networkManager.WaitForJoinAck();
        //Console.WriteLine("--- Connessione Stabilita ---");
        //Console.WriteLine($"Messaggio Tipo: {ack.Type}");
        //Console.WriteLine($"ID Giocatore assegnato: {ack.PlayerId}");
        //Console.WriteLine($"Posizione di Spawn: X={ack.SpawnX}, Y={ack.SpawnY}");
        //Console.WriteLine($"Posizione di Spawn: X={ack.OpponentSpawnX}, Y={ack.OpponentSpawnY}");
        //Console.WriteLine("-----------------------------");
        
        _width = GraphicsDevice.Viewport.Width;
        _height = GraphicsDevice.Viewport.Height;
        
        _reconciler  = new Reconciler(this);
       
        //IMovements movements = new MovementsManager(this);
        //IMovements movementsRed = new MovementsManagerRed();
        CollisionManager collisionManager = new CollisionManager(this);
        
        //string bluePathPlatform = "Content/images/green_logo.png";
        //string redPathPlatform = "Content/images/red_logo.png";
        //NetDt = 1f / (float)ack.Heartz;
        //Console.WriteLine($"Delta time: {NetDt}");
        //_bluePlatform = new BasePlatform(this, new Vector2(ack.SpawnX, ack.SpawnY), "blueP", bluePathPlatform);
        //_redPlatform =  new BasePlatform(this, new Vector2(ack.OpponentSpawnX, ack.OpponentSpawnY), "redP", redPathPlatform);
        //_myPenguin = new Penguin(this,"penguin", _bluePlatform._position, Vector2.Zero, movements, NetDt);
        //_redPenguin = new Penguin(this,"penguinRed", _redPlatform._position, Vector2.Zero, movementsRed, NetDt);
        //_obstacle = new Obstacle(this, new Vector2(100, 100), 1, 1);
        //_obstacle1 = new Obstacle(this, new Vector2(100, 50), 1, 1);
        //_eggsEvent = new EggsEvent(this, _myPenguin, _redPenguin);
        //_events = new Events(this, _redPenguin);
       
        //Components.Add(_myPenguin);
        //Components.Add(_redPenguin);
        //Components.Add(_bluePlatform);
        //Components.Add(_redPlatform);
        //Components.Add(_obstacle);
        Components.Add(collisionManager);
        //Components.Add(_eggsEvent);
        //Components.Add(_events);
        gameSession = new GameSession(this);
        
        base.Initialize();
    }
    
    
   
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _uiFont = Content.Load<SpriteFont>("UIAmmo");
        load_texture("Content/images/environment.png");
        base.LoadContent();
    }
    
    
    public void load_texture(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
        this._backgroundTexture = Texture2D.FromStream(GraphicsDevice, stream);
    }

    public void DrawComponentsOfType<T>(IEnumerable<T> allComponents)
    {
        foreach (var component in allComponents.OfType<T>())
        {
            ((dynamic)component).Draw(_spriteBatch);
        }
    }

    private void drawUI()
    {
        if (gameSession._myPenguin != null && gameSession._redPenguin != null)
        {
            // Testo Munizioni (in basso a sinistra come lo avevi)
            string ammoText = $"Munizioni: {gameSession._myPenguin._penguinShotHandler.Ammo}";
            _spriteBatch.DrawString(_uiFont, ammoText, new Vector2(_width / 10f, _height * 0.85f), Color.Black);

            // Punteggio Player Blu (in alto a sinistra)
            string blueScoreText = $"Punteggio Blu: {gameSession._eggsEvent._myPenguinScore}";
            _spriteBatch.DrawString(_uiFont, blueScoreText, new Vector2(20, 20), Color.Blue);

            // Punteggio Player Rosso (in alto a destra)
            string redScoreText = $"Punteggio Rosso: {gameSession._eggsEvent._redPenguinScore}";
            Vector2 redScoreSize = _uiFont.MeasureString(redScoreText); // Misuriamo la scritta per allinearla a destra
            _spriteBatch.DrawString(_uiFont, redScoreText, new Vector2(_width - redScoreSize.X - 20, 20), Color.Red);
        }
    }
    
    protected override void Draw(GameTime gameTime)
    {
        // 1. Pulisce lo schermo (il "famoso" azzurro CornflowerBlue)
        GraphicsDevice.Clear(Color.White);
        
        _spriteBatch.Begin();

        _spriteBatch.Draw(_backgroundTexture, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
  
        DrawComponentsOfType<BasePlatform>(Components.OfType<BasePlatform>());
        DrawComponentsOfType<Obstacle>(Components.OfType<Obstacle>());
        DrawComponentsOfType<Egg>(Components.OfType<Egg>());
        DrawComponentsOfType<Penguin>(Components.OfType<Penguin>());
        DrawComponentsOfType<Ball>(Components.OfType<Ball>());
        
        drawUI();
        
        _spriteBatch.End();

        base.Draw(gameTime);
    }
    
    protected override void Update(GameTime gameTime)
    {
        
        networkManager?.Receive();

        // Controlla se gameSession e i suoi componenti interni sono pronti
        if (gameSession == null || gameSession._events == null || gameSession._myPenguin == null)
        {
            base.Update(gameTime);
            return;
        }
        
        // Solo ora chiami GetLatest per prendere l'ultimo stato arrivato dal server
        _reconciler.GetLatest(gameSession._events._authQueue, auth =>
        {
            _reconciler.OnServerAuth(auth.Ack, auth.Position);
            _reconciler.Apply(ref gameSession._myPenguin._position, 200f, gameSession.NetDt);
        });
        
        if (gameSession._eggsEvent._eggs.Count == 0)
        {
            if (gameSession._eggsEvent._myPenguinScore > gameSession._eggsEvent._redPenguinScore)
            {
                Console.WriteLine("Pinguino BLU ha vinto!!!!!!!!!!");
            }
            else
            {
                Console.WriteLine("Pinguino ROSSO ha vinto!!!!!!!!!");
            }
        }
        base.Update(gameTime);
    }
}