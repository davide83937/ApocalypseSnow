using System;
using System.Buffers.Binary;

namespace ApocalypseSnow;

// Dati del tiro ricevuti dal server per il pinguino remoto.
public readonly record struct RemoteShotMessage(
    float TargetX, float TargetY, int Charge, ShotType Type, ShotKey Key)
{
    // MsgRemoteShot is 22 bytes including the leading message type byte.
    public const int PayloadLength = 21;

    // Legge il tiro remoto dal payload TCP.
    public static RemoteShotMessage Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadLength)
            throw new ArgumentException("Invalid MsgRemoteShot payload length.", nameof(payload));

        return new RemoteShotMessage(
            BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload[..4])),
            BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload[4..8])),
            BinaryPrimitives.ReadInt32LittleEndian(payload[8..12]),
            (ShotType)payload[12],
            new ShotKey(
                BinaryPrimitives.ReadUInt32LittleEndian(payload[13..17]),
                BinaryPrimitives.ReadUInt32LittleEndian(payload[17..21])));
    }
}
