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

    private bool _hitObstacle = false;

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
    private readonly float _maxHeight;

    public Ball(
        Game game,
        string tagPenguin,
        Vector2 startPosition,
        Vector2 groundVelocityWorld,
        float startVerticalVelocity,
        string tag,
        float gravity
    ) : base(game, tag, startPosition)
    {
        _startPosition = startPosition;
        _groundVelocityWorld = groundVelocityWorld;
        _startVerticalVelocity = startVerticalVelocity;
        _gravity = gravity > 0f ? gravity : 150f;
        _ballTime = 0.0f;
        _scale = ScaleMin;
        this.tagPenguin = tagPenguin;

        _flightDuration = (2f * _startVerticalVelocity) / _gravity;
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

    public override void Update(GameTime gameTime)
    {
        if (_hitObstacle)
            return;

        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _ballTime += deltaTime;

        if (_ballTime >= _flightDuration)
        {
            _hitObstacle = true;
            CollisionManager.Instance.removeObject(_tag);
            return;
        }

        float t = _ballTime;

        float worldDeltaX = _groundVelocityWorld.X * t;
        float worldDeltaY = _groundVelocityWorld.Y * t;

        float z = (_startVerticalVelocity * t) - (0.5f * _gravity * t * t);
        if (z < 0f) z = 0f;
        _currentHeight = z;

        Console.WriteLine("Current height: " + _currentHeight);
        Vector2 screen = new Vector2(0, 0);
        screen = PhysicsAPI.calculateScreenPosition(_startPosition, worldDeltaX, worldDeltaY, z);
        //float screenX = _startPosition.X + worldDeltaX;
        //float screenY = _startPosition.Y + (PlanePerspectiveY * worldDeltaY) - (HeightProjection * z);

        _position.X = screen.X;
        _position.Y = screen.Y;

       // float alpha = 0f;
        //if (_maxHeight > 0.0001f)
       //     alpha = MathHelper.Clamp(z / _maxHeight, 0f, 1f);

        //_scale = PhysicsAPI.LerpFloat(ScaleMin, ScaleMax, alpha);
        _scale = PhysicsAPI.calculateVisualScale( z,  _maxHeight,  0f,  1f, out float alpha);

        Vector2 shadowScreen = PhysicsAPI.calculateScreenPosition(_startPosition, worldDeltaX, worldDeltaY, z);
        float shadowScreenX = shadowScreen.X;
        float shadowScreenY = shadowScreen.Y;//_startPosition.Y + (PlanePerspectiveY * worldDeltaY);

        _shadowPosition = new Vector2(shadowScreenX, shadowScreenY);

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

        //CollisionManager.Instance.modifyObject(_tag, posCollX, posCollY, _texture.Width, _texture.Height);
    }
}