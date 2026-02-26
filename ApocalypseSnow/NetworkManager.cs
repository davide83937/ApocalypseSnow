using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Encoding = System.Text.Encoding;

namespace ApocalypseSnow;

public class NetworkManager : IDisposable
{
    private TcpClient _tcpClient;
    private NetworkStream _stream;
    private readonly string _ip;
    private readonly int _port;

    public NetworkManager(string ip, int port)
    {
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore di connessione: {ex.Message}");
        }
    }
    

    public void SendState(StateStruct state)
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
    }

    public void SendShot(ShotStruct shot)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        byte[] packet = new byte[9];
        packet[0] = (byte)shot.Type;
        Buffer.BlockCopy(BitConverter.GetBytes((int)shot.mouseX), 0, packet, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((int)shot.mouseY), 0, packet, 4, 4);

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
    
    public void SendJoin(JoinStruct join)
    {
        if (_stream == null || !_tcpClient.Connected) return;

        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8))
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
    
    public JoinAckStruct WaitForJoinAck()
    {
        if (_stream == null || !_tcpClient.Connected) 
            throw new Exception("Non connesso al server");

        byte[] buffer = new byte[13];
        int totalRead = 0;
    
        // Leggiamo fino a riempire il buffer da 13 byte
        while (totalRead < 13)
        {
            int read = _stream.Read(buffer, totalRead, 13 - totalRead);
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
            SpawnY = BitConverter.ToSingle(buffer, 9)
        };
    }

    public void Dispose()
    {
        _stream?.Close();
        _tcpClient?.Close();
    }
}