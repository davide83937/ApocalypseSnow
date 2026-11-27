using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace ApocalypseSnow;

// Rappresenta la palla visibile sul client.
// La fisica viene campionata dalla DLL C++; qui gestiamo quando aggiornare
// la posizione, le collisioni locali e gli effetti grafici. Il server resta
// autoritativo sull'esito del tiro: la scomparsa della palla e' solo visiva.
public class Ball : CollisionExtensions
{
    // ===== PARAMETRI GRAFICI =====
    public const float LandingHoldSeconds = 0.5f;
    public const float LandingVisualSeconds = 3f;
    private const float ScaleMin = 1.0f;
    private const float ScaleMax = 1.35f;

    // ===== DATI IMMUTABILI DEL TIRO =====
    private readonly Vector2 _startPosition;
    private readonly Vector2 _groundVelocityWorld;
    private readonly float _startVerticalVelocity;
    private readonly float _gravity;
    private readonly float _flightDuration;
    private readonly float _maxHeight;
    private readonly string tagPenguin;

    public ShotKey ShotKey { get; }
    public uint ObservedStartTick { get; private set; }
    private bool _hasObservedStart;

    // ===== STATO LOCALE DELLA PALLA =====
    private float _ballTime;
    public float _scale;
    public float _currentHeight;

    // Questi stati riguardano il client, non la decisione del server.
    private bool _locallyLanded;           // Mostriamo il frammento, non la palla in volo.
    private bool _predictedHitVisual;      // HIT previsto: la palla sparisce subito.
    private bool _predictedPenguinContact; // Evita doppie notifiche dello stesso contatto.
    private bool _predictedObstacleBlock;  // Un muro impedisce di predire un HIT sul pinguino.
    private float _landingVisualElapsed;

    // ===== TEXTURE E OMBRA =====
    private Texture2D _texture;
    private Texture2D _brokenTexture;
    private int _halfTextureFractionWidth;
    private int _halfTextureFractionHeight;
    private bool _hitObstacle = false;
    private Vector2 _shadowPosition;
    private float _shadowScale;
    private float _shadowOpacity;

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
        this.tagPenguin = tagPenguin;
        _startPosition = startPosition;
        _groundVelocityWorld = groundVelocityWorld;
        _startVerticalVelocity = startVerticalVelocity;
        _gravity = gravity > 0f ? gravity : 150f;

        _flightDuration = ComputeFlightDurationSeconds(_startVerticalVelocity, _gravity);
        _maxHeight = ShotPhysicsAPI.ShotMaximumHeight(_startVerticalVelocity, _gravity);

        _ballTime = 0f;
        _scale = ScaleMin;
        _shadowPosition = startPosition;
        _shadowScale = 1f;
        _shadowOpacity = 0.4f;
    }

    public void MarkObservedStart(uint tick)
    {
        ObservedStartTick = tick;
        _hasObservedStart = true;
    }

    // ===== FISICA DEL VOLO =====

    public static float ComputeFlightDurationSeconds(float startVerticalSpeed, float gravity)
        => ShotPhysicsAPI.ShotFlightDuration(startVerticalSpeed, gravity);

    public readonly record struct FlightSample(Vector2 Screen, Vector2 Shadow, float Height);

    // La DLL calcola la traiettoria XYZ. Con Z=0 otteniamo la proiezione
    // dell'ombra sul terreno, usando la stessa posizione XY della palla.
    public static FlightSample SampleTrajectory(Vector2 start, Vector2 velocity, float verticalSpeed,
        float gravity, float seconds)
    {
        PhysicsAPI.calculate_parabolic_motion(seconds, velocity.X, velocity.Y,
            verticalSpeed, gravity, out float dx, out float dy, out float z);

        return new FlightSample(
            PhysicsAPI.calculateScreenPosition(start, dx, dy, z),
            PhysicsAPI.calculateScreenPosition(start, dx, dy, 0f),
            z);
    }

    private Texture2D load_texture(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        return Texture2D.FromStream(GraphicsDevice, stream);
    }
    
    protected override void UnloadContent()
    {
        _texture?.Dispose();
        _brokenTexture?.Dispose();
        base.UnloadContent();
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
        if (!_hitObstacle)
        {
            spriteBatch.Draw(
                _texture,
                _shadowPosition,
                null,
                Color.Black * _shadowOpacity,
                0f,
                Vector2.Zero,
                _shadowScale,
                SpriteEffects.None,
                0f
            );
        }

        Texture2D texToDraw = _hitObstacle ? _brokenTexture : _texture;

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
    
    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // HIT previsto: nascondiamo immediatamente la palla in Draw,
        // ma rimuoviamo il componente al successivo Update.
        if (_predictedHitVisual)
        {
            RemoveLocalVisual();
            return;
        }

        // Dopo un muro o l'atterraggio resta soltanto il frammento grafico.
        if (_locallyLanded)
        {
            UpdateLandingVisual(deltaTime);
            return;
        }

        _ballTime += deltaTime;

        if (HasLanded())
        {
            LandAtEndOfFlight();
            return;
        }

        UpdateFlightPosition();
    }

    private bool HasLanded()
        => _hasObservedStart
            ? ShotFlightTiming.HasLanded(ObservedStartTick, GameSession.LocalTick,
                                        _flightDuration, GameSession.TickRateHz)
            : _ballTime >= _flightDuration;

    private void LandAtEndOfFlight()
    {
        // Campioniamo il punto esatto di arrivo, non l'ultimo frame in aria.
        var landing = SampleTrajectory(_startPosition, _groundVelocityWorld,
            _startVerticalVelocity, _gravity, _flightDuration);

        _position = landing.Screen;
        _shadowPosition = landing.Shadow;
        _currentHeight = 0f;
        BeginLandingVisual();
    }

    private void UpdateFlightPosition()
    {
        // Prima del tick di atterraggio non campioniamo il tempo finale.
        float sampleTime = MathF.Min(_ballTime, MathF.Max(0f, _flightDuration - 0.000001f));
        var sample = SampleTrajectory(_startPosition, _groundVelocityWorld,
            _startVerticalVelocity, _gravity, sampleTime);

        _position = sample.Screen;
        _currentHeight = sample.Height;
        _shadowPosition = sample.Shadow;

        _scale = PhysicsAPI.calculateVisualScale(
            _currentHeight, _maxHeight, ScaleMin, ScaleMax, out float alpha);
        _shadowScale = PhysicsAPI.LerpFloat(1f, 0.5f, alpha);
        _shadowOpacity = PhysicsAPI.LerpFloat(0.4f, 0.15f, alpha);

        // La DLL controlla soltanto l'intersezione XY del collider.
        // La quota della palla viene valutata in OnCollisionEnter().
        CollisionManager.Instance.modifyObject(
            _tag,
            (int)sample.Shadow.X + _halfTextureFractionWidth,
            (int)sample.Shadow.Y + _halfTextureFractionHeight,
            _texture.Width,
            _texture.Height,
            minZ: _currentHeight,
            maxZ: _currentHeight);
    }

    // ===== IMPATTI LOCALI =====

    public static bool ShouldPredictPenguinHit(bool obstacleAlreadyHit, bool obstacleOverlapsNow)
        => !obstacleAlreadyHit && !obstacleOverlapsNow;

    public bool CanPredictPenguinHit()
    {
        if (_locallyLanded || _predictedHitVisual ||
            _currentHeight >= GameWorld.PenguinCollisionHeight)
            return false;

        // La palla puo' sovrapporsi in XY al muro senza colpirlo:
        // consideriamo il muro solo sotto la sua altezza fisica.
        bool obstacleOverlapsNow = CollisionManager.Instance.ShotOverlapsObstacle(
            _tag, _currentHeight);
        if (obstacleOverlapsNow)
            _predictedObstacleBlock = true;

        return ShouldPredictPenguinHit(_predictedObstacleBlock, obstacleOverlapsNow);
    }

    protected override void OnCollisionEnter(string otherTag, CollisionRecordOut collisionRecordOut)
    {
        if (_currentHeight >= 30f) return;
        switch (otherTag)
        {
            case "obstacle":
                _hitObstacle = true;
                Game.Components.Remove(this);
                CollisionManager.Instance.removeObject(_tag);
                break;

            case string t when isDeleteConditions(t):
                Console.WriteLine("Palla eliminata");
                Game.Components.Remove(this);
                CollisionManager.Instance.removeObject(_tag);
                break;
        }
    }
    
    private bool isDeleteConditions(string otherTag)
    {
        return (
            otherTag != "obstacle" &&
            otherTag != tagPenguin &&
            !otherTag.EndsWith("P") &&
            !otherTag.StartsWith("egg") 
        );
    }

        // La prediction dell'HIT e del freeze viene effettuata dal Penguin.
        // Qui impediamo solamente che il medesimo contatto venga elaborato
        // piu' volte o dopo un impatto con il muro.
        if ((otherTag == "penguin" || otherTag == "penguinRed") &&
            otherTag != _shooterTag && !_predictedPenguinContact && CanPredictPenguinHit())
        {
            _predictedPenguinContact = true;
            CollisionManager.Instance.removeObject(_tag);
        }
    }

    // Chiamato dal pinguino quando prevede un HIT: solo effetto locale.
    public void PredictPenguinImpact()
    {
        _predictedHitVisual = true;
        CollisionManager.Instance.removeObject(_tag);
    }

    private void BeginLandingVisual()
    {
        _locallyLanded = true;
        _landingVisualElapsed = 0f;
        CollisionManager.Instance.removeObject(_tag);
    }

    private void UpdateLandingVisual(float deltaTime)
    {
        _landingVisualElapsed += deltaTime;
        if (IsLocalVisualComplete(false, true, _landingVisualElapsed))
            RemoveLocalVisual();
    }

    private void RemoveLocalVisual()
    {
        CollisionManager.Instance.removeObject(_tag);
        Game.Components.Remove(this);
        Dispose();
    }

    // ===== RENDERING: NON MODIFICA LA FISICA =====

    public static bool IsLandingFragmentVisible(float elapsed)
        => elapsed >= 0f && elapsed < LandingVisualSeconds;

    public static bool IsLocalVisualComplete(bool predictedHit, bool landed, float elapsed)
        => predictedHit || (landed && !IsLandingFragmentVisible(elapsed));

    public static float LandingFragmentOpacity(float elapsed)
    {
        if (!IsLandingFragmentVisible(elapsed))
            return 0f;
        if (elapsed <= LandingHoldSeconds)
            return 1f;
        return (LandingVisualSeconds - elapsed) / (LandingVisualSeconds - LandingHoldSeconds);
    }

    public static bool ShouldDrawLandingFragment(bool locallyLanded, bool predictedHit, float elapsed)
        => locallyLanded && !predictedHit && LandingFragmentOpacity(elapsed) > 0f;



    private void DrawShadow(SpriteBatch spriteBatch)
    {
        // Ombra ovalizzata sul terreno, leggermente sotto la base della palla.
        spriteBatch.Draw(
            _texture,
            _shadowPosition + new Vector2(0f, _texture.Height * 0.75f),
            null,
            Color.Black * _shadowOpacity,
            0f,
            Vector2.Zero,
            new Vector2(_shadowScale, _shadowScale * 0.50f),
            SpriteEffects.None,
            0f);
    }

    // ===== RISORSE MONOGAME =====

  
}
