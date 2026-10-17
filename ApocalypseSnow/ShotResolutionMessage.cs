using System;
using System.Buffers.Binary;
using Microsoft.Xna.Framework;

namespace ApocalypseSnow;

public enum ShotOutcome : byte
{
    Expired = 1,
    Obstacle = 2,
    Penguin = 3
}

// Esito definitivo, con ACK e posizione del bersaglio al momento dell'HIT.
public readonly record struct ShotResolutionMessage(
    ShotKey Key, ShotOutcome Outcome, uint HitPlayerId, uint ServerTick,
    uint HitAck, Vector2 HitPosition, uint FlightTicks = 0)
{
    public const int PayloadLength = 33;

    // Decodifica l'esito autoritativo e la posizione della collisione.
    public static ShotResolutionMessage Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadLength)
            throw new ArgumentException("Invalid MsgShotResolved payload length.", nameof(payload));

        uint owner = BinaryPrimitives.ReadUInt32LittleEndian(payload[..4]);
        uint shotTick = BinaryPrimitives.ReadUInt32LittleEndian(payload[4..8]);
        ShotOutcome outcome = (ShotOutcome)payload[8];
        uint hitPlayerId = BinaryPrimitives.ReadUInt32LittleEndian(payload[9..13]);
        uint serverTick = BinaryPrimitives.ReadUInt32LittleEndian(payload[13..17]);
        uint hitAck = BinaryPrimitives.ReadUInt32LittleEndian(payload[17..21]);
        float hitX = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload[21..25]));
        float hitY = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(payload[25..29]));
        uint flightTicks = BinaryPrimitives.ReadUInt32LittleEndian(payload[29..33]);

        if (outcome is not (ShotOutcome.Expired or ShotOutcome.Obstacle or ShotOutcome.Penguin))
            throw new ArgumentException("Invalid authoritative shot outcome.", nameof(payload));
        if ((outcome == ShotOutcome.Penguin) != (hitPlayerId != 0))
            throw new ArgumentException("Inconsistent shot outcome and hit player ID.", nameof(payload));
        if (!float.IsFinite(hitX) || !float.IsFinite(hitY))
            throw new ArgumentException("Invalid authoritative hit position.", nameof(payload));

        return new ShotResolutionMessage(
            new ShotKey(owner, shotTick), outcome, hitPlayerId, serverTick,
            hitAck, new Vector2(hitX, hitY), flightTicks);
    }
}
