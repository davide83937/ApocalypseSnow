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
    private int _myPenguinScore = 0;
    private int _redPenguinScore = 0;
    private Obstacle _obstacle;
    private Obstacle _obstacle1;
    private BasePlatform _bluePlatform;
    private BasePlatform _redPlatform;
    private SpriteFont _uiFont;
    private int _width;
    private int _height;
    private Texture2D _backgroundTexture;
    private List<Egg>  _eggs;
    private Random random;
    
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
        
        IMovements movements = new MovementsManager();
        IMovements movementsRed = new MovementsManagerRed();
        CollisionManager collisionManager = new CollisionManager(this);
        
        //CONNESSIONE ------------------------------------------------------
        NetworkManager networkManager = new NetworkManager("127.0.0.1", 8080);
        //networkManager.Connect();
        string bluePathPlatform = "Content/images/green_logo.png";
        string redPathPlatform = "Content/images/red_logo.png";
        
        string playerName = "Davide";
        JoinStruct joinStruct = new JoinStruct(playerName);
        networkManager.SendJoin(joinStruct);
        // 2. Attendi la risposta (il gioco si fermerà qui finché non arriva il secondo player)
        JoinAckStruct ack = networkManager.WaitForJoinAck();
        Console.WriteLine("--- Connessione Stabilita ---");
        Console.WriteLine($"Messaggio Tipo: {ack.Type}");
        Console.WriteLine($"ID Giocatore assegnato: {ack.PlayerId}");
        Console.WriteLine($"Posizione di Spawn: X={ack.SpawnX}, Y={ack.SpawnY}");
        Console.WriteLine($"Posizione di Spawn: X={ack.OpponentSpawnX}, Y={ack.OpponentSpawnY}");
        Console.WriteLine("-----------------------------");
 
        _bluePlatform = new BasePlatform(this, new Vector2(ack.SpawnX, ack.SpawnY), "blueP", bluePathPlatform);
        _redPlatform =  new BasePlatform(this, new Vector2(ack.OpponentSpawnX, ack.OpponentSpawnY), "redP", redPathPlatform);
        _myPenguin = new Penguin(this,"penguin", _bluePlatform._position, Vector2.Zero, movements, networkManager);// <-MANCAVA ULTIMO PARAMETRO
        _redPenguin = new Penguin(this,"penguinRed", _redPlatform._position, Vector2.Zero, movementsRed, networkManager);
        //collisionManager.sendCollisionEvent += _myPenguin.OnColliderEnter;
        _obstacle = new Obstacle(this, new Vector2(100, 100), 1, 1);
        _obstacle1 = new Obstacle(this, new Vector2(100, 50), 1, 1);
        _eggs = new List<Egg>();
     
        
        Components.Add(collisionManager);
        Components.Add(_myPenguin);
        Components.Add(_obstacle);
        //Components.Add(_obstacle1);
        Components.Add(_bluePlatform);
        Components.Add(_redPlatform);
        //Components.Add(_myPenguin);
        Components.Add(_redPenguin);
        random = new Random();
        
       
        
        for(int i = 0; i < 5; i++)
        {
            int x = random.Next(250, 500);
            int y = random.Next(0, 400);
            Egg egg = new Egg(this, new Vector2(x,y), "egg"+i);
            _eggs.Add(egg);
            Components.Add(egg);
        }
        
        // 3. FONDAMENTALE: base.Initialize() chiamerà automaticamente 
        // l'Initialize e il LoadContent di tutti i componenti in lista.
        _myPenguin._penguinColliderHandler.eggTakenEvent += removeEgg;
        _redPenguin._penguinColliderHandler.eggTakenEvent += removeEgg;
        _myPenguin._penguinColliderHandler.eggPutEvent += addEgg;
        _redPenguin._penguinColliderHandler.eggPutEvent += addEgg;
        _myPenguin._penguinColliderHandler.eggDeleteEvent += removeEggCompletaly;
        _redPenguin._penguinColliderHandler.eggDeleteEvent += removeEggCompletaly           ;
        base.Initialize();
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
    
    private void removeEggCompletaly(object sender, string tagEgg)
    {
        Egg eggToRemove = null;

        // 1. Cerchiamo l'uovo senza rimuoverlo subito
        foreach (Egg egg in _eggs)
        {
            if (egg._tag == tagEgg)
            {
                eggToRemove = egg;
                break; // Una volta trovato, usciamo dal ciclo
            }
        }

        // 2. Eseguiamo la rimozione fuori dal ciclo foreach
        if (eggToRemove != null)
        {
            // Rimuoviamo dalla logica delle collisioni
            CollisionManager.Instance.removeObject(eggToRemove._tag);
        
            // Rimuoviamo dai componenti di MonoGame
            Components.Remove(eggToRemove);
        
            // Rimuoviamo dalla nostra lista privata (ORA è SICURO)
            _eggs.Remove(eggToRemove);

            // 3. Aggiorniamo il punteggio
            Penguin penguin = null;
            if (sender == _myPenguin._penguinColliderHandler) penguin = _myPenguin;
            else if (sender == _redPenguin._penguinColliderHandler) penguin = _redPenguin;

            if (penguin._tag == "penguin")
                {
                    _myPenguinScore++;
                }
                else
                {
                    _redPenguinScore++;
                }
            
        
        }
    }

    /*private void addEgg(object sender, EventArgs e)
    {
        foreach (Egg egg in _eggs)
        {
            if (sender is Penguin penguin)
            {
                if (egg._tag ==penguin._myEgg)
                {
                    Components.Add(egg);
                    egg._position.X = penguin._position.X+48;
                    egg._position.Y = penguin._position.Y+100;
                    CollisionManager.Instance.addObject(egg._tag, egg._position.X, egg._position.Y, egg._texture.Width,
                        egg._texture.Height);
                }
            }
        }
    }*/
    
    private void addEgg(object sender, EventArgs e)
    {
        // Determiniamo quale pinguino ha lanciato l'evento tramite il suo handler
        Penguin penguin = null;
        if (sender == _myPenguin._penguinColliderHandler) penguin = _myPenguin;
        else if (sender == _redPenguin._penguinColliderHandler) penguin = _redPenguin;

        if (penguin != null)
        {
            foreach (Egg egg in _eggs)
            {
                if (egg._tag == penguin._myEgg)
                {
                    if (!Components.Contains(egg)) Components.Add(egg); // Evita duplicati
                    egg._position.X = penguin._position.X + 48;
                    egg._position.Y = penguin._position.Y + 100;
                
                    CollisionManager.Instance.addObject(egg._tag, egg._position.X, egg._position.Y, 
                        egg._texture.Width, egg._texture.Height);
                    break; 
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
        // 2. SECONDO STRATO: Ambiente (Ostacoli e Uova)
        // Cicliamo prima solo gli oggetti che devono stare "sotto"
        foreach (var component in Components)
        {
            if (component is BasePlatform plat)
            {
                plat.Draw(_spriteBatch);
            }
            else if (component is Obstacle obstacle)
            {
                obstacle.Draw(_spriteBatch);
            }
        }
        
        foreach (var component in Components)
        {
            if (component is Obstacle obstacle)
            {
                obstacle.Draw(_spriteBatch);
            }
            else if (component is Egg egg)
            {
                egg.Draw(_spriteBatch);
            }
        }

        // 3. TERZO STRATO: Entità dinamiche (Pinguini e Palle)
        // Disegniamo i pinguini e i proiettili sopra le piattaforme
        foreach (var component in Components)
        {
            if (component is Penguin penguin)
            {
                penguin.Draw(_spriteBatch);
            }
            else if (component is Ball ball)
            {
                ball.Draw(_spriteBatch);
            }
        }

        // 4. QUARTO STRATO: UI (Le scritte)
        // Sempre per ultime, così nulla può coprirle
        if (_myPenguin != null && _redPenguin != null)
        {
            // Testo Munizioni (in basso a sinistra come lo avevi)
            string ammoText = $"Munizioni: {_myPenguin._penguinShotHandler.Ammo}";
            _spriteBatch.DrawString(_uiFont, ammoText, new Vector2(_width / 10f, _height * 0.85f), Color.Black);

            // Punteggio Player Blu (in alto a sinistra)
            string blueScoreText = $"Punteggio Blu: {_myPenguinScore}";
            _spriteBatch.DrawString(_uiFont, blueScoreText, new Vector2(20, 20), Color.Blue);

            // Punteggio Player Rosso (in alto a destra)
            string redScoreText = $"Punteggio Rosso: {_redPenguinScore}";
            Vector2 redScoreSize = _uiFont.MeasureString(redScoreText); // Misuriamo la scritta per allinearla a destra
            _spriteBatch.DrawString(_uiFont, redScoreText, new Vector2(_width - redScoreSize.X - 20, 20), Color.Red);
        }
        // 4. Invia tutto alla scheda video
        _spriteBatch.End();

        base.Draw(gameTime);
    }
    
    protected override void Update(GameTime gameTime)
    {
        _width = GraphicsDevice.Viewport.Width;
        _height = GraphicsDevice.Viewport.Height;
        if (_eggs.Count == 0)
        {
            if (_myPenguinScore > _redPenguinScore)
            {
                Console.WriteLine("Pinguino BLU ha vinto!!!!!!!!!!!");
            }
            else
            {
                Console.WriteLine("Pinguino ROSSO ha vinto!!!!!!!!!!");
            }
        }
        base.Update(gameTime);
    }
}