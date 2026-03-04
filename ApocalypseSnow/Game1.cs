using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class Game1: Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private GameSession gameSession;
    private SpriteFont _uiFont;
    private int _width;
    private int _height;
    private Texture2D _backgroundTexture;
    //private NetworkManager networkManager;
    private Reconciler _reconciler;

    private string state = "Premi invio per iniziare...";
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
        //networkManager = new NetworkManager(this, "127.0.0.1", 8080);
        _width = GraphicsDevice.Viewport.Width;
        _height = GraphicsDevice.Viewport.Height;
        _reconciler  = new Reconciler(this);
        //IMovements movements = new MovementsManager(this);
        //IMovements movementsRed = new MovementsManagerRed();
        CollisionManager collisionManager = new CollisionManager(this);
        
        
        Components.Add(collisionManager);
        //Components.Add(_eggsEvent);
        //Components.Add(_events);
        //gameSession = new GameSession(this);
        //Components.Add(gameSession);
        
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

        if (gameSession != null)
        {
            DrawComponentsOfType<BasePlatform>(Components.OfType<BasePlatform>());
            DrawComponentsOfType<Obstacle>(Components.OfType<Obstacle>());
            DrawComponentsOfType<Egg>(Components.OfType<Egg>());
            DrawComponentsOfType<Penguin>(Components.OfType<Penguin>());
            DrawComponentsOfType<Ball>(Components.OfType<Ball>());
            drawUI();
        }
        else
        {
            
            _spriteBatch.DrawString(_uiFont, state, new Vector2(100, 100), Color.Black);
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }
    
    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape) && gameSession != null)
        {
            
            gameSession.EndSession();
            gameSession = null; // Portiamo a null per indicare che non c'è una partita attiva
            return;
        }

        //bool enterState = Keyboard.GetState().IsKeyDown(Keys.Enter);
        if (Keyboard.GetState().IsKeyDown(Keys.Enter) && gameSession == null)
        {
            gameSession = new GameSession(this);
            Components.Add(gameSession);
            return;
        }

        // Se non c'è una sessione, potresti voler mostrare di nuovo il messaggio in console
        if (gameSession == null)
        {
            // Qui potresti rimettere la logica per far ripartire una nuova partita
            // Console.WriteLine("Premi Invio per una nuova partita...");
            return;
        }
        base.Update(gameTime);
    }
}