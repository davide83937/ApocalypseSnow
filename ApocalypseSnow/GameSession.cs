using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

/// Gestisce il ciclo di vita di una partita.
/// Connessione al server, creazione degli oggetti di gioco,
/// comunicazione client-server e riconciliazione dello stato.
public sealed class GameSession : GameComponent
{
    private NetworkManager networkManager;
    // task di rete che consegna l'esito del JOIN al thread principale di MonoGame.
    private readonly ConcurrentQueue<(JoinAckStruct Ack, string Error)> _joinResults = new();
    public Penguin _myPenguin;
    public Penguin _redPenguin;
    private BasePlatform _bluePlatform;
    private BasePlatform _redPlatform;
    public float NetDt = 0;
    public EggsEvent _eggsEvent;
    public Events _events;
    public string state = null;
    private uint _localPlayerId;
    public string MatchResult { get; private set; }
    public bool IsMatchOver => MatchResult != null;

    //copia del tick locale, che incrementiamo ad ogni update, e del tick accumulato, che invece incrementiamo con il delta time
    //e usiamo per capire quando è ora di fare un nuovo tick di gioco (e quindi inviare un nuovo input al server)
    public static uint LocalTick { get; private set; } = 0;
    public static uint TickRateHz { get; private set; } = 30;
    // Questo accumulatore ci serve per gestire il tick rate del gioco, che è indipendente dal frame rate.
    private float _tickAcc = 0f;

    // LocalTick e' la prossima sequenza da inviare; il movimento usa quella appena inviata.
    public static uint LastSentInputTick => LocalTick == 0 ? 0 : LocalTick - 1;

    public GameSession(Game game) : base(game)
    {
        //richiamiamo il costruttore della classe padre GameComponent, passando l'oggetto Game come parametro.
        //GameSession estende GameComponent, quindi eredita le funzionalità di base per essere un componente del gioco.
        //Gamesession quindi eredita la proprietà Game, che rappresenta l'istanza del gioco a cui il componente appartiene.
    }

    public override void Initialize()
    {
        string host = Environment.GetEnvironmentVariable("APOCALYPSE_SERVER_HOST") ?? "127.0.0.1";
        int port = int.TryParse(Environment.GetEnvironmentVariable("APOCALYPSE_SERVER_PORT"), out int configuredPort)
                   && configuredPort is > 0 and <= 65535 ? configuredPort : 8080;
        networkManager = new NetworkManager(Game, host, port);
        Console.WriteLine($"[NETWORK_TEST] connecting to {host}:{port}");
        state = "IN ATTESA DI UN AVVERSARIO...";
 
        System.Threading.Tasks.Task.Run(() =>
        {
            try 
            {
                string playerName = "Davide";
                JoinStruct joinStruct = new JoinStruct(playerName);
                networkManager.SendJoin(joinStruct);

                // L'attesa bloccante resta sul thread secondario.
                JoinAckStruct ack = networkManager.WaitForJoinAck();

                // L'avvio della partita avverrà in Update, sul thread principale.
                _joinResults.Enqueue((ack, null));
            }
            catch (Exception ex)
            {
                // Anche gli errori vengono comunicati al thread principale.
                _joinResults.Enqueue((default, ex.Message));
            }
        });
        
        base.Initialize();
    }

    private void StartMatch(JoinAckStruct ack)
    {
        IMovements movements = new MovementsManager(Game);
        IMovements movementsRed = new MovementsManagerRed();
        string bluePathPlatform = "Content/images/green_logo.png";
        string redPathPlatform = "Content/images/red_logo.png";

        //aggiunti questi due per tenere traccia del tick del server e del client, utili per la reconciler
        LocalTick = 1; // Il server inizializza LastSeqN=0: la prima sequenza valida è 1.
        TickRateHz = ack.Heartz;
        _tickAcc = 0f;
        _localPlayerId = ack.PlayerId;
        MatchResult = null;
        networkManager.OnMatchStatusReceived += HandleMatchStatus;
        Reconciler.Instance.Reset();
        //Reconciler.Instance.SetNextSeq(1);

        NetDt = 1f / (float)ack.Heartz;

        _bluePlatform = new BasePlatform(Game, new Vector2(ack.SpawnX, ack.SpawnY), "blueP", bluePathPlatform);
        _redPlatform =  new BasePlatform(Game, new Vector2(ack.OpponentSpawnX, ack.OpponentSpawnY), "redP", redPathPlatform);
        _myPenguin = new Penguin(Game,"penguin", _bluePlatform._position, Vector2.Zero, movements, NetDt, ack.PlayerId);
        _redPenguin = new Penguin(Game,"penguinRed", _redPlatform._position, Vector2.Zero, movementsRed, NetDt, ack.PlayerId == 1 ? 2u : 1u);

        _eggsEvent = new EggsEvent(Game, _myPenguin, _redPenguin);
        _events = new Events(Game, _redPenguin);
        Reconciler.Instance.AttachMatchContext(ack.PlayerId, _myPenguin, _redPenguin, _events);

        // StartMatch viene eseguito da Update: registriamo i componenti sul thread principale.
        Game.Components.Add(_myPenguin);
        Game.Components.Add(_redPenguin);
        Game.Components.Add(_bluePlatform);
        Game.Components.Add(_redPlatform);

        Game.Components.Add(_eggsEvent);
        Game.Components.Add(_events);
    }
    
    // I punteggi arrivano dal server e non dal conteggio locale degli sprite.
    private void HandleMatchStatus(uint player1Score, uint player2Score, uint remaining, uint winnerId)
    {
        _eggsEvent._myPenguinScore = (int)(_localPlayerId == 1 ? player1Score : player2Score);
        _eggsEvent._redPenguinScore = (int)(_localPlayerId == 1 ? player2Score : player1Score);
        if (winnerId != 0)
        {
            string outcome = winnerId == 3 ? "PAREGGIO!" :
                winnerId == _localPlayerId ? "HAI VINTO!" : "HAI PERSO!";
            MatchResult = $"{outcome}  {_eggsEvent._myPenguinScore} - {_eggsEvent._redPenguinScore}";
        }
    }

    public void EndSession()
    {
        // 1. Rimuovi tutti i componenti che la sessione ha aggiunto al gioco
        // Le palle non confermate vanno rimosse anche quando si esce dal match.
        foreach (Ball ball in Game.Components.OfType<Ball>().ToArray())
        {
            CollisionManager.Instance.removeObject(ball._tag);
            Game.Components.Remove(ball);
            ball.Dispose();
        }
        CollisionManager.Instance.ClearAll();
        Reconciler.Instance.Reset();
        
        if (_eggsEvent != null)
        {
            _eggsEvent.RemoveAllEggs();
            Game.Components.Remove(_eggsEvent);
        }
        Game.Components.Remove(_myPenguin);
        Game.Components.Remove(_redPenguin);
        Game.Components.Remove(_bluePlatform);
        Game.Components.Remove(_redPlatform);
        Game.Components.Remove(_eggsEvent);
        Game.Components.Remove(_events);

        // 2. Rimuovi la sessione stessa dai componenti del gioco
        Game.Components.Remove(this);

        // 3. Chiudi la connessione di rete (fondamentale!)
        // Se il tuo NetworkManager ha un metodo Close o Dispose, chiamalo qui
        // Altrimenti, assicurati che il socket venga rilasciato.
        if (networkManager != null)
        {
            networkManager.OnMatchStatusReceived -= HandleMatchStatus;
            networkManager.Disconnect();
        }

        Console.WriteLine("Partita terminata e risorse pulite.");
    }
    
    public override void Update(GameTime gameTime)
    {
        // L'ACK viene prodotto dal task di rete e consumato sul thread principale.
        if (_joinResults.TryDequeue(out var result))
        {
            if (result.Error != null)
            {
                state = "Errore di connessione!";
                Console.WriteLine(result.Error);
            }
            else
            {
                try
                {
                    StartMatch(result.Ack);
                    state = null;
                }
                catch (Exception ex)
                {
                    state = "Errore di avvio partita!";
                    Console.WriteLine(ex);
                }
            }
        }

        // Se il match non è iniziato, non eseguire la simulazione.
        if (state != null || _myPenguin == null || _events == null)
            return;
        
        NetworkManager.Instance?.Receive();
        if (IsMatchOver)
            return;

        // Acquisisce ultimo ACK e posizione dal server, senza correggere ancora il movimento.
        Reconciler.Instance.ReadAuthPosition();
        // Per ogni tiro ricevuto: conferma/smentisce la prediction e gestisce gli HIT.
        Reconciler.Instance.ReadAuthShot(LocalTick);

        // Invia un input ogni NetDt secondi (30 Hz), recuperando eventuali tick arretrati.
        _tickAcc += (float)gameTime.ElapsedGameTime.TotalSeconds;
        while (_tickAcc >= NetDt)
        {
            _tickAcc -= NetDt;
            Debug.WriteLine($"LocalTick++: {LocalTick}");

            // StateStruct e' una struct: modifichiamo la copia da inviare, non lo stato del pinguino.
            StateStruct stateToSend = _myPenguin._penguinInputHandler._stateStruct;

            if (_myPenguin.LocalFreeze.IsFrozen(LocalTick))
            {
                stateToSend.Current |= StateList.Freezing;
                stateToSend.Current &= ~StateMasks.InputsBlockedByFreeze;
            }
            else
            {
                stateToSend.Current &= ~StateList.Freezing;
            }

            // Registra solo i bit necessari alla riconciliazione della posizione.
            StateList pendingInput = stateToSend.Current & StateMasks.PositionReconciliationInputs;
            Reconciler.Instance.RecordPendingInput(LocalTick, pendingInput);

            // Invia lo stato completo usando lo stesso tick registrato localmente.
            NetworkManager.Instance.SendState(stateToSend, LocalTick);
            LocalTick++;
        }

        // Fuori dal ciclo a tick fisso: la riconciliazione viene eseguita una volta per Update().

        // Dopo l'avanzamento dei tick, applica caduta uovo e correzioni di HIT ora dovuti.
        Reconciler.Instance.ReconcileShot(LocalTick);

        // Replay degli input non confermati e correzione della posizione del pinguino locale.
        Reconciler.Instance.ReconcilePosition(
            ref _myPenguin._position,
            200f,
            NetDt,
            _myPenguin.ResolveObstacleOverlaps,
            isCurrentlyFrozen: _myPenguin.LocalFreeze.IsFrozen(LastSentInputTick)
        );


        
        // La fine della partita viene decisa esclusivamente dal server.
        base.Update(gameTime);
    }

    public uint getLocaltick()
    {
        return LocalTick;
    }
}