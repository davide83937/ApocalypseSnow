using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace ApocalypseSnow;

public class Ball:CollisionExtensions
{
    private Texture2D _texture;
    //private string _tag;
    private readonly Vector2 _startPosition;
    //private Vector2 _position;
    private readonly Vector2 _startSpeed;
    private float _ballTime;
    private readonly Vector2 _finalPosition;
    private float _scale;
    private static readonly float Gravity = 150f;
    private static readonly float L = 0.1f;  // massimo
    private static readonly float K = 0.0005f;   // velocità di crescita
    private int _halfTextureFractionWidth;
    private int _halfTextureFractionHeight;
    private string tagPenguin;
  
    
    

    public Ball(Game game,string tagPenguin, Vector2 startPosition, Vector2 startSpeed, 
        Vector2 finalPosition, string tag) : base(game, tag, startPosition)
    {
        this._startPosition = startPosition;
        //this._position = startPosition;
        this._startSpeed = startSpeed;
        _ballTime = 0.0f;
        this._finalPosition = finalPosition;
        //_tag = tag;
        this._scale = 1.0f;
        this.tagPenguin = tagPenguin;
    }


    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void calculate_ball_scale(
        float startX, float startY,
        float finalX, float finalY,
        float posX, float posY,
        float startSpeedX,
        float L, float K,
        ref float scale,
        out bool reachedTarget
    );
    
    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern float calculate_ball_scale_only(
        float startX, float startY, float finalX, float finalY, float posX, 
        float startSpeedX, float L, float K, float currentScale);

    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool check_target_reached(float posX, float finalX, float startSpeedX);
    
    private void FinalPointCalculous()
    {
       
        // 1. Calcoliamo il nuovo scale delegando al C++
        _scale = calculate_ball_scale_only(
            _startPosition.X, _startPosition.Y, 
            _finalPosition.X, _finalPosition.Y, 
            _position.X, 
            _startSpeed.X, 
            L, K, 
            _scale
        );
        
        // 3. Rimozione dell'oggetto se il target è raggiunto
        if (chechTarget())
        {
            Game.Components.Remove(this);
            CollisionManager.Instance.removeObject(_tag);
        }
    }

    public bool chechTarget()
    {
        if (_startSpeed.X > 0) // Tiro verso DESTRA
        {
            if (_position.X >= _finalPosition.X) return true;
        }
        else if (_startSpeed.X < 0) // Tiro verso SINISTRA
        {
            if (_position.X <= _finalPosition.X) return true;
        }
        return false;
    }
    

    private void load_texture(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
        this._texture = Texture2D.FromStream(GraphicsDevice, stream);
    }

    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parabolic_motion(float gravity, float startPositionX, float startPositionY, ref float positionX, ref float positionY, float startVelocityX,
        float startVelocityY, float gameTime);
    
 
    
    protected override void LoadContent()
    {
        load_texture("Content/images/palla1.png");
        CollisionManager.Instance.addObject(_tag, _position.X, _position.Y, _texture.Width, _texture.Height );
        //CollisionManager.Instance.sendCollisionEvent += OnColliderEnter;
        base.LoadContent();
        _halfTextureFractionWidth  = _texture.Width / 2;
        _halfTextureFractionHeight = _texture.Height / 2;
    }
    

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(_texture, _position,null, Color.White, 0f, 
            Vector2.Zero, 
            _scale, 
            SpriteEffects.None, 
            0f);
    }
    
    
    protected override void OnCollisionEnter(string otherTag, CollisionRecordOut collisionRecordOut)
    {
        //if (!collisionRecordOut.Involves(_tag)) return;
        //string otherTag = collisionRecordOut.GetOtherTag(_tag);

        switch (otherTag)
        {
            case string t when isDeleteConditions(t):
                Game.Components.Remove(this);
                CollisionManager.Instance.removeObject(_tag);
                break;
        }
    }
    private bool isDeleteConditions(string otherTag)
    {
        return (otherTag != tagPenguin && !otherTag.EndsWith("P") && !otherTag.StartsWith("egg") && _scale < 1.15f);
    }
    
    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _ballTime += deltaTime;
        parabolic_motion(100,_startPosition.X+48, _startPosition.Y, ref _position.X, ref _position.Y,
            _startSpeed.X, -_startSpeed.Y, _ballTime);
        //Console.WriteLine($"Campo: {v._x}, Valore: {v._y}");
        //Console.WriteLine($"Scale: {_scale}");
        //Console.WriteLine($"Gravity: {gravity}");
        if (_scale < 1.0f) { _scale = 1.0f; }
        if (_scale > 1.4f) { _scale = 1.4f; }
        //Console.WriteLine($"Scale: {_scale}");
        int posCollX = (int)_position.X+ _halfTextureFractionWidth;
        int posCollY = (int)_position.Y+ _halfTextureFractionHeight;
        //Console.WriteLine($"posCollX: {posCollX}, posCollY: {posCollY}");
        CollisionManager.Instance.modifyObject(_tag, posCollX, posCollY, _texture.Width, _texture.Height );
        FinalPointCalculous();
    }
   
    
}