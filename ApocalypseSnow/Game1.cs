using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace ApocalypseSnow;

public class Game1: Game {
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private GameSession gameSession;
    private SpriteFont _uiFont;
    private int _width;
    private int _height;
    private Texture2D _backgroundTextureGaming;
    private Texture2D _backgroundTextureMenu;
    private Reconciler _reconciler;

    private string state = "PREMI INVIO PER INIZIARE...";
    private const string GameBackgroundPath = "Content/images/background.png";
    private const string StartBackgroundPath = "Content/images/start.png";

    public Game1()
    {
        // Configura e gestisce il dispositivo grafico utilizzato dal gioco.
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }
    
    protected override void Initialize()
    {
        _width = GraphicsDevice.Viewport.Width;
        _height = GraphicsDevice.Viewport.Height;
        _reconciler  = new Reconciler(this);
        CollisionManager collisionManager = new CollisionManager(this);
        Components.Add(collisionManager);

        // richiama il metodo Initialize della classe base per inizializzare i componenti registrati nella collezione Components.
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _uiFont = Content.Load<SpriteFont>("UIAmmo");

        _backgroundTextureGaming = LoadTexture(GameBackgroundPath);
        _backgroundTextureMenu = LoadTexture(StartBackgroundPath);

        base.LoadContent();
    }


    private Texture2D LoadTexture(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }

    /// Disegna tutti i componenti utilizzando lo SpriteBatch.
    ///  al metodo potremmo aggiungere where T : IGameRenderable per evitare di mettere (dynamic), ma dovremmo creare un'interfaccia IGameRenderable
    ///  e far implementare a tutti i componenti che vogliamo disegnare questa interfaccia.
    public void DrawComponentsOfType<T>(IEnumerable<T> allComponents)
    {
        foreach (var component in allComponents)
            ((dynamic)component).Draw(_spriteBatch);
    }

    private void DrawUI()
    {
        if (gameSession._myPenguin != null && gameSession._redPenguin != null)
        {
            string esc = "Premi Esc per uscire (ma perdi)";
            _spriteBatch.DrawString(_uiFont, esc, new Vector2(_width / 15f, _height * 0.95f), Color.Chocolate);
            
            
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
        GraphicsDevice.Clear(Color.White);
        _spriteBatch.Begin();

        if (gameSession != null)
        {
            // Se stiamo aspettando l'avversario
            if (gameSession.state != null)
            {
                _spriteBatch.Draw(_backgroundTextureMenu, new Rectangle(0, 0, _width, _height), Color.White);
                DrawFancyText(gameSession.state, new Vector2(_width / 1.5f, _height / 1.2f), Color.Red, gameTime, true, 1.7f);
            }
            else 
            {
                //disegniamo i vari componenti del gioco (sfondo, piattaforme, ostacoli, uova, pinguini, palle) in questo esatto ordine
                //importante per la corretta sovrapposizione degli elementi sullo schermo
                _spriteBatch.Draw(_backgroundTextureGaming, new Rectangle(0, 0, _width, _height), Color.White);
                // Se la partita è iniziata (state è null), disegna i componenti
                DrawComponentsOfType<BasePlatform>(Components.OfType<BasePlatform>());
                DrawComponentsOfType<Obstacle>(Components.OfType<Obstacle>());
                DrawComponentsOfType<Egg>(Components.OfType<Egg>());
                DrawComponentsOfType<Penguin>(Components.OfType<Penguin>());
                DrawComponentsOfType<Ball>(Components.OfType<Ball>());
                DrawUI();
                if (gameSession.IsMatchOver)
                    DrawFancyText(gameSession.MatchResult,
                        new Vector2(_width / 2f, _height / 2f),
                        Color.Gold, gameTime, true, 1.3f);
            }
        }
        else
        {
            _spriteBatch.Draw(_backgroundTextureMenu, new Rectangle(0, 0, _width, _height), Color.White);
            DrawFancyText(state, new Vector2(_width / 1.5f, _height / 1.2f), Color.YellowGreen, gameTime, true, 1.6f);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }
    
  
    private void DrawFancyText(string text, Vector2 screenPos, Color textColor, GameTime gameTime, bool pulse = false, float baseScale = 1.0f)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 1. Calcolo dimensioni e origine (centro)
        Vector2 size = _uiFont.MeasureString(text);
        Vector2 origin = size / 2;
        float finalScale = baseScale;

        // 2. Effetto pulsante (opzionale)
        if (pulse)
        {
            float pulseDelta = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4f) * 0.05f;
            finalScale += pulseDelta;
        }

        // 3. DISEGNO DEL BORDO (Outline)
        // Disegniamo il testo in nero spostato di 2 pixel in tutte le direzioni principali
        // Moltiplichiamo l'offset per la scala così il bordo rimane proporzionato
        float outlineDist = 2f * finalScale; 
        Color outlineColor = Color.Black * 0.8f; // Nero leggermente trasparente per morbidezza

        // Disegno a croce (Sopra, Sotto, Sinistra, Destra)
        _spriteBatch.DrawString(_uiFont, text, screenPos + new Vector2(outlineDist, 0), outlineColor, 0f, origin, finalScale, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_uiFont, text, screenPos + new Vector2(-outlineDist, 0), outlineColor, 0f, origin, finalScale, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_uiFont, text, screenPos + new Vector2(0, outlineDist), outlineColor, 0f, origin, finalScale, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_uiFont, text, screenPos + new Vector2(0, -outlineDist), outlineColor, 0f, origin, finalScale, SpriteEffects.None, 0f);

        // 4. DISEGNO OMBRA PROFONDA (Opzionale, per dare profondità)
        _spriteBatch.DrawString(_uiFont, text, screenPos + new Vector2(3, 3) * finalScale, Color.Black * 0.5f, 0f, origin, finalScale, SpriteEffects.None, 0f);

        // 5. DISEGNO TESTO PRINCIPALE
        _spriteBatch.DrawString(_uiFont, text, screenPos, textColor, 0f, origin, finalScale, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// Gestisce l'avvio e la chiusura della sessione tramite tastiera
    /// e aggiorna i componenti del gioco durante la partita.
    /// </summary>
    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();

        if (gameSession == null)
        {
            // Avvia una nuova sessione quando viene premuto Invio.
            if (keyboard.IsKeyDown(Keys.Enter))
            {
                gameSession = new GameSession(this);
                Components.Add(gameSession);
            }
        }
        else if (keyboard.IsKeyDown(Keys.Escape))
        {
            // Termina la sessione corrente.
            gameSession.EndSession();
            gameSession = null;
        }
        else
        {
            // Dopo l'esito del server la scena rimane visibile ma non evolve.
            if (!gameSession.IsMatchOver)
                base.Update(gameTime);
        }
    }
}