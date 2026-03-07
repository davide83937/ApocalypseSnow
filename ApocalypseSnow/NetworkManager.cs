using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Encoding = System.Text.Encoding;

namespace ApocalypseSnow;

public class NetworkManager : GameComponent
{
    private TcpClient _tcpClient;
    private NetworkStream _stream;
    private readonly string _ip;
    private readonly int _port;
    public event Action<uint, float, float> OnAuthReceived;
    public event Action<float, float, int> OnRemoteReceived;
    public event Action<float, float, int> OnRemoteShotReceived; // mouseX, mouseY, charge
    public event Action<int, float, float> OnEggReceived;

    public uint StartTick { get; private set; }
    public uint ServerTickHz { get; private set; }

    private static NetworkManager _instance;

    
    public static NetworkManager Instance
    {
        get
        {
            return _instance;
        }
        private set => _instance = value; // Permettiamo di scriverlo internamente
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
            _tcpClient.NoDelay = true; // Disabilita Nagle per ridurre la latenza
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

        // Creiamo un pacchetto fisso di 9 byte (1 tipo + 8 dati)
        byte[] packet = new byte[9];
        packet[0] = (byte)join.Type;

        // Copiamo il nome nel buffer a partire dall'indice 1 (max 8 caratteri)
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(join.playerName);
        int length = Math.Min(nameBytes.Length, 8);
        Buffer.BlockCopy(nameBytes, 0, packet, 1, length);

        _stream.Write(packet, 0, packet.Length);
        _stream.Flush();
    }
    

    public void SendState(StateStruct state, uint seq)
    {
        // Se non siamo connessi, non inviamo nulla
        if (_stream == null || !_tcpClient.Connected) return;

        // La dimensione del pacchetto passa da 13 a 21 byte per includere X e Y
        // Pacchetto: 1 (Tipo) + 4 (Mask) + 4 (Seq) + 4 (DeltaTime) + 4 (X) + 4 (Y) = 21 byte
        byte[] packet = new byte[9];
    
        // 1. Inseriamo il tipo di messaggio (1 byte)
        packet[0] = (byte)MessageType.State; 
    
        //Console.WriteLine(_stateSequence);
        //Console.WriteLine($"X after Normalization: {position.X}, Y after Normalization: {position.Y}");
        // 2. Inseriamo i dati nel buffer (Little Endian, come si aspetta Go)
        Buffer.BlockCopy(BitConverter.GetBytes((int)state.Current), 0, packet, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(seq), 0, packet, 5, 4);

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
    
    // Aggiungi questo metodo in NetworkManager.cs
    private byte[] ReadExactly(int count)
    {
        byte[] buffer = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = _stream.Read(buffer, offset, count - offset);
            if (read <= 0) throw new Exception("Connessione persa");
            offset += read;
        }
        return buffer;
    }
    

   
    public void Receive()
    {
        // Verifichiamo se ci sono abbastanza byte per ALMENO un messaggio (1 + 12 = 13 byte)
        // Questo evita che ReadExactly blocchi il thread principale (e quindi la finestra)
        while (_tcpClient.Connected && _tcpClient.Available >= 13)
        {
            int type = _stream.ReadByte();
            if (type == -1) break;

            // Ora siamo sicuri che i 12 byte del payload siano già nel buffer di rete
            byte[] payload = new byte[12];
            int read = 0;
            while (read < 12)
            {
                read += _stream.Read(payload, read, 12 - read);
            }

            if (type == 8) // MsgSpawnEgg
            {
                System.Diagnostics.Debug.WriteLine($"EGG arrived, handler={(OnEggReceived == null ? "NULL" : "OK")}");
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
                OnAuthReceived?.Invoke(ackSeq, x, y);
            }
            else if (type == 6) // MsgRemoteState
            {
                float x = BitConverter.ToSingle(payload, 0);
                float y = BitConverter.ToSingle(payload, 4);
                int mask = BitConverter.ToInt32(payload, 8);
                OnRemoteReceived?.Invoke(x, y, mask);
            }
            else if (type == 7) // MsgRemoteShot
            {
             
                float mx = BitConverter.ToSingle(payload, 0);
                float my = BitConverter.ToSingle(payload, 4);
                int charge = BitConverter.ToInt32(payload, 8);
                OnRemoteShotReceived?.Invoke(mx, my, charge);
            }
            else if (type == 9) // MsgTickAlign
            {
                uint startTick = BitConverter.ToUInt32(payload, 0);
                uint tickHz = BitConverter.ToUInt32(payload, 4);
                // payload[8..12] = reserved/padding

                StartTick = startTick;
                ServerTickHz = tickHz;
            }
        }
    }

    
    public void SendShot(ShotStruct shot)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        // Pacchetto: 1 byte (Type) + 4 byte (mouseX) + 4 byte (mouseY) + 4 byte (charge) = 13 byte
        byte[] packet = new byte[17];

        // 1. Inseriamo il tipo all'inizio
        packet[0] = (byte)shot.Type;

        // 2. Inseriamo i dati sfalsando correttamente l'offset di destinazione
        Buffer.BlockCopy(BitConverter.GetBytes(GameSession.LocalTick), 0, packet, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(shot.mouseX), 0, packet, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(shot.mouseY), 0, packet, 9, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(shot.charge), 0, packet, 13, 4);


        try
        {
            _stream.Write(packet, 0, packet.Length);
            //_stream.Flush();
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

        byte[] buffer = new byte[25];
        int totalRead = 0;
    
        // Leggiamo fino a riempire il buffer da 13 byte
        while (totalRead < 25)
        {
            int read = _stream.Read(buffer, totalRead, 25 - totalRead);
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
            OpponentSpawnY = BitConverter.ToSingle(buffer, 17),  // Lettura nuovi dati
            Heartz =  BitConverter.ToUInt32(buffer, 21)
        };

    }
    
  

    public void Disconnect()
    {
        try
        {
            _stream?.Close();
            _tcpClient?.Close();
            Console.WriteLine("Connessione TCP chiusa correttamente.");
            _instance = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore durante la disconnessione: {ex.Message}");
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream?.Close();
            _tcpClient?.Close();
            _instance = null;
        }
        base.Dispose(disposing);
    }
}