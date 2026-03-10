using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class Events:GameComponent
{
    Penguin _myPenguin;
    Penguin _redPenguin;
    List<Obstacle> _obstacles;
    public record struct AuthSnapshot(uint Ack, Vector2 Position);
    public readonly ConcurrentQueue<AuthSnapshot> _authQueue = new();
    
    public Events(Game game, Penguin redPenguin) : base(game)
    {
        _redPenguin = redPenguin;
        _obstacles = new List<Obstacle>();
        NetworkManager.Instance.OnAuthReceived += HandleServerReconciliation;
        NetworkManager.Instance.OnRemoteReceived += HandleRedPosition;
        NetworkManager.Instance.OnRemoteShotReceived += HandleRemoteShot;
        NetworkManager.Instance.OnObstacleReceived += HandleSpawnObstacles;
    }
    
    private void HandleServerReconciliation(uint ackSeq, float x, float y)
    {
        Vector2 position = new Vector2(x, y);
        _authQueue.Enqueue(new AuthSnapshot(ackSeq, position));
    }

    private void HandleSpawnObstacles(float x, float y)
    {
        Console.WriteLine($"Spawning obstacles: {x}, {y}");
        Vector2 position = new Vector2(x, y);
        Obstacle obstacle = new Obstacle(Game, position);
        _obstacles.Add(obstacle);
        Game.Components.Add(obstacle);
    }
    
    private void HandleRedPosition(float x, float y, int mask)
    {
        _redPenguin._position.X = x;
        _redPenguin._position.Y = y;
        //_redPenguin._penguinInputHandler._stateStruct.Current = (StateList)mask;
    }
    
    private void HandleRemoteShot(float mx, float my, int charge, ShotType shotType)
    {
        //charge = 30000;
        Vector2 target = new Vector2(mx, my);
        _redPenguin.HandleRemoteShot(target,charge,shotType);
        _redPenguin._myEgg = null;
    }
}