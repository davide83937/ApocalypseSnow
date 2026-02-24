using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class Game1: Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    
    // Dichiariamo il nostro pinguino qui!
    private Penguin _myPenguin;
    private Penguin _redPenguin;
    private Obstacle _obstacle;
    private Obstacle _obstacle1;
    private BasePlatform _bluePlatform;
    private BasePlatform _redPlatform;
    private SpriteFont _uiFont;
    private int _width;
    private int _height;
    private Texture2D _backgroundTexture;
    private List<Egg>  _eggs;
    
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
        IAnimation animationRed = new AnimationManagerRed();
        IMovements movements = new MovementsManager();
        IMovements movementsRed = new MovementsManagerRed();
        CollisionManager collisionManager = new CollisionManager(this);
        
        //CONNESSIONE ------------------------------------------------------
        //NetworkManager networkManager = new NetworkManager("127.0.0.1", 8080);
        //networkManager.Connect();
        string bluePathPlatform = "Content/images/green_logo.png";
        string redPathPlatform = "Content/images/red_logo.png";
 
        _bluePlatform = new BasePlatform(this, new Vector2(100, 300), "blueP", bluePathPlatform);
        _redPlatform =  new BasePlatform(this, new Vector2(550, 25), "redP", redPathPlatform);
        _myPenguin = new Penguin(this,"penguin", _bluePlatform._position, Vector2.Zero, animation, movements);// <-MANCAVA ULTIMO PARAMETRO
        _redPenguin = new Penguin(this,"penguinRed", _redPlatform._position, Vector2.Zero, animationRed, movementsRed);
        //collisionManager.sendCollisionEvent += _myPenguin.OnColliderEnter;
        _obstacle = new Obstacle(this, new Vector2(100, 100), 1, 1);
        _obstacle1 = new Obstacle(this, new Vector2(100, 50), 1, 1);
        _eggs = new List<Egg>();
        //Console.ReadLine("Inserisci il tuo nome");
        string playerName = "Davide";
        JoinStruct joinStruct = new JoinStruct(playerName);
        //networkManager.SendJoin(joinStruct);--------------------------------------------------------------------------
        
        Components.Add(collisionManager);
        //Components.Add(_myPenguin);
        Components.Add(_obstacle);
        //Components.Add(_obstacle1);
        Components.Add(_bluePlatform);
        Components.Add(_redPlatform);
        Components.Add(_myPenguin);
        Components.Add(_redPenguin);
        for(int i = 0; i < 1; i++)
        {
            Egg egg = new Egg(this, new Vector2(500,350), "egg"+i);
            _eggs.Add(egg);
            Components.Add(egg);
        }
        // 3. FONDAMENTALE: base.Initialize() chiamerà automaticamente 
        // l'Initialize e il LoadContent di tutti i componenti in lista.
        base.Initialize();
        _myPenguin.eggTakenEvent += removeEgg;
        _redPenguin.eggTakenEvent += removeEgg;
        _myPenguin.eggPutEvent += addEgg;
        _redPenguin.eggPutEvent += addEgg;
    }
    
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _uiFont = Content.Load<SpriteFont>("UIAmmo");
        load_texture("Content/images/environment.png");
        base.LoadContent();
    }

    private void removeEgg(object sender, string tagEgg)
    {
        foreach (Egg egg in _eggs)
        {
            if (egg._tag == tagEgg)
            {
                CollisionManager.Instance.removeObject(egg._tag);
                Components.Remove(egg);
            }
        }
    }

    private void addEgg(object sender, EventArgs e)
    {
        foreach (Egg egg in _eggs)
        {
            if (sender is Penguin penguin)
            {
                if (egg._tag ==penguin._myEgg)
                {
                    Components.Add(egg);
                    egg._position = penguin._position;
                    CollisionManager.Instance.addObject(egg._tag, penguin._position.X, penguin._position.Y, egg._texture.Width,
                        egg._texture.Height);
                }
            }
        }
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
            else if (component is Egg egg)
            {
                egg.Draw(_spriteBatch);
            }
            else if (component is Penguin penguin && penguin != _myPenguin)
            {
                penguin.Draw(_spriteBatch);
            }
            else if (component is BasePlatform plat)
            {
                plat.Draw(_spriteBatch);
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