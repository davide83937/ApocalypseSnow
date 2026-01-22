using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class Game1: Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    
    // Dichiariamo il nostro pinguino qui!
    private Penguin _myPenguin;
    private Texture2D pinguTexture;
    private SpriteFont _uiFont;
    private int width;
    private int height;
    
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        width = 0;
        height = 0;
    }
    
    protected override void Initialize()
    {
        // 1. Crea il pinguino qui
        _myPenguin = new Penguin(this, new Vector2(100, 100), Vector2.Zero);
    
        // 2. Aggiungilo ai componenti PRIMA di chiamare base.Initialize()
        Components.Add(_myPenguin);

        // 3. FONDAMENTALE: base.Initialize() chiamerà automaticamente 
        // l'Initialize e il LoadContent di tutti i componenti in lista.
        base.Initialize();
    }
    
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _uiFont = Content.Load<SpriteFont>("UIAmmo");
        base.LoadContent();
    }
    
    protected override void Draw(GameTime gameTime)
    {
        // 1. Pulisce lo schermo (il "famoso" azzurro CornflowerBlue)
        GraphicsDevice.Clear(Color.White);

        // 2. Inizia la coda di disegno
        _spriteBatch.Begin();

        // 3. Chiama il disegno del pinguino
        if (_myPenguin != null)
        {
            _myPenguin.Draw(_spriteBatch);
            // Disegno della UI (Munizioni)
            string ammoText = $"Munizioni: {_myPenguin._ammo}";
            // Posizioniamo il testo in alto a sinistra (10, 10)
            _spriteBatch.DrawString(_uiFont, ammoText, new Vector2(width/10, (height/1.2f)), Color.Black);
        }

        // 4. Invia tutto alla scheda video
        _spriteBatch.End();

        base.Draw(gameTime);
    }
    
    protected override void Update(GameTime gameTime)
    {
        width = GraphicsDevice.Viewport.Width;
        height = GraphicsDevice.Viewport.Height;
        base.Update(gameTime);
    }
}