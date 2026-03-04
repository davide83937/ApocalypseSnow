using System;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public record struct JoinSnapshot(uint PlayerId, Vector2 SpawnPos);
//public record struct AuthSnapshot(uint Ack, Vector2 Position);
public record struct RemoteSnapshot(Vector2 Position, StateList Mask);

public sealed class GameSession : GameComponent
{
    //private NetworkManager networkManager;
    public Penguin _myPenguin;
    public Penguin _redPenguin;
    private Obstacle _obstacle;
    //private Obstacle _obstacle1;
    private BasePlatform _bluePlatform;
    private BasePlatform _redPlatform;
    public float NetDt = 0;
    public EggsEvent _eggsEvent;
    public Events _events;
    private Game _game;
    
    public GameSession(Game game) : base(game)
    {
        _game = game;
     
        //networkManager = new NetworkManager(this, "192.168.1.27", 8080);
        //networkManager = new NetworkManager(this, "7.tcp.eu.ngrok.io", 13297);
        //networkManager = new NetworkManager(this, "3.125.188.168", 13297);
        //networkManager = new NetworkManager(this, "18.192.31.30", 11179);
    }

    public override void Initialize()
    {
        //networkManager = new NetworkManager(_game, "127.0.0.1", 8080);
        //networkManager = new NetworkManager(this, "192.168.1.27", 8080);
        //networkManager = new NetworkManager(this, "7.tcp.eu.ngrok.io", 13297);
        //networkManager = new NetworkManager(this, "3.125.188.168", 13297);
        //networkManager = new NetworkManager(this, "18.192.31.30", 11179);
        string playerName = "Davide";
        JoinStruct joinStruct = new JoinStruct(playerName);
        NetworkManager.Instance.SendJoin(joinStruct);
        // 2. Attendi la risposta (il gioco si fermerà qui finché non arriva il secondo player)
        JoinAckStruct ack = NetworkManager.Instance.WaitForJoinAck();
        IMovements movements = new MovementsManager(_game);
        IMovements movementsRed = new MovementsManagerRed();
        string bluePathPlatform = "Content/images/green_logo.png";
        string redPathPlatform = "Content/images/red_logo.png";
        NetDt = 1f / (float)ack.Heartz;
        Console.WriteLine($"Delta time: {NetDt}");
        _bluePlatform = new BasePlatform(_game, new Vector2(ack.SpawnX, ack.SpawnY), "blueP", bluePathPlatform);
        _redPlatform =  new BasePlatform(_game, new Vector2(ack.OpponentSpawnX, ack.OpponentSpawnY), "redP", redPathPlatform);
        _myPenguin = new Penguin(_game,"penguin", _bluePlatform._position, Vector2.Zero, movements, NetDt);
        _redPenguin = new Penguin(_game,"penguinRed", _redPlatform._position, Vector2.Zero, movementsRed, NetDt);
        _obstacle = new Obstacle(_game, new Vector2(100, 100), 1, 1);
        //_obstacle1 = new Obstacle(_game, new Vector2(100, 50), 1, 1);
        _eggsEvent = new EggsEvent(_game, _myPenguin, _redPenguin);
        _events = new Events(_game, _redPenguin);
        _game.Components.Add(_myPenguin);
        _game.Components.Add(_redPenguin);
        _game.Components.Add(_bluePlatform);
        _game.Components.Add(_redPlatform);
        _game.Components.Add(_obstacle);
        //_game.Components.Add(collisionManager);
        _game.Components.Add(_eggsEvent);
        _game.Components.Add(_events);
        
        base.Initialize();
    }
    
    
}