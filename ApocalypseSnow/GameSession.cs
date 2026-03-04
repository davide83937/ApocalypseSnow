using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public record struct JoinSnapshot(uint PlayerId, Vector2 SpawnPos);
//public record struct AuthSnapshot(uint Ack, Vector2 Position);
public record struct RemoteSnapshot(Vector2 Position, StateList Mask);

public sealed class GameSession : GameComponent
{
 
    
    public GameSession(Game game) : base(game)
    {
    }
}