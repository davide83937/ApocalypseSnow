using System;
using System.Diagnostics;
using System.Linq;
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

    //copia del tick locale, che incrementiamo ad ogni update, e del tick accumulato, che invece incrementiamo con il delta time
    //e usiamo per capire quando è ora di fare un nuovo tick di gioco (e quindi inviare un nuovo input al server)
    public static uint LocalTick { get; private set; } = 0;
    // Questo accumulatore ci serve per gestire il tick rate del gioco, che è indipendente dal frame rate. stessa logica del pinguino, per ora la metto qui
    private float _tickAcc = 0f;
    private uint _localPlayerId;
    private bool _hasAuthSnapshot;
    private Events.AuthPositionSnapshot _latestAuthSnapshot;
    private readonly System.Collections.Generic.List<(uint HitTick, uint Ack, Vector2 Position, ShotKey Key)> _deferredHitCorrections = new();
    

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
        string host = Environment.GetEnvironmentVariable("APOCALYPSE_SERVER_HOST") ?? "127.0.0.1";
        int port = int.TryParse(Environment.GetEnvironmentVariable("APOCALYPSE_SERVER_PORT"), out int parsedPort) ? parsedPort : 8080;
        networkManager = new NetworkManager(_game, host, port);
        Console.WriteLine($"[NETWORK_TEST] connecting to {host}:{port}");
        //networkManager = new NetworkManager(this, "192.168.1.27", 8080);
        //networkManager = new NetworkManager(this, "7.tcp.eu.ngrok.io", 13297);
        //networkManager = new NetworkManager(this, "3.125.188.168", 13297);
        //networkManager = new NetworkManager(this, "18.192.31.30", 11179);
        state = "IN ATTESA DI UN AVVERSARIO...";
 
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

        //aggiunti questi due per tenere traccia del tick del server e del client, utili per la reconciler
        LocalTick = 1; // Il server inizializza LastSeqN=0: la prima sequenza valida è 1.
        _localPlayerId = ack.PlayerId;
        _hasAuthSnapshot = false;
        _deferredHitCorrections.Clear();
        _tickAcc = 0f;
        Reconciler.Instance.Reset();
        //Reconciler.Instance.SetNextSeq(1);

        NetDt = 1f / (float)ack.Heartz;

        _bluePlatform = new BasePlatform(_game, new Vector2(ack.SpawnX, ack.SpawnY), "blueP", bluePathPlatform);
        _redPlatform =  new BasePlatform(_game, new Vector2(ack.OpponentSpawnX, ack.OpponentSpawnY), "redP", redPathPlatform);
        _myPenguin = new Penguin(_game,"penguin", _bluePlatform._position, Vector2.Zero, movements, NetDt, ack.PlayerId);
        _redPenguin = new Penguin(_game,"penguinRed", _redPlatform._position, Vector2.Zero, movementsRed, NetDt, ack.PlayerId == 1 ? 2u : 1u);
        //_obstacle = new Obstacle(_game, new Vector2(100, 100));
        _eggsEvent = new EggsEvent(_game, _myPenguin, _redPenguin);
        _events = new Events(_game, _redPenguin);

        // AGGIUNTA AI COMPONENTI
        // Nota: MonoGame è solitamente thread-safe per Components.Add, 
        // ma è buona norma farlo qui appena ricevuti i dati.
        _game.Components.Add(_myPenguin);
        _game.Components.Add(_redPenguin);
        _game.Components.Add(_bluePlatform);
        _game.Components.Add(_redPlatform);
        //_game.Components.Add(_obstacle);
        _game.Components.Add(_eggsEvent);
        _game.Components.Add(_events);
    }
    
    public void EndSession()
    {
        // 1. Rimuovi tutti i componenti che la sessione ha aggiunto al gioco
        CollisionManager.Instance.ClearAll();
        Reconciler.Instance.Reset();
        _deferredHitCorrections.Clear();
        
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
        // ✅ ricevi SEMPRE


        // se non è iniziato il match, stop qui
        if (state != null || _myPenguin == null || _events == null)
            return;
        
        NetworkManager.Instance?.Receive();

        // Conserva l'ultimo ACK e applica gli HIT prima di registrare altri input.
        Reconciler.Instance.GetLatest(_events._authPositionQueue, auth =>
        {
            if (!_hasAuthSnapshot || auth.Ack >= _latestAuthSnapshot.Ack)
            {
                _latestAuthSnapshot = auth;
                _hasAuthSnapshot = true;
                Reconciler.Instance.OnServerAuth(auth.Ack, auth.Position);
            }
        });
        ApplyShotResolutions();
        ApplyDueHitCorrections();

        // ✅ tick fisso (stesso dt della sim di rete)
        _tickAcc += (float)gameTime.ElapsedGameTime.TotalSeconds;
        while (_tickAcc >= NetDt)
        {
            _tickAcc -= NetDt;
            Debug.WriteLine($"LocalTick++: {LocalTick}");

            var st = _myPenguin._penguinInputHandler._stateStruct;
            if (_myPenguin.LocalFreeze.IsFrozen(LocalTick))
            {
                st.Current |= StateList.Freezing;
                st.Current &= ~(StateList.Up | StateList.Down | StateList.Left | StateList.Right |
                                StateList.Moving | StateList.ShootLeft | StateList.ShootRight |
                                StateList.Reload | StateList.TakingEgg | StateList.PuttingEgg);
            }
            else
                st.Current &= ~StateList.Freezing;

            // 2) Conserva anche gli stati che bloccano il movimento per il replay.
            StateList moveMask = st.Current & (
                StateList.Up | StateList.Down | StateList.Left | StateList.Right |
                StateList.Reload | StateList.Freezing | StateList.TakingEgg | StateList.PuttingEgg);

            // 3) record + send usando QUESTO tick
            Reconciler.Instance.Record(LocalTick, moveMask);
            NetworkManager.Instance.SendState(st, LocalTick);
            LocalTick++;
        }

        //SUPER NOTA: dentro il while siamo a 30 HZ, fuori dal while ma dentro questo Update siamo a 60 FPS

        ApplyDueHitCorrections();

        // Solo ora chiami GetLatest per prendere l'ultimo stato arrivato dal server
        Reconciler.Instance.Apply(
            ref _myPenguin._position,
            200f,
            NetDt,
            candidatePos => CollisionManager.Instance.wouldCollide(
                "penguin",
                candidatePos.X + 48f,
                candidatePos.Y + 64f,
                96,
                64,
                "obstacle"
            )
        );


        
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

    // Corregge la posizione usando l'ACK dell'HIT e blocca gli input gia predetti.
    private void ApplyShotResolutions()
    {
        while (_events.PendingShotResolutions.Count > 0)
        {
            ShotResolutionMessage resolution = _events.PendingShotResolutions.Dequeue();
            ShotTrace.Log($"[SHOT_TRACE] role=CLIENT event=SERVER_RESULT key={resolution.Key} outcome={resolution.Outcome} localTick={LocalTick} serverTick={resolution.ServerTick} hitAck={resolution.HitAck} hitPlayer={resolution.HitPlayerId} utc={DateTime.UtcNow:O}");

            // La palla conclusa non deve produrre altre collisioni predette.
            Ball ball = _game.Components.OfType<Ball>().FirstOrDefault(b => b.ShotKey == resolution.Key);
            if (ball != null)
            {
                CollisionManager.Instance.removeObject(ball._tag);
                _game.Components.Remove(ball);
            }

            bool hitMe = resolution.Outcome == ShotOutcome.Penguin && resolution.HitPlayerId == _localPlayerId;
            bool hitOpponent = resolution.Outcome == ShotOutcome.Penguin && !hitMe;
            // Server ACK remains the anchor for authoritative position reconciliation;
            // local freeze is anchored to the remote ball's observed start + server flight.
            uint localHitTick = LocalTick;
            bool hasRemoteStart = _events.TryTakeRemoteShotStart(resolution.Key, out uint remoteStartTick);
            if (hitMe && hasRemoteStart)
            {
                localHitTick = remoteStartTick + resolution.FlightTicks;
                ShotTrace.Log($"[SHOT_ALIGN] key={resolution.Key} remoteStart={remoteStartTick} flightTicks={resolution.FlightTicks} reconstructedHit={localHitTick} now={LocalTick} remaining={Math.Max(0L, (long)localHitTick + 90 - LocalTick)} hitAck={resolution.HitAck}");
            }
            else if (hitMe)
                ShotTrace.Log($"[SHOT_ALIGN] key={resolution.Key} missing_remote_start; fallback=now localTick={LocalTick}");
            uint freezeEnd = (hitMe ? localHitTick : resolution.HitAck) + 90;

            // La conferma o smentita del server chiude anche la prediction sul remoto.
            bool remoteWasPredictedFrozen = _redPenguin.LocalFreeze.IsFrozen(LocalTick);
            _redPenguin.LocalFreeze.Resolve(resolution.Key, false, 0);
            if (hitOpponent)
            {
                _redPenguin._position = resolution.HitPosition;
                _redPenguin._penguinColliderHandler.HandleHitByBall(ref _redPenguin._myEgg);
            }
            else if (remoteWasPredictedFrozen &&
                     !_redPenguin.LocalFreeze.IsFrozen(LocalTick) &&
                     _events.HasRemotePosition)
            {
                _redPenguin._position = _events.LatestRemotePosition;
            }

            if (hitMe)
                _myPenguin.LocalFreeze.ConfirmLocal(resolution.Key, localHitTick);
            else
                _myPenguin.LocalFreeze.Resolve(resolution.Key, false, 0);
            if (!hitMe)
                continue;

            // L'uovo cade solo quando il server conferma l'HIT.
            _myPenguin._penguinColliderHandler.HandleHitByBall(ref _myPenguin._myEgg);
            Reconciler.Instance.BlockMovement(localHitTick + 1, freezeEnd);
            uint ack = resolution.HitAck;
            Vector2 position = resolution.HitPosition;
            if (_hasAuthSnapshot && _latestAuthSnapshot.Ack >= ack)
            {
                ack = _latestAuthSnapshot.Ack;
                position = _latestAuthSnapshot.Position;
            }

            // Se l'HIT e' ancora futuro sulla timeline locale, non anticipare lo snap.
            if (localHitTick > LocalTick)
            {
                _deferredHitCorrections.Add((localHitTick, ack, position, resolution.Key));
                ShotTrace.Log($"[HIT_RECON] deferred key={resolution.Key} hitTick={localHitTick} now={LocalTick} ack={ack}");
            }
            else
                ApplyHitCorrection(resolution.Key, ack, position);
        }
    }

    private void ApplyDueHitCorrections()
    {
        for (int i = _deferredHitCorrections.Count - 1; i >= 0; --i)
        {
            var hit = _deferredHitCorrections[i];
            if (hit.HitTick > LocalTick)
                continue;
            _deferredHitCorrections.RemoveAt(i);
            ApplyHitCorrection(hit.Key, hit.Ack, hit.Position);
        }
    }

    private void ApplyHitCorrection(ShotKey key, uint ack, Vector2 position)
    {
        // Un ACK piu' recente vale piu' di una vecchia posizione d'impatto.
        if (_hasAuthSnapshot && _latestAuthSnapshot.Ack >= ack)
        {
            ack = _latestAuthSnapshot.Ack;
            position = _latestAuthSnapshot.Position;
        }
        Vector2 before = _myPenguin._position;
        Reconciler.Instance.OnServerAuth(ack, position);
        // Il replay degli input pendenti e' eseguito nel normale Update.
        ShotTrace.Log($"[HIT_RECON] key={key} ack={ack} before={before} auth={position} now={LocalTick}");
    }

    public uint getLocaltick()
    {
        return LocalTick;
    }
}