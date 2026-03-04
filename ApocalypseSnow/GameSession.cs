using System;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public record struct JoinSnapshot(uint PlayerId, Vector2 SpawnPos);
//public record struct AuthSnapshot(uint Ack, Vector2 Position);
public record struct RemoteSnapshot(Vector2 Position, StateList Mask);

public sealed class GameSession : GameComponent
{
    private NetworkManager networkManager;
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
    public string state = null;
  
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
        networkManager = new NetworkManager(_game, "127.0.0.1", 8080);
        //networkManager = new NetworkManager(this, "192.168.1.27", 8080);
        //networkManager = new NetworkManager(this, "7.tcp.eu.ngrok.io", 13297);
        //networkManager = new NetworkManager(this, "3.125.188.168", 13297);
        //networkManager = new NetworkManager(this, "18.192.31.30", 11179);
        state = "In attesa di un avversario...";
 
        System.Threading.Tasks.Task.Run(() =>
        {
            try 
            {
                string playerName = "Davide";
                JoinStruct joinStruct = new JoinStruct(playerName);
                NetworkManager.Instance.SendJoin(joinStruct);

                // Questa riga BLOCCHEREBBE il gioco, ma qui è dentro un Task, 
                // quindi la finestra di gioco continua a girare!
                JoinAckStruct ack = NetworkManager.Instance.WaitForJoinAck();

                // Una volta ricevuto l'ACK, creiamo i pinguini e gli oggetti
                // Usiamo un metodo di supporto
                StartMatch(ack);
            
                // Togliamo il messaggio di attesa
                state = null;
            }
            catch (Exception ex)
            {
                state = "Errore di connessione!";
                Console.WriteLine(ex.Message);
            }
        });
        
        base.Initialize();
    }

    private void StartMatch(JoinAckStruct ack)
    {
        IMovements movements = new MovementsManager(_game);
        IMovements movementsRed = new MovementsManagerRed();
        string bluePathPlatform = "Content/images/green_logo.png";
        string redPathPlatform = "Content/images/red_logo.png";
    
        NetDt = 1f / (float)ack.Heartz;

        _bluePlatform = new BasePlatform(_game, new Vector2(ack.SpawnX, ack.SpawnY), "blueP", bluePathPlatform);
        _redPlatform =  new BasePlatform(_game, new Vector2(ack.OpponentSpawnX, ack.OpponentSpawnY), "redP", redPathPlatform);
        _myPenguin = new Penguin(_game,"penguin", _bluePlatform._position, Vector2.Zero, movements, NetDt);
        _redPenguin = new Penguin(_game,"penguinRed", _redPlatform._position, Vector2.Zero, movementsRed, NetDt);
        _obstacle = new Obstacle(_game, new Vector2(100, 100), 1, 1);
        _eggsEvent = new EggsEvent(_game, _myPenguin, _redPenguin);
        _events = new Events(_game, _redPenguin);

        // AGGIUNTA AI COMPONENTI
        // Nota: MonoGame è solitamente thread-safe per Components.Add, 
        // ma è buona norma farlo qui appena ricevuti i dati.
        _game.Components.Add(_myPenguin);
        _game.Components.Add(_redPenguin);
        _game.Components.Add(_bluePlatform);
        _game.Components.Add(_redPlatform);
        _game.Components.Add(_obstacle);
        _game.Components.Add(_eggsEvent);
        _game.Components.Add(_events);
    }
    
    public void EndSession()
    {
        // 1. Rimuovi tutti i componenti che la sessione ha aggiunto al gioco
        CollisionManager.Instance.ClearAll();
        Reconciler.Instance.Reset();
        
        if (_eggsEvent != null)
        {
            _eggsEvent.RemoveAllEggs();
            _game.Components.Remove(_eggsEvent);
        }
        _game.Components.Remove(_myPenguin);
        _game.Components.Remove(_redPenguin);
        _game.Components.Remove(_bluePlatform);
        _game.Components.Remove(_redPlatform);
        _game.Components.Remove(_obstacle);
        _game.Components.Remove(_eggsEvent);
        _game.Components.Remove(_events);

        // 2. Rimuovi la sessione stessa dai componenti del gioco
        _game.Components.Remove(this);

        // 3. Chiudi la connessione di rete (fondamentale!)
        // Se il tuo NetworkManager ha un metodo Close o Dispose, chiamalo qui
        // Altrimenti, assicurati che il socket venga rilasciato.
        NetworkManager.Instance.Disconnect(); 

        Console.WriteLine("Partita terminata e risorse pulite.");
    }
    
    public override void Update(GameTime gameTime)
    {
        if (state != null || _myPenguin == null || _events == null)
        {
            return; 
        }
        
        NetworkManager.Instance?.Receive();
      
        // Solo ora chiami GetLatest per prendere l'ultimo stato arrivato dal server
        Reconciler.Instance.GetLatest(_events._authQueue, auth =>
        {
            Reconciler.Instance.OnServerAuth(auth.Ack, auth.Position);
            Reconciler.Instance.Apply(ref _myPenguin._position, 200f, NetDt);
        });
        
        if (_eggsEvent._eggs.Count == 0)
        {
            if (_eggsEvent._myPenguinScore > _eggsEvent._redPenguinScore)
            {
                Console.WriteLine("Pinguino BLU ha vinto!!!!!!!!!!");
            }
            else
            {
                Console.WriteLine("Pinguino ROSSO ha vinto!!!!!!!!!");
            }
        }
        base.Update(gameTime);
    }
}