using System.Collections.Concurrent;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class Events:GameComponent
{
    Penguin _myPenguin;
    Penguin _redPenguin;
    public readonly ConcurrentQueue<AuthSnapshot> _authQueue = new();
    
    public Events(Game game, Penguin redPenguin) : base(game)
    {
        _redPenguin = redPenguin;
        NetworkManager.Instance.OnAuthReceived += HandleServerReconciliation;
        NetworkManager.Instance.OnRemoteReceived += HandleRedPosition;
        NetworkManager.Instance.OnRemoteShotReceived += HandleRemoteShot;
    }
    
    private void HandleServerReconciliation(uint ackSeq, float x, float y)
    {
        Vector2 position = new Vector2(x, y);
        _authQueue.Enqueue(new AuthSnapshot(ackSeq, position));
    }
    
    private void HandleRedPosition(float x, float y, int mask)
    {
        _redPenguin._position.X = x;
        _redPenguin._position.Y = y;
        //_redPenguin._penguinInputHandler._stateStruct.Current = (StateList)mask;
    }
    
    private void HandleRemoteShot(int mx, int my, int charge)
    {
        Vector2 target = new Vector2(mx, my);
        _redPenguin.HandleRemoteShot(target);
        _redPenguin._myEgg = null;
    }
}