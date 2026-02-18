using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ApocalypseSnow;

public class Ball:DrawableGameComponent
{
    private Texture2D _texture;
    private Vector2 _start_position;
    private Vector2 _position;
    private Vector2 _start_speed;
    private float _ball_time;
    private Vector2 finalPosition;
    private float _scale;
    private float gravity = 150f;
    

    public Ball(Game game, Vector2 startPosition, Vector2 startSpeed, Vector2 finalPosition) : base(game)
    {
        this._start_position = startPosition;
        this._position = startPosition;
        this._start_speed = startSpeed;
        _ball_time = 0.0f;
        this.finalPosition = finalPosition;
        this._scale = 1.0f;
    }


    

    public void finalPointCalculous()
    {
        bool haRaggiuntoTarget = false;
        float differenceY = Math.Abs(_start_position.Y-finalPosition.Y);
        //Console.WriteLine(differenceY);

        float L = 0.1f;  // massimo
        float k = 0.0003f;   // velocità di crescita
        float t = differenceY; // es: tempo, velocità, carica

        float x = L * (1f - MathF.Exp(-k * t));
        
        if (_start_speed.X > 0) // Tiro verso DESTRA
        {
            if (_position.X >= finalPosition.X) haRaggiuntoTarget = true;
            if (_position.X < (finalPosition.X+_start_position.X+20)/2) { _scale = _scale + x; }
            else { _scale = _scale - x; }
        }
        else if (_start_speed.X < 0) // Tiro verso SINISTRA
        {
            if (_position.X <= finalPosition.X) haRaggiuntoTarget = true;
            if (_position.X > (_start_position.X+finalPosition.X+20)/2) { _scale = _scale + x; }
            else { _scale = _scale - x; }
        }

        // 3. Applichiamo l'impatto se il target è raggiunto
        if (haRaggiuntoTarget)
        {
            // Rimuoviamo la palla
            Game.Components.Remove(this);
            // ball_list.Remove(this); // Se hai passato il riferimento alla lista
        }
    }
    

    private void load_texture(string path)
    {
        using (var stream = System.IO.File.OpenRead(path))
        {
            // 1. Carichiamo l'immagine (deve essere nel Content Pipeline)
            this._texture = Texture2D.FromStream(GraphicsDevice, stream);
        }
    }

    [DllImport("libPhysicsDll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void parabolic_motion(float gravity, float startPositionX, float startPositionY, ref float positionX, ref float positionY, float startVelocityX,
        float startVelocityY, float gameTime);
    
    
    protected override void LoadContent()
    {
        load_texture("Content/images/Snowball1.png");
    }


    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(_texture, _position,null, Color.White, 0f, 
            Vector2.Zero, 
            _scale, 
            SpriteEffects.None, 
            0f);
    }

    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _ball_time += deltaTime;
        
        parabolic_motion(gravity,_start_position.X+20, _start_position.Y, ref _position.X, ref _position.Y,_start_speed.X, -_start_speed.Y, _ball_time);
        
        //Console.WriteLine($"Campo: {v._x}, Valore: {v._y}");
        //Console.WriteLine($"Scale: {_scale}");
        //Console.WriteLine($"Gravity: {gravity}");
        if (_scale < 1.0f) { _scale = 1.0f; }
        if (_scale > 1.6f) { _scale = 1.6f; }
        finalPointCalculous();
       
    }
    
}