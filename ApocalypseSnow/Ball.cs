using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace ApocalypseSnow;

public class Ball : CollisionExtensions
{
    private Texture2D _texture;
    private Texture2D _brokenTexture;

    private readonly Vector2 _startPosition;

    // Velocità sul piano "mondo" (non screen-space puro)
    private readonly Vector2 _groundVelocityWorld;

    // Componente verticale iniziale
    private readonly float _startVerticalVelocity;

    // Gravità passata dal PenguinShotHandler
    private readonly float _gravity;

    private float _ballTime;
    public float _scale;
    public float _currentHeight;

    private int _halfTextureFractionWidth;
    private int _halfTextureFractionHeight;
    private readonly string tagPenguin;
    public ShotKey ShotKey { get; }
    public uint ObservedStartTick { get; private set; }
    private bool _hasObservedStart;
    
    public void MarkObservedStart(uint tick, string role)
    {
        ObservedStartTick = tick;
        _hasObservedStart = true;
        ShotTrace.Log($"[SHOT_TRACE] role={role} event=START key={ShotKey} tick={tick} utc={DateTime.UtcNow:O}");
    }

    // Questi stati sono esclusivamente locali; ShotResolved non li modifica.
    private bool _locallyLanded;
    private bool _predictedHitVisual;
    private bool _predictedPenguinContact;
    private float _landingVisualElapsed;
    // Solo per MISS: 500 ms pienamente visibile, poi 2.5 secondi di dissolvenza.
    public const float LandingHoldSeconds = 0.5f;
    public const float LandingVisualSeconds = 3f;

    public static bool IsLandingFragmentVisible(float elapsed) => elapsed >= 0f && elapsed < LandingVisualSeconds;

    public static bool IsLocalVisualComplete(bool predictedHit, bool landed, float elapsed)
        => predictedHit || (landed && !IsLandingFragmentVisible(elapsed));

    // L'effetto parte dal primo frame di atterraggio locale, senza attendere
    // il risultato del server. Un HIT gia' predetto non puo' far ricomparire
    // la palla, neanche se il server lo smentisce.
    public static bool ShouldDrawLandingFragment(bool locallyLanded, bool predictedHit, float elapsed)
        => locallyLanded && !predictedHit && LandingFragmentOpacity(elapsed) > 0f;

    public static float LandingFragmentOpacity(float elapsed)
    {
        if (!IsLandingFragmentVisible(elapsed))
            return 0f;
        if (elapsed <= LandingHoldSeconds)
            return 1f;
        return (LandingVisualSeconds - elapsed) / (LandingVisualSeconds - LandingHoldSeconds);
    }

    // ===== VARIABILI PER L'OMBRA VISIVA =====
    private Vector2 _shadowPosition;
    private float _shadowScale;
    private float _shadowOpacity;

    // ===== PARAMETRI DEL MODELLO =====

    // Quanto schiacciamo il piano sull'asse Y a schermo
    private const float PlanePerspectiveY = 0.70f;

    // Quanto la quota z "alza" visivamente la palla
    private const float HeightProjection = 0.35f;

    // Scala visiva della palla in base alla quota
    private const float ScaleMin = 1.0f;
    private const float ScaleMax = 1.35f;

    // Tempo totale di volo e altezza massima
    private readonly float _flightDuration;
    public float FlightDurationSeconds => _flightDuration;
    public static float ComputeFlightDurationSeconds(float startVerticalSpeed, float gravity)
        => (2f * startVerticalSpeed) / gravity;

    public readonly record struct FlightSample(Vector2 Screen, Vector2 Shadow, float Height);

    // Stesso campionamento realmente usato da Ball.Update, verificabile nei test Go/C#.
    public static FlightSample SampleTrajectory(Vector2 start, Vector2 velocity, float verticalSpeed,
        float gravity, float seconds)
    {
        PhysicsAPI.calculate_parabolic_motion(seconds, velocity.X, velocity.Y,
            verticalSpeed, gravity, out float dx, out float dy, out float z);
        return new FlightSample(
            PhysicsAPI.calculateScreenPosition(start, dx, dy, z),
            PhysicsAPI.calculateScreenPosition(start, dx, dy, 0f), z);
    }
    private readonly float _maxHeight;

    public Ball(
        Game game,
        string tagPenguin,
        Vector2 startPosition,
        Vector2 groundVelocityWorld,
        float startVerticalVelocity,
        string tag,
        float gravity,
        ShotKey shotKey
    ) : base(game, tag, startPosition)
    {
        ShotKey = shotKey;
        _startPosition = startPosition;
        _groundVelocityWorld = groundVelocityWorld;
        _startVerticalVelocity = startVerticalVelocity;
        _gravity = gravity > 0f ? gravity : 150f;
        _ballTime = 0.0f;
        _scale = ScaleMin;
        this.tagPenguin = tagPenguin;

        _flightDuration = ComputeFlightDurationSeconds(_startVerticalVelocity, _gravity);
        _maxHeight = (_startVerticalVelocity * _startVerticalVelocity) / (2f * _gravity);

        _shadowPosition = startPosition;
        _shadowScale = 1.0f;
        _shadowOpacity = 0.4f;
    }

    private Texture2D load_texture(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }

    protected override void LoadContent()
    {
        _texture = load_texture("Content/images/palla1.png");
        _brokenTexture = load_texture("Content/images/palla_rotta.png");

        CollisionManager.Instance.addObject(_tag, _position.X, _position.Y, _texture.Width, _texture.Height);
        base.LoadContent();

        _halfTextureFractionWidth = _texture.Width / 2;
        _halfTextureFractionHeight = _texture.Height / 2;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        // HIT predetto localmente: la palla sparisce subito. Il server non
        // decide quando mostrare, nascondere o dissolvere questo effetto.
        if (_predictedHitVisual)
            return;

        if (_locallyLanded)
        {
            if (ShouldDrawLandingFragment(_locallyLanded, _predictedHitVisual, _landingVisualElapsed))
                spriteBatch.Draw(_brokenTexture, _position, Color.White * LandingFragmentOpacity(_landingVisualElapsed));
            return;
        }

        {
            spriteBatch.Draw(
                _texture,
                // Impronta sul terreno: ovalizzata e allineata alla base
                // della palla, cosi' rimane leggibile anche nel tiro basso.
                _shadowPosition + new Vector2(0f, _texture.Height * 0.75f),
                null,
                Color.Black * _shadowOpacity,
                0f,
                Vector2.Zero,
                new Vector2(_shadowScale, _shadowScale * 0.50f),
                SpriteEffects.None,
                0f
            );
        }

        Texture2D texToDraw = _texture;

        spriteBatch.Draw(
            texToDraw,
            _position,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            _scale,
            SpriteEffects.None,
            0f
        );
    }

    // HIT predetto: scomparsa visiva definitiva, indipendente dal server.
    public void PredictPenguinImpact()
    {
        _predictedHitVisual = true;
        CollisionManager.Instance.removeObject(_tag);
    }

    private void RemoveLocalVisual()
    {
        CollisionManager.Instance.removeObject(_tag);
        Game.Components.Remove(this);
        Dispose();
    }

    protected override void UnloadContent()
    {
        _texture?.Dispose();
        _brokenTexture?.Dispose();
        base.UnloadContent();
    }

    protected override void OnCollisionEnter(string otherTag, CollisionRecordOut collisionRecordOut)
    {
        if (_predictedHitVisual || _locallyLanded || _currentHeight >= 30f)
            return;

        // L'ostacolo non e' una sentenza locale: la palla resta DAVANTI al muro
        // fino al tick di impatto deciso dal server, anche con jitter.
        // Il pinguino gestisce gia' la prediction dell'HIT nell'altro handler.
        if ((otherTag == "penguin" || otherTag == "penguinRed") &&
            otherTag != tagPenguin && !_predictedPenguinContact)
        {
            _predictedPenguinContact = true;
            // Una singola prediction per palla, senza duplicati nei frame seguenti.
            CollisionManager.Instance.removeObject(_tag);
        }
    }

    // ===== NUOVO METODO PER IL MOTO PARABOLICO =====
  
    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_predictedHitVisual)
        {
            // Nascosta al frame dell'HIT, rimossa al successivo Update locale.
            RemoveLocalVisual();
            return;
        }
        if (_locallyLanded)
        {
            _landingVisualElapsed += deltaTime;
            if (IsLocalVisualComplete(false, true, _landingVisualElapsed))
                RemoveLocalVisual();
            return;
        }
        _ballTime += deltaTime;

        // Il volo termina al tick della simulazione locale, con la stessa
        // quantizzazione con cui il server determina la scadenza.
        bool landed = _hasObservedStart
            ? ShotFlightTiming.HasLanded(ObservedStartTick, GameSession.LocalTick,
                                        _flightDuration, GameSession.TickRateHz)
            : _ballTime >= _flightDuration;
        if (landed)
        {
            // Il frammento persiste fino a 3 s: posizioniamolo sul punto di
            // atterraggio reale, non sul penultimo campione ancora in aria.
            var landing = SampleTrajectory(_startPosition, _groundVelocityWorld,
                _startVerticalVelocity, _gravity, _flightDuration);
            _position = landing.Screen;
            _shadowPosition = landing.Shadow;
            _currentHeight = 0f;
            _locallyLanded = true;
            _landingVisualElapsed = 0f;
            CollisionManager.Instance.removeObject(_tag);
            ShotTrace.Log($"[SHOT_LOCAL_LANDING] key={ShotKey} tick={GameSession.LocalTick}; fade_local");
            return;
        }

        var sample = SampleTrajectory(_startPosition, _groundVelocityWorld, _startVerticalVelocity,
            _gravity, MathF.Min(_ballTime, MathF.Max(0f, _flightDuration - 0.000001f)));
        _currentHeight = sample.Height;
        _position = sample.Screen;

        _scale = PhysicsAPI.calculateVisualScale(_currentHeight, _maxHeight, ScaleMin, ScaleMax, out float alpha);

        // L'ombra deve stare sul piano di gioco, quindi quota z = 0
        Vector2 shadowScreen = sample.Shadow;
        float shadowScreenX = shadowScreen.X;
        float shadowScreenY = shadowScreen.Y;

        _shadowPosition = shadowScreen;

        _shadowScale = PhysicsAPI.LerpFloat(1.0f, 0.5f, alpha);
        _shadowOpacity = PhysicsAPI.LerpFloat(0.4f, 0.15f, alpha);

        if (_currentHeight >= 30f) 
        {
            // Spostiamo il collider fuori campo temporaneamente
            CollisionManager.Instance.modifyObject(_tag, -1000, -1000, _texture.Width, _texture.Height);
        }
        else 
        {
            // La palla è bassa, aggiorniamo il collider sulla posizione reale (ombra)
            int posCollX = (int)shadowScreenX + _halfTextureFractionWidth;
            int posCollY = (int)shadowScreenY + _halfTextureFractionHeight;
            CollisionManager.Instance.modifyObject(_tag, posCollX, posCollY, _texture.Width, _texture.Height);
        }
    }
}