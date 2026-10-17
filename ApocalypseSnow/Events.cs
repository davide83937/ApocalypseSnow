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
    public record struct AuthPositionSnapshot(uint Ack, Vector2 Position);
    public readonly ConcurrentQueue<AuthPositionSnapshot> _authPositionQueue = new();
    // Final server outcomes, indexed by the same key used by Ball.
    private readonly Dictionary<ShotKey, ShotResolutionMessage> _shotResolutions = new();
    public IReadOnlyDictionary<ShotKey, ShotResolutionMessage> ShotResolutions => _shotResolutions;
    
    public Events(Game game, Penguin redPenguin) : base(game)
    {
        _redPenguin = redPenguin;
        _obstacles = new List<Obstacle>();
        NetworkManager.Instance.OnAuthStateReceived += HandleServerReconciliation;
        NetworkManager.Instance.OnRemoteStateReceived += HandleRedState;
        NetworkManager.Instance.OnRemoteShotReceived += HandleRemoteShot;
        NetworkManager.Instance.OnShotResolvedReceived += HandleShotResolved;
        NetworkManager.Instance.OnObstacleReceived += HandleSpawnObstacles;
    }
    
    private void HandleServerReconciliation(uint ackSeq, float x, float y)
    {
        Vector2 position = new Vector2(x, y);
        _authPositionQueue.Enqueue(new AuthPositionSnapshot(ackSeq, position));
    }

    private void HandleSpawnObstacles(float x, float y)
    {
        Console.WriteLine($"Spawning obstacles: {x}, {y}");
        Vector2 position = new Vector2(x, y);
        Obstacle obstacle = new Obstacle(Game, position);
        _obstacles.Add(obstacle);
        Game.Components.Add(obstacle);
    }
    
    private void HandleRedState(float x, float y, int mask)
    {
        _redPenguin._position.X = x;
        _redPenguin._position.Y = y;
        //_redPenguin._penguinInputHandler._stateStruct.Current = (StateList)mask;
    }
    
    private void HandleShotResolved(ShotResolutionMessage resolution)
    {
        // Receipt is recorded but must NOT yet change freezing or predicted movement.
        _shotResolutions.TryAdd(resolution.Key, resolution);
    }

    private void HandleRemoteShot(RemoteShotMessage shot)
    {
        Vector2 target = new Vector2(shot.TargetX, shot.TargetY);
        _redPenguin.HandleRemoteShot(target, shot.Charge, shot.Type, shot.Key);
        _redPenguin._myEgg = null;
    }
}