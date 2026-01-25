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
    private float finalPosition;
    

    public Ball(Game game, Vector2 startPosition, Vector2 startSpeed, float finalPosition) : base(game)
    {
        this._start_position = startPosition;
        this._position = startPosition;
        this._start_speed = startSpeed;
        _ball_time = 0.0f;
        this.finalPosition = finalPosition;
    }

    public void finalPointCalculous()
    {
        bool haRaggiuntoTarget = false;

        if (_start_speed.X > 0) // Tiro verso DESTRA
        {
            if (_position.X >= finalPosition) haRaggiuntoTarget = true;
        }
        else if (_start_speed.X < 0) // Tiro verso SINISTRA
        {
            if (_position.X <= finalPosition) haRaggiuntoTarget = true;
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
    private static extern Vector parabolic_motion(float startPositionX, float startPositionY, float startVelocityX,
        float startVelocityY, float gameTime);
    
    
    protected override void LoadContent()
    {
        load_texture("Content/images/Snowball1.png");
    }
    
    

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(_texture, _position, Color.White);
    }

    public override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _ball_time += deltaTime;
        var v = parabolic_motion(_start_position.X+20, _start_position.Y, _start_speed.X, -_start_speed.Y, _ball_time);
        _position.X = v._x;
        _position.Y = v._y;
        //Console.WriteLine($"Campo: {v._x}, Valore: {v._y}");
        
        finalPointCalculous();
       
    }
    
}