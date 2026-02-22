using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class Game1: Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    
    // Dichiariamo il nostro pinguino qui!
    private Penguin _myPenguin;
    private Obstacle _obstacle;
    private SpriteFont _uiFont;
    private int _width;
    private int _height;
    private Texture2D _backgroundTexture;
    
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
        // 1. Crea il pinguino qui
        IAnimation animation = new AnimationManager();
        IMovements movements = new MovementsManager();
        CollisionManager collisionManager = new CollisionManager(this);
        //CONNESSIONE ------------------------------------------------------
        //NetworkManager networkManager = new NetworkManager("127.0.0.1", 8080);
        //networkManager.Connect();
        _myPenguin = new Penguin(this, new Vector2(100, 400), Vector2.Zero, animation, movements);// <-MANCAVA ULTIMO PARAMETRO
        //collisionManager.sendCollisionEvent += _myPenguin.OnColliderEnter;
        _obstacle = new Obstacle(this, new Vector2(100, 100), 1, 1);


        //Console.ReadLine("Inserisci il tuo nome");
        string playerName = "Davide";
        JoinStruct joinStruct = new JoinStruct(playerName);
        //networkManager.SendJoin(joinStruct);--------------------------------------------------------------------------
    
        
        // 2. Aggiungilo ai componenti PRIMA di chiamare base.Initialize()
        Components.Add(collisionManager);
        //Components.Add(_myPenguin);
        Components.Add(_obstacle);
        Components.Add(_myPenguin);
       

        // 3. FONDAMENTALE: base.Initialize() chiamerà automaticamente 
        // l'Initialize e il LoadContent di tutti i componenti in lista.
        base.Initialize();
    }
    
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _uiFont = Content.Load<SpriteFont>("UIAmmo");
        load_texture("Content/images/environment.png");
        base.LoadContent();
    }
    
    private void load_texture(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
        this._backgroundTexture = Texture2D.FromStream(GraphicsDevice, stream);
    }
    
    protected override void Draw(GameTime gameTime)
    {
        // 1. Pulisce lo schermo (il "famoso" azzurro CornflowerBlue)
        GraphicsDevice.Clear(Color.White);

        // 2. Inizia la coda di disegno
        _spriteBatch.Begin();

        _spriteBatch.Draw(_backgroundTexture, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
        // 3. Chiama il disegno del pinguino
        if (_myPenguin != null)
        {
            
            _myPenguin.Draw(_spriteBatch);
            // Disegno della UI (Munizioni)
            string ammoText = $"Munizioni: {_myPenguin.Ammo}";
            // Posizioniamo il testo in alto a sinistra (10, 10)
            _spriteBatch.DrawString(_uiFont, ammoText, new Vector2(_width/10f, (_height/1.2f)), Color.Black);
        }

        foreach (var component in Components)
        {
            if (component is Ball ball)
            {
                ball.Draw(_spriteBatch);
            }
            else if (component is Obstacle obstacle)
            {
                obstacle.Draw(_spriteBatch);
            }
        }

        // 4. Invia tutto alla scheda video
        _spriteBatch.End();

        base.Draw(gameTime);
    }
    
    protected override void Update(GameTime gameTime)
    {
        _width = GraphicsDevice.Viewport.Width;
        _height = GraphicsDevice.Viewport.Height;
        base.Update(gameTime);
    }
}