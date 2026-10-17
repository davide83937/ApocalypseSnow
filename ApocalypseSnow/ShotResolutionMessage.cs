using System;
using System.Buffers.Binary;

namespace ApocalypseSnow;

public enum ShotOutcome : byte
{
    Expired = 1,
    Obstacle = 2,
    Penguin = 3
}

/// <summary>
/// Final authoritative fate of a ball. The ShotKey is shared with the predicted
/// ball and MsgRemoteShot. HitPlayerId is zero for non-penguin outcomes.
/// ServerTick is diagnostic/event ordering metadata, not a local client tick.
/// </summary>
public readonly record struct ShotResolutionMessage(
    ShotKey Key, ShotOutcome Outcome, uint HitPlayerId, uint ServerTick)
{
    // MsgShotResolved: 18 bytes including the message type.
    public const int PayloadLength = 17;

    public static ShotResolutionMessage Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadLength)
            throw new ArgumentException("Invalid MsgShotResolved payload length.", nameof(payload));

        uint owner = BinaryPrimitives.ReadUInt32LittleEndian(payload[..4]);
        uint shotTick = BinaryPrimitives.ReadUInt32LittleEndian(payload[4..8]);
        ShotOutcome outcome = (ShotOutcome)payload[8];
        uint hitPlayerId = BinaryPrimitives.ReadUInt32LittleEndian(payload[9..13]);
        uint serverTick = BinaryPrimitives.ReadUInt32LittleEndian(payload[13..17]);
        if (outcome is not (ShotOutcome.Expired or ShotOutcome.Obstacle or ShotOutcome.Penguin))
            throw new ArgumentException("Invalid authoritative shot outcome.", nameof(payload));
        if ((outcome == ShotOutcome.Penguin) != (hitPlayerId != 0))
            throw new ArgumentException("Inconsistent shot outcome and hit player ID.", nameof(payload));
        return new ShotResolutionMessage(new ShotKey(owner, shotTick), outcome, hitPlayerId, serverTick);
    }
}
