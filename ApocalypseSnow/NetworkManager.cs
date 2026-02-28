using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Encoding = System.Text.Encoding;

namespace ApocalypseSnow;

public class NetworkManager : GameComponent, IDisposable
{
    private TcpClient _tcpClient;
    private NetworkStream _stream;
    private readonly string _ip;
    private readonly int _port;
    private uint _stateSequence = 0;
    public event Action<uint, float, float> OnAuthReceived;
    public event Action<float, float, int> OnRemoteReceived;
    public event Action<int, int, int> OnRemoteShotReceived; // mouseX, mouseY, charge
    public event Action<int, float, float> OnEggReceived;

    private static NetworkManager _instance;

    
    public static NetworkManager Instance
    {
        get
        {
            if (_instance == null)
                throw new Exception("NetworkManager deve essere inizializzato in Game1 prima dell'uso!");
            return _instance;
        }
    }

    // Il costruttore deve accettare 'Game' e passarlo al padre tramite base(game)
    public NetworkManager(Game game, string ip, int port) : base(game)
    {
        if (_instance != null)
            throw new Exception("Puoi creare solo una istanza di NetworkManager!");
        _instance = this;
        _ip = ip;
        _port = port;
        Connect();
    }


    public void Connect()
    {
        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(_ip, _port);
            _stream = _tcpClient.GetStream();
            Console.WriteLine("Connessione OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore di connessione: {ex.Message}");
        }
    }
    
    public void SendJoin(JoinStruct join)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
        using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8))
        {
            // 1. Scriviamo il tipo (1 byte)
            writer.Write((byte)join.Type);

            // 2. Scriviamo la stringa
            // BinaryWriter aggiunge automaticamente un prefisso con la lunghezza
            writer.Write(join.playerName);

            byte[] packet = ms.ToArray();
            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();
        }
    }

    /*public void SendState(StateStruct state)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        byte[] packet = new byte[9];
        packet[0] = (byte)state.Type;
        Buffer.BlockCopy(BitConverter.GetBytes((int)state.Current), 0, packet, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((int)state.Old), 0, packet, 4, 4);

        try
        {
            _stream.Write(packet, 0, packet.Length);
            _stream.Flush(); // Assicura che i dati vengano inviati subito
        }
        catch (IOException)
        {
            // Gestione eventuale disconnessione
            Connect(); 
        }
    }*/
    

    public void SendState(StateStruct state, float deltaTime, Vector2 position)
    {
        // Se non siamo connessi, non inviamo nulla
        if (_stream == null || !_tcpClient.Connected) return;

        // La dimensione del pacchetto passa da 13 a 21 byte per includere X e Y
        // Pacchetto: 1 (Tipo) + 4 (Mask) + 4 (Seq) + 4 (DeltaTime) + 4 (X) + 4 (Y) = 21 byte
        byte[] packet = new byte[21];
    
        // 1. Inseriamo il tipo di messaggio (1 byte)
        packet[0] = (byte)MessageType.State; 
    
        // Incrementiamo il numero di sequenza per l'ordine dei pacchetti
        _stateSequence++;
        //Console.WriteLine(_stateSequence);
        //Console.WriteLine($"X after Normalization: {position.X}, Y after Normalization: {position.Y}");
        // 2. Inseriamo i dati nel buffer (Little Endian, come si aspetta Go)
        Buffer.BlockCopy(BitConverter.GetBytes((int)state.Current), 0, packet, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(_stateSequence), 0, packet, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(deltaTime), 0, packet, 9, 4);
    
        // 3. Aggiungiamo le coordinate appena calcolate localmente
        Buffer.BlockCopy(BitConverter.GetBytes(position.X), 0, packet, 13, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(position.Y), 0, packet, 17, 4);

        try 
        {
            // 4. Inviamo il pacchetto al server
            _stream.Write(packet, 0, packet.Length);
            // Evitiamo Flush() qui se viene chiamato ad ogni frame per non bloccare il thread, 
            // Write() sul NetworkStream invia già rapidamente in background
        } 
        catch (IOException) 
        { 
            // In caso di errore di rete, tentiamo la riconnessione
            Connect(); 
        }
    }
    
    public void Receive()
    {
        while (_tcpClient.Connected && _stream.DataAvailable)
        {
            int type = _stream.ReadByte();
            if (type == -1) break;

            byte[] payload = new byte[12];
            _stream.Read(payload, 0, 12);

         
           if (type == 8) // MsgSpawnEgg
            {
                int id = BitConverter.ToInt32(payload, 0);
                float x = BitConverter.ToSingle(payload, 4);
                float y = BitConverter.ToSingle(payload, 8);
                OnEggReceived?.Invoke(id, x, y);
            }
            else if (type == 4) // MsgAuthState
            {
                uint ackSeq = BitConverter.ToUInt32(payload, 0);
                float x = BitConverter.ToSingle(payload, 4);
                float y = BitConverter.ToSingle(payload, 8);
                // Lancia l'evento a chiunque sia in ascolto
                OnAuthReceived?.Invoke(ackSeq, x, y);
            }
            else if (type == 6) // MsgRemoteState
            {
                float x = BitConverter.ToSingle(payload, 0);
                float y = BitConverter.ToSingle(payload, 4);
                int mask = BitConverter.ToInt32(payload, 8);
                // Lancia l'evento a chiunque sia in ascolto
                OnRemoteReceived?.Invoke(x, y, mask);
            }
            else if (type == 7) // MsgRemoteShot (Ricevuto dal server per l'avversario)
            {
                int mx = BitConverter.ToInt32(payload, 0);
                int my = BitConverter.ToInt32(payload, 4);
                int charge = BitConverter.ToInt32(payload, 8);
                // Lancia l'evento
                OnRemoteShotReceived?.Invoke(mx, my, charge);
            }
            
        }
    }
    
    /*public void Receive(Action<uint, float, float> onAuth, Action<float, float, int> onRemote)
    {
        // Verifichiamo se ci sono dati pronti nello stream
        while (_tcpClient.Connected && _stream.DataAvailable)
        {
            int type = _stream.ReadByte();
            if (type == -1) break;

            byte[] payload = new byte[12]; // La maggior parte dei messaggi sono 1+12 byte
            _stream.Read(payload, 0, 12);

            if (type == 4) // MsgAuthState (Il mio pinguino autoritativo)
            {
                uint ackSeq = BitConverter.ToUInt32(payload, 0);
                float x = BitConverter.ToSingle(payload, 4);
                float y = BitConverter.ToSingle(payload, 8);
                onAuth?.Invoke(ackSeq, x, y);
            }
            else if (type == 6) // MsgRemoteState (Il pinguino avversario)
            {
                float x = BitConverter.ToSingle(payload, 0);
                float y = BitConverter.ToSingle(payload, 4);
                int mask = BitConverter.ToInt32(payload, 8);
                onRemote?.Invoke(x, y, mask);
            }
        }
    }*/

    // Aggiungi questo metodo per inviare la richiesta
    public void SendEggAction(int action, int eggId, uint playerId)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        byte[] packet = new byte[13];
        packet[0] = 9; // MsgEggAction
        Buffer.BlockCopy(BitConverter.GetBytes(action), 0, packet, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(eggId), 0, packet, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(playerId), 0, packet, 9, 4);

        _stream.Write(packet, 0, packet.Length);
        _stream.Flush();
    }
    
    public void SendShot(ShotStruct shot)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        // Pacchetto: 1 byte (Type) + 4 byte (mouseX) + 4 byte (mouseY) + 4 byte (charge) = 13 byte
        byte[] packet = new byte[13];
    
        // 1. Inseriamo il tipo all'inizio
        packet[0] = (byte)shot.Type;
    
        // 2. Inseriamo i dati sfalsando correttamente l'offset di destinazione (1, 5, 9)
        Buffer.BlockCopy(BitConverter.GetBytes(shot.mouseX), 0, packet, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(shot.mouseY), 0, packet, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(shot.charge), 0, packet, 9, 4);

        try
        {
            _stream.Write(packet, 0, packet.Length);
            _stream.Flush();
        }
        catch (IOException)
        {
            Connect();
        }
    }
    
    public JoinAckStruct WaitForJoinAck()
    {
        int tentativi = 0;
        while (!_tcpClient.Connected && tentativi < 50) 
        {
            Thread.Sleep(100); // Aspetta 100ms
            tentativi++;
        }
        if (_stream == null || !_tcpClient.Connected) 
            throw new Exception("Non connesso al server");

        byte[] buffer = new byte[21];
        int totalRead = 0;
    
        // Leggiamo fino a riempire il buffer da 13 byte
        while (totalRead < 21)
        {
            int read = _stream.Read(buffer, totalRead, 21 - totalRead);
            if (read <= 0) throw new Exception("Server disconnesso durante l'attesa del JoinAck");
            totalRead += read;
        }

        // Verifichiamo che il tipo sia corretto (JoinAck = 5)
        if (buffer[0] != (byte)MessageType.JoinAck)
            throw new Exception($"Ricevuto tipo messaggio inatteso: {buffer[0]}");

        // Estraiamo i dati dal buffer binario (Little Endian come in Go)
        return new JoinAckStruct
        {
            Type = (MessageType)buffer[0],
            PlayerId = BitConverter.ToUInt32(buffer, 1),
            SpawnX = BitConverter.ToSingle(buffer, 5),
            SpawnY = BitConverter.ToSingle(buffer, 9),
            OpponentSpawnX = BitConverter.ToSingle(buffer, 13), // Lettura nuovi dati
            OpponentSpawnY = BitConverter.ToSingle(buffer, 17)  // Lettura nuovi dati
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
        base.Dispose(disposing);
    }
}