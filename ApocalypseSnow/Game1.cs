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
    
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
    }
    
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // CARICAMENTO DIRETTO: Nessun MGCB, nessun .xnb
        // Usiamo System.IO per aprire il file come un flusso di dati (stream)
        string path = "Content/images/penguin_blue_gathering.png";
        using (var stream = System.IO.File.OpenRead(path))
        {
            // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
            pinguTexture = Texture2D.FromStream(GraphicsDevice, stream);
            _myPenguin = new Penguin(this, pinguTexture, new Vector2(0, 0), new Vector2(0, 0));
            Components.Add(_myPenguin); // <--- QUESTA RIGA È FONDAMENTALE
        }
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
        }

        // 4. Invia tutto alla scheda video
        _spriteBatch.End();

        base.Draw(gameTime);
    }
    
    protected override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
    }
}