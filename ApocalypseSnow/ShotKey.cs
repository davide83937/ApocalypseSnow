using System;

namespace ApocalypseSnow;

/// <summary>
/// Identifies the same shot on the firing client, the server and the opponent:
/// player ID from JoinAck + LocalTick carried by MsgShot.
/// </summary>
public readonly record struct ShotKey(uint PlayerId, uint ShotTick)
{
    // CollisionRecordIn._tag is a 16-byte ANSI buffer (15 chars + NUL).
    // The two existing prefixes Ball and RedBall leave room for 8 hex digits.
    public string CollisionTag(string prefix)
    {
        if (prefix != "Ball" && prefix != "RedBall")
            throw new ArgumentException("Unexpected ball collision prefix.", nameof(prefix));

        return $"{prefix}{ShotTick:x8}";
    }
}
