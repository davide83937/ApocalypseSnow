using System;
using System.Net;
using System.Net.Sockets;

namespace ApocalypseSnow;

public class NetworkManager
{
    private UdpClient _udpClient;
    private IPEndPoint _serverEndPoint;

    public NetworkManager(string ip, int port)
    {
        _udpClient = new UdpClient();
        _serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
    }

    public void SendState(StateStruct state)
    {
        // Prepariamo un buffer: 
        // 4 byte per Current + 4 per Old + 4 per mouseX + 4 per mouseY = 16 byte totali
        byte[] packet = new byte[16];

        // Convertiamo tutto in array di byte
        Buffer.BlockCopy(BitConverter.GetBytes((int)state.Current), 0, packet, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes((int)state.Old), 0, packet, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(state.mouseX ?? -1000), 0, packet, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(state.mouseY ?? -1000), 0, packet, 12, 4);

        // Invio via UDP
        _udpClient.Send(packet, packet.Length, _serverEndPoint);
    }
}