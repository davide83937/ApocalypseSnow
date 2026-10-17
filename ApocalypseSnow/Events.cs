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
    public readonly Queue<ShotResolutionMessage> PendingShotResolutions = new();
    private readonly Dictionary<ShotKey, uint> _remoteShotStartTicks = new();

    public bool TryTakeRemoteShotStart(ShotKey key, out uint startTick)
    {
        if (_remoteShotStartTicks.TryGetValue(key, out startTick))
        {
            _remoteShotStartTicks.Remove(key);
            return true;
        }
        return false;
    }
    public Vector2 LatestRemotePosition { get; private set; }
    public bool HasRemotePosition { get; private set; }
    
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
        LatestRemotePosition = new Vector2(x, y);
        HasRemotePosition = true;

        // Durante un HIT predetto non sovrascrive il remoto con stati ancora non congelati.
        bool predictedFreeze = _redPenguin.LocalFreeze.IsFrozen(GameSession.LocalTick);
        bool serverFreezing = (((StateList)mask & StateList.Freezing) != StateList.None);
        if (!predictedFreeze || serverFreezing)
            _redPenguin._position = LatestRemotePosition;
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
        _remoteShotStartTicks[shot.Key] = GameSession.LocalTick;
        _redPenguin.HandleRemoteShot(target, shot.Charge, shot.Type, shot.Key);
        _redPenguin._myEgg = null;
    }
}