using System;
using System.Collections.Generic;
using System.IO;

namespace ApocalypseSnow;

/// <summary>
/// Reassembles complete application messages from the TCP byte stream.
/// A fragmented payload must never consume its message type prematurely.
/// </summary>
public sealed class NetworkPacketFramer
{
    private readonly List<byte> _pending = new(256);

    public void Append(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
            _pending.Add(value);
    }

    public bool TryRead(out byte type, out byte[] payload)
    {
        type = 0;
        payload = Array.Empty<byte>();
        if (_pending.Count == 0)
            return false;

        type = _pending[0];
        int payloadSize = type switch
        {
            4 or 6 or 8 or 9 or 10 => 12,
            7 => RemoteShotMessage.PayloadLength,
            11 => ShotResolutionMessage.PayloadLength,
            _ => throw new InvalidDataException($"Unknown server message type: {type}")
        };
        int packetSize = payloadSize + 1;
        if (_pending.Count < packetSize)
            return false;

        payload = _pending.GetRange(1, payloadSize).ToArray();
        _pending.RemoveRange(0, packetSize);
        return true;
    }
}
