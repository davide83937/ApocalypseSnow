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
    public AuthPositionSnapshot LatestLocalSnapshot { get; private set; }
    public bool HasLocalSnapshot { get; private set; }
    // Final server outcomes, indexed by the same key used by Ball.
    private readonly Dictionary<ShotKey, ShotResolutionMessage> _shotResolutions = new();
    public IReadOnlyDictionary<ShotKey, ShotResolutionMessage> ShotResolutions => _shotResolutions;
    public readonly Queue<ShotResolutionMessage> PendingShotResolutions = new();

    public Vector2 LatestRemotePosition { get; private set; }
    public bool HasRemotePosition { get; private set; }
    
    public Events(Game game, Penguin myPenguin, Penguin redPenguin) : base(game)
    {
        _myPenguin = myPenguin;
        _redPenguin = redPenguin;
        _obstacles = new List<Obstacle>();
        NetworkManager.Instance.OnAuthStateReceived += HandleServerReconciliation;
        NetworkManager.Instance.OnRemoteStateReceived += HandleRedState;
        NetworkManager.Instance.OnRemoteShotReceived += HandleRemoteShot;
        NetworkManager.Instance.OnShotResolvedReceived += HandleShotResolved;
        NetworkManager.Instance.OnObstacleReceived += HandleSpawnObstacles;
    }
    
    private void HandleServerReconciliation(uint ackSeq, float x, float y, uint freezeRemaining)
    {
        LatestLocalSnapshot = new AuthPositionSnapshot(ackSeq, new Vector2(x, y));
        HasLocalSnapshot = true;
        _authPositionQueue.Enqueue(LatestLocalSnapshot);
        _myPenguin.SetServerFreeze(freezeRemaining);
    }

    private void HandleSpawnObstacles(float x, float y)
    {
        Console.WriteLine($"Spawning obstacles: {x}, {y}");
        Vector2 position = new Vector2(x, y);
        Obstacle obstacle = new Obstacle(Game, position);
        _obstacles.Add(obstacle);
        Game.Components.Add(obstacle);
    }
    
    private void HandleRedState(float x, float y, int mask, uint freezeRemaining)
    {
        LatestRemotePosition = new Vector2(x, y);
        HasRemotePosition = true;
        _redPenguin._position = LatestRemotePosition;
        _redPenguin.SetServerFreeze(freezeRemaining);
    }
    
    // Accoda ogni esito autoritativo per applicarlo nel GameSession.
    private void HandleShotResolved(ShotResolutionMessage resolution)
    {
        if (_shotResolutions.TryAdd(resolution.Key, resolution))
            PendingShotResolutions.Enqueue(resolution);
    }

    // Crea la palla remota usando i dati del server.
    private void HandleRemoteShot(RemoteShotMessage shot)
    {
        Vector2 target = new Vector2(shot.TargetX, shot.TargetY);
        _redPenguin.HandleRemoteShot(target, shot.Charge, shot.Type, shot.Key);
    }
}