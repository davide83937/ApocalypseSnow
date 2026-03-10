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

    public event Action<uint, float, float> OnAuthReceived;
    public event Action<float, float, int> OnRemoteReceived;
    public event Action<float, float, int, ShotType> OnRemoteShotReceived; // mouseX, mouseY, charge, shotType
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

    private byte[] ReadExactPayload(int payloadLength)
    {
        byte[] payload = new byte[payloadLength];
        int read = 0;

        while (read < payloadLength)
        {
            int chunk = _stream.Read(payload, read, payloadLength - read);
            if (chunk <= 0)
                throw new IOException("Connessione chiusa durante la lettura del payload.");

            read += chunk;
        }

        return payload;
    }

    public void Receive()
    {
        if (_stream == null || _tcpClient == null || !_tcpClient.Connected)
            return;

        while (_tcpClient.Connected && _tcpClient.Available > 0)
        {
            int type = _stream.ReadByte();
            if (type == -1) break;

            switch (type)
            {
                case 8: // MsgSpawnEgg -> 12 byte payload
                    {
                        if (_tcpClient.Available < 12) return;
                        byte[] payload = ReadExactPayload(12);

                        System.Diagnostics.Debug.WriteLine($"EGG arrived, handler={(OnEggReceived == null ? "NULL" : "OK")}");
                        int id = BitConverter.ToInt32(payload, 0);
                        float x = BitConverter.ToSingle(payload, 4);
                        float y = BitConverter.ToSingle(payload, 8);
                        OnEggReceived?.Invoke(id, x, y);
                        break;
                    }

                case 4: // MsgAuthState -> 12 byte payload
                    {
                        if (_tcpClient.Available < 12) return;
                        byte[] payload = ReadExactPayload(12);

                        uint ackSeq = BitConverter.ToUInt32(payload, 0);
                        float x = BitConverter.ToSingle(payload, 4);
                        float y = BitConverter.ToSingle(payload, 8);
                        OnAuthReceived?.Invoke(ackSeq, x, y);
                        break;
                    }

                case 6: // MsgRemoteState -> 12 byte payload
                    {
                        if (_tcpClient.Available < 12) return;
                        byte[] payload = ReadExactPayload(12);

                        float x = BitConverter.ToSingle(payload, 0);
                        float y = BitConverter.ToSingle(payload, 4);
                        int mask = BitConverter.ToInt32(payload, 8);
                        OnRemoteReceived?.Invoke(x, y, mask);
                        break;
                    }

                case 7: // MsgRemoteShot -> 13 byte payload (4 + 4 + 4 + 1)
                    {
                        if (_tcpClient.Available < 13) return;
                        byte[] payload = ReadExactPayload(13);

                        float mx = BitConverter.ToSingle(payload, 0);
                        float my = BitConverter.ToSingle(payload, 4);
                        int charge = BitConverter.ToInt32(payload, 8);
                        ShotType shotType = (ShotType)payload[12];

                        OnRemoteShotReceived?.Invoke(mx, my, charge, shotType);
                        break;
                    }

                case 9: // MsgTickAlign -> 12 byte payload
                    {
                        if (_tcpClient.Available < 12) return;
                        byte[] payload = ReadExactPayload(12);

                        uint startTick = BitConverter.ToUInt32(payload, 0);
                        uint tickHz = BitConverter.ToUInt32(payload, 4);

                        StartTick = startTick;
                        ServerTickHz = tickHz;
                        break;
                    }

                case 10: // MsgSpawnObstacle -> 12 byte payload
                    {
                        if (_tcpClient.Available < 12) return;
                        byte[] payload = ReadExactPayload(12);

                        Console.WriteLine($"Ricevuto messaggio 10. Payload length: {payload.Length}");
                        float x = BitConverter.ToSingle(payload, 0);
                        float y = BitConverter.ToSingle(payload, 4);
                        OnObstacleReceived?.Invoke(x, y);
                        break;
                    }

                default:
                    Console.WriteLine($"Tipo messaggio sconosciuto ricevuto: {type}");
                    return;
            }
        }
    }

    public void SendShot(ShotStruct shot)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        byte[] packet = new byte[17];
        packet[0] = (byte)shot.Type;

        Buffer.BlockCopy(BitConverter.GetBytes(GameSession.LocalTick), 0, packet, 1, 4);
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