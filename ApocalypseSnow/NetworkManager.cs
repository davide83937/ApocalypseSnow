using System;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public class NetworkManager : GameComponent
{
    private TcpClient _tcpClient;
    private NetworkStream _stream;
    private readonly string _ip;
    private readonly int _port;
    private readonly NetworkPacketFramer _packetFramer = new();
    private readonly byte[] _incomingBuffer = new byte[4096];

    public event Action<uint, float, float> OnAuthStateReceived; //posizione autoritativa del player locale
    public event Action<uint, float, int, ShotType> OnAuthShotReceived; //shot autoritativo del player locale (seqN, mouseY, charge, shotType) rivedere la firma

    public event Action<float, float, int> OnRemoteStateReceived;
    public event Action<RemoteShotMessage> OnRemoteShotReceived; // authoritative remote shot metadata
    public event Action<ShotResolutionMessage> OnShotResolvedReceived;

    public event Action<int, float, float> OnEggReceived;
    public event Action<float, float> OnObstacleReceived;

    public uint StartTick { get; private set; }
    public uint ServerTickHz { get; private set; }

    private static NetworkManager _instance;

    public static NetworkManager Instance
    {
        get { return _instance; }
        private set => _instance = value;
    }

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
            _tcpClient.NoDelay = true;
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

        byte[] packet = new byte[9];
        packet[0] = (byte)join.Type;

        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(join.playerName);
        int length = Math.Min(nameBytes.Length, 8);
        Buffer.BlockCopy(nameBytes, 0, packet, 1, length);

        _stream.Write(packet, 0, packet.Length);
        _stream.Flush();
    }

    public void SendState(StateStruct state, uint seq)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        byte[] packet = new byte[9];
        packet[0] = (byte)MessageType.State;

        Buffer.BlockCopy(BitConverter.GetBytes((int)state.Current), 0, packet, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(seq), 0, packet, 5, 4);

        try
        {
            _stream.Write(packet, 0, packet.Length);
        }
        catch (IOException)
        {
            Connect();
        }
    }

    // Riceve i messaggi completi e li passa agli handler.
    public void Receive()
    {
        if (_stream == null || _tcpClient == null || !_tcpClient.Connected)
            return;

        // Read only bytes already available: do not stall the MonoGame update
        // when a TCP packet contains only part of an application message.
        while (_tcpClient.Available > 0)
        {
            int count = Math.Min(_tcpClient.Available, _incomingBuffer.Length);
            int read = _stream.Read(_incomingBuffer, 0, count);
            if (read <= 0)
                throw new IOException("Connessione chiusa durante la ricezione.");
            _packetFramer.Append(_incomingBuffer.AsSpan(0, read));
        }

        while (_packetFramer.TryRead(out byte type, out byte[] payload))
        {
            switch (type)
            {
                case 8: // MsgSpawnEgg
                    {
                        int id = BitConverter.ToInt32(payload, 0);
                        float x = BitConverter.ToSingle(payload, 4);
                        float y = BitConverter.ToSingle(payload, 8);
                        OnEggReceived?.Invoke(id, x, y);
                        break;
                    }

                case 4: // MsgAuthState
                    {
                        uint ackSeq = BitConverter.ToUInt32(payload, 0);
                        float x = BitConverter.ToSingle(payload, 4);
                        float y = BitConverter.ToSingle(payload, 8);
                        OnAuthStateReceived?.Invoke(ackSeq, x, y);
                        break;
                    }

                case 6: // MsgRemoteState
                    {
                        float x = BitConverter.ToSingle(payload, 0);
                        float y = BitConverter.ToSingle(payload, 4);
                        int mask = BitConverter.ToInt32(payload, 8);
                        OnRemoteStateReceived?.Invoke(x, y, mask);
                        break;
                    }

                case 7: // MsgRemoteShot
                    OnRemoteShotReceived?.Invoke(RemoteShotMessage.Parse(payload));
                    break;

                case 11: // MsgShotResolved
                    OnShotResolvedReceived?.Invoke(ShotResolutionMessage.Parse(payload));
                    break;

                case 9: // MsgTickAlign (reserved; no server sender yet)
                    StartTick = BitConverter.ToUInt32(payload, 0);
                    ServerTickHz = BitConverter.ToUInt32(payload, 4);
                    break;

                case 10: // MsgSpawnObstacle
                    {
                        float x = BitConverter.ToSingle(payload, 0);
                        float y = BitConverter.ToSingle(payload, 4);
                        OnObstacleReceived?.Invoke(x, y);
                        break;
                    }
            }
        }
    }

    public void SendShot(ShotStruct shot)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        byte[] packet = new byte[17];
        packet[0] = (byte)shot.Type;

        Buffer.BlockCopy(BitConverter.GetBytes(shot.localTick), 0, packet, 1, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(shot.mouseX), 0, packet, 5, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(shot.mouseY), 0, packet, 9, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(shot.charge), 0, packet, 13, 4);

        try
        {
            _stream.Write(packet, 0, packet.Length);
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
            Thread.Sleep(100);
            tentativi++;
        }

        if (_stream == null || !_tcpClient.Connected)
            throw new Exception("Non connesso al server");

        byte[] buffer = new byte[25];
        int totalRead = 0;

        while (totalRead < 25)
        {
            int read = _stream.Read(buffer, totalRead, 25 - totalRead);
            if (read <= 0) throw new Exception("Server disconnesso durante l'attesa del JoinAck");
            totalRead += read;
        }

        if (buffer[0] != (byte)MessageType.JoinAck)
            throw new Exception($"Ricevuto tipo messaggio inatteso: {buffer[0]}");

        return new JoinAckStruct
        {
            Type = (MessageType)buffer[0],
            PlayerId = BitConverter.ToUInt32(buffer, 1),
            SpawnX = BitConverter.ToSingle(buffer, 5),
            SpawnY = BitConverter.ToSingle(buffer, 9),
            OpponentSpawnX = BitConverter.ToSingle(buffer, 13),
            OpponentSpawnY = BitConverter.ToSingle(buffer, 17),
            Heartz = BitConverter.ToUInt32(buffer, 21)
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