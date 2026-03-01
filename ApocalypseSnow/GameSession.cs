using System;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public record struct JoinSnapshot(uint PlayerId, Vector2 SpawnPos);
public record struct AuthSnapshot(uint Ack, Vector2 Position);
public record struct RemoteSnapshot(Vector2 Position, StateList Mask);

public sealed class GameSession : GameComponent, IDisposable
{
    public event Action<IGameComponent>? OnEntitySpawned;
    public event Action<IGameComponent>? OnEntityDestroyed;

    private const string ServerAddr = "4.tcp.eu.ngrok.io";
    private const int ServerPort = 10083;

    private const float NetHz = 30f;
    private const float NetDt = 1f / NetHz;
    private const float MoveSpeed = 200f;
    
    
    public GameSession(Game game) : base(game)
    {
    }
}