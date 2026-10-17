using System.Collections.Concurrent;
using System.Buffers.Binary;
using ApocalypseSnow;
using Microsoft.Xna.Framework;

// Tests use the production Reconciler, PhysicsWrapper and native libPhysicsDll.
// No game window or network socket is created. Keep expectations independent
// of freeze implementation: these are the pre-freeze movement baseline.
internal static class Program
{
    private const float Speed = 200f;
    private const float Dt = 1f / 30f;
    private const float Step = Speed * Dt;

    private static int Main()
    {
        var reconciler = new Reconciler(null!);
        (string Name, Action<Reconciler> Run)[] tests =
        [
            ("No auth: never touch predicted position", NoAuthDoesNotCorrect),
            ("Auth only: authoritative position wins", AuthWithoutPending),
            ("ACK removes acknowledged moves, replay keeps the rest", AckAndReplay),
            ("Replay matches prediction: no correction", AccuratePrediction),
            ("Dead zone ignores sub-2px mismatch", DeadZone),
            ("Soft correction interpolates medium mismatch", SoftCorrection),
            ("Large mismatch snaps to replayed position", HardCorrection),
            ("Reload, freezing, egg actions skip movement in replay", ActionLocks),
            ("Blocked collision rejects candidate without stepping backward", ObstacleCollision),
            ("Diagonal movement uses normalized native physics", DiagonalMovement),
            ("Twenty buffered inputs replay despite partial ACK", InputBurst),
            ("Subsequent ACK replays only new pending inputs", ConsecutiveAcknowledgements),
            ("Reset discards pending movement from previous match", ResetClearsPending),
            ("GetLatest consumes queue and emits only final snapshot", LatestSnapshot),
            ("Repeated Apply without fresh auth does nothing", SingleUseAuth),
            ("Shot keys disambiguate shooters and ticks", ShotKeyEquality),
            ("Every ball collider tag is unique and fits native 16-byte buffer", BallCollisionTags),
            ("RemoteShot wire payload preserves identity and trajectory fields", RemoteShotPayload),
            ("RemoteShot decoder rejects malformed length", RejectMalformedRemoteShot),
            ("Three terminal shot results decode with complete identity", ShotResolutionPayload),
            ("Terminal results reject corrupted payload", ShotResolutionRejectsInvalid),
            ("TCP framing survives byte-by-byte fragmented resolution", FragmentedShotResolution),
            ("TCP framing preserves concatenated and split messages", FramingBatchAndSplit),
            ("TCP framing rejects unknown packet types", FramingRejectsUnknown)
        ];

        var errors = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                reconciler.Reset();
                run(reconciler);
                Console.WriteLine($"PASS: {name}");
            }
            catch (Exception ex)
            {
                errors++;
                Console.Error.WriteLine($"FAIL: {name}: {ex}");
            }
        }

        Console.WriteLine($"RESULT: {tests.Length - errors}/{tests.Length} passed");
        return errors == 0 ? 0 : 1;
    }

    private static void At(Vector2 actual, float x, float y, float tolerance = 0.03f)
    {
        if (MathF.Abs(actual.X - x) > tolerance || MathF.Abs(actual.Y - y) > tolerance)
            throw new Exception($"Expected ({x:F3}, {y:F3}), got ({actual.X:F3}, {actual.Y:F3})");
    }

    private static void Equal<T>(T actual, T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new Exception($"Expected {expected}, got {actual}");
    }

    private static void Apply(Reconciler r, ref Vector2 position, Func<Vector2, bool>? isBlocked = null)
        => r.Apply(ref position, Speed, Dt, isBlocked);

    private static void NoAuthDoesNotCorrect(Reconciler r)
    {
        r.Record(1, StateList.Right);
        var pos = new Vector2(40, 10);
        Apply(r, ref pos);
        At(pos, 40, 10);
    }

    private static void AuthWithoutPending(Reconciler r)
    {
        r.OnServerAuth(9, new Vector2(20, 30));
        var pos = new Vector2(0, 0);
        Apply(r, ref pos);
        At(pos, 20, 30);
    }

    private static void AckAndReplay(Reconciler r)
    {
        r.Record(1, StateList.Right);
        r.Record(2, StateList.Up);
        r.Record(3, StateList.Right);
        r.OnServerAuth(2, new Vector2(100, 100));
        var pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 100 + Step, 100);
    }

    private static void AccuratePrediction(Reconciler r)
    {
        r.Record(1, StateList.Right);
        r.Record(2, StateList.Right);
        r.Record(3, StateList.Right);
        r.OnServerAuth(1, new Vector2(Step, 0));
        var pos = new Vector2(Step * 3, 0);
        Apply(r, ref pos);
        At(pos, Step * 3, 0);
    }

    private static void DeadZone(Reconciler r)
    {
        r.OnServerAuth(1, new Vector2(100, 100));
        var pos = new Vector2(101, 100);
        Apply(r, ref pos);
        At(pos, 101, 100);
    }

    private static void SoftCorrection(Reconciler r)
    {
        r.OnServerAuth(1, new Vector2(100, 100));
        var pos = new Vector2(108, 100);
        Apply(r, ref pos);
        At(pos, 106, 100);
    }

    private static void HardCorrection(Reconciler r)
    {
        r.OnServerAuth(1, new Vector2(100, 100));
        var pos = new Vector2(120, 100);
        Apply(r, ref pos);
        At(pos, 100, 100);
    }

    private static void ActionLocks(Reconciler r)
    {
        r.Record(1, StateList.Right | StateList.Reload);
        r.Record(2, StateList.Right | StateList.Freezing);
        r.Record(3, StateList.Right | StateList.TakingEgg);
        r.Record(4, StateList.Right | StateList.PuttingEgg);
        r.Record(5, StateList.Right);
        r.OnServerAuth(0, Vector2.Zero);
        var pos = new Vector2(100, 0);
        Apply(r, ref pos);
        At(pos, Step, 0);
    }

    private static void ObstacleCollision(Reconciler r)
    {
        // Speed 30 => 1 pixel/step; all candidates at X>=2 are blocked.
        r.Record(1, StateList.Right);
        r.Record(2, StateList.Right);
        r.Record(3, StateList.Right);
        r.OnServerAuth(0, Vector2.Zero);
        var pos = new Vector2(30, 0);
        r.Apply(ref pos, 30f, Dt, candidate => candidate.X >= 1.99f);
        At(pos, 1, 0);
    }

    private static void DiagonalMovement(Reconciler r)
    {
        r.Record(1, StateList.Right | StateList.Up);
        r.OnServerAuth(0, Vector2.Zero);
        var pos = new Vector2(40, 0);
        Apply(r, ref pos);
        var expected = Step / MathF.Sqrt(2f);
        At(pos, expected, -expected);
    }

    private static void InputBurst(Reconciler r)
    {
        for (uint seq = 1; seq <= 20; seq++)
            r.Record(seq, StateList.Right);
        r.OnServerAuth(10, new Vector2(10 * Step, 0));
        var pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 20 * Step, 0);
    }

    private static void ConsecutiveAcknowledgements(Reconciler r)
    {
        for (uint seq = 1; seq <= 20; seq++)
            r.Record(seq, StateList.Right);

        r.OnServerAuth(10, new Vector2(10 * Step, 0));
        var pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 20 * Step, 0);

        for (uint seq = 21; seq <= 30; seq++)
            r.Record(seq, StateList.Right);

        r.OnServerAuth(20, new Vector2(20 * Step, 0));
        Apply(r, ref pos);
        At(pos, 30 * Step, 0);
    }

    private static void ResetClearsPending(Reconciler r)
    {
        r.Record(1, StateList.Right);
        r.Reset();
        r.OnServerAuth(0, Vector2.Zero);
        var pos = new Vector2(20, 0);
        Apply(r, ref pos);
        At(pos, 0, 0);
    }

    private readonly record struct Snapshot(uint Ack, Vector2 Position);

    private static void LatestSnapshot(Reconciler r)
    {
        var queue = new ConcurrentQueue<Snapshot>();
        queue.Enqueue(new Snapshot(1, Vector2.One));
        queue.Enqueue(new Snapshot(2, Vector2.One * 2));
        queue.Enqueue(new Snapshot(3, Vector2.One * 3));
        var count = 0;
        r.GetLatest(queue, snapshot =>
        {
            count++;
            Equal(snapshot.Ack, 3u);
            At(snapshot.Position, 3, 3);
        });
        Equal(count, 1);
        Equal(queue.Count, 0);
    }

    private static void ShotKeyEquality(Reconciler r)
    {
        var key = new ShotKey(1, 33);
        Equal(key, new ShotKey(1, 33));
        if (key == new ShotKey(2, 33) || key == new ShotKey(1, 34))
            throw new Exception("ShotKey collision between different shooter/tick pairs");
    }

    private static void BallCollisionTags(Reconciler r)
    {
        var first = new ShotKey(1, 33);
        var second = new ShotKey(2, 34);
        Equal(first.CollisionTag("Ball"), "Ball00000021");
        Equal(second.CollisionTag("RedBall"), "RedBall00000022");
        var longTag = new ShotKey(2, uint.MaxValue).CollisionTag("RedBall");
        Equal(longTag.Length, 15);
        Equal(longTag, "RedBallffffffff");
        try
        {
            first.CollisionTag("Unexpected");
            throw new Exception("Unknown collision prefix was accepted");
        }
        catch (ArgumentException)
        {
            // Expected.
        }
    }

    private static void RemoteShotPayload(Reconciler r)
    {
        // The Go server transmits [targetX:f32][targetY:f32][charge:u32]
        // [shotType:u8][owner:u32][shotTick:u32] after MsgRemoteShot.
        Span<byte> payload = stackalloc byte[RemoteShotMessage.PayloadLength];
        BinaryPrimitives.WriteInt32LittleEndian(payload[..4], BitConverter.SingleToInt32Bits(412.5f));
        BinaryPrimitives.WriteInt32LittleEndian(payload[4..8], BitConverter.SingleToInt32Bits(13.25f));
        BinaryPrimitives.WriteInt32LittleEndian(payload[8..12], 600);
        payload[12] = (byte)ShotType.Right;
        BinaryPrimitives.WriteUInt32LittleEndian(payload[13..17], 2u);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[17..21], 33u);
        var shot = RemoteShotMessage.Parse(payload);
        Equal(shot.TargetX, 412.5f);
        Equal(shot.TargetY, 13.25f);
        Equal(shot.Charge, 600);
        Equal(shot.Type, ShotType.Right);
        Equal(shot.Key, new ShotKey(2, 33));
    }

    private static void RejectMalformedRemoteShot(Reconciler r)
    {
        try
        {
            RemoteShotMessage.Parse(new byte[20]);
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new Exception("Truncated MsgRemoteShot payload was accepted");
    }

    private static byte[] ResolutionPacket(
        uint owner = 1, uint shotTick = 33, ShotOutcome outcome = ShotOutcome.Penguin,
        uint hitPlayerId = 2, uint serverTick = 500)
    {
        byte[] packet = new byte[1 + ShotResolutionMessage.PayloadLength];
        packet[0] = (byte)MessageType.ShotResolved;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1, 4), owner);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(5, 4), shotTick);
        packet[9] = (byte)outcome;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(10, 4), hitPlayerId);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(14, 4), serverTick);
        return packet;
    }

    private static void ShotResolutionPayload(Reconciler r)
    {
        foreach (var (outcome, victim) in new[]
        {
            (ShotOutcome.Penguin, 2u), (ShotOutcome.Obstacle, 0u), (ShotOutcome.Expired, 0u)
        })
        {
            var packet = ResolutionPacket(outcome: outcome, hitPlayerId: victim);
            var decoded = ShotResolutionMessage.Parse(packet.AsSpan(1));
            Equal(decoded.Key, new ShotKey(1, 33));
            Equal(decoded.Outcome, outcome);
            Equal(decoded.HitPlayerId, victim);
            Equal(decoded.ServerTick, 500u);
        }
    }

    private static void ShotResolutionRejectsInvalid(Reconciler r)
    {
        static void Reject(byte[] payload)
        {
            try { ShotResolutionMessage.Parse(payload); }
            catch (ArgumentException) { return; }
            throw new Exception("Corrupt shot resolution was accepted");
        }
        Reject(new byte[15]);
        var invalidType = ResolutionPacket();
        invalidType[9] = 99;
        Reject(invalidType[1..]);
        Reject(ResolutionPacket(outcome: ShotOutcome.Obstacle, hitPlayerId: 2)[1..]);
        Reject(ResolutionPacket(outcome: ShotOutcome.Penguin, hitPlayerId: 0)[1..]);
    }

    private static void FragmentedShotResolution(Reconciler r)
    {
        var framer = new NetworkPacketFramer();
        byte[] packet = ResolutionPacket(owner: 2, shotTick: 33, outcome: ShotOutcome.Obstacle, hitPlayerId: 0);
        for (int i = 0; i < packet.Length - 1; ++i)
        {
            framer.Append(packet.AsSpan(i, 1));
            if (framer.TryRead(out _, out _))
                throw new Exception("Fragmented packet dispatched before final byte");
        }
        framer.Append(packet.AsSpan(packet.Length - 1, 1));
        if (!framer.TryRead(out byte type, out byte[] payload))
            throw new Exception("Complete packet not reconstructed");
        Equal(type, (byte)MessageType.ShotResolved);
        var resolution = ShotResolutionMessage.Parse(payload);
        Equal(resolution.Key, new ShotKey(2, 33));
        Equal(resolution.Outcome, ShotOutcome.Obstacle);
        if (framer.TryRead(out _, out _))
            throw new Exception("Framer replayed already consumed message");
    }

    private static void FramingBatchAndSplit(Reconciler r)
    {
        var framer = new NetworkPacketFramer();
        byte[] auth = new byte[13];
        auth[0] = 4;
        byte[] remote = new byte[22];
        remote[0] = 7;
        byte[] resolved = ResolutionPacket();
        byte[] input = auth.Concat(remote).Concat(resolved).ToArray();
        int split = input.Length - 5;
        framer.Append(input.AsSpan(0, split));
        if (!framer.TryRead(out byte t1, out byte[] p1) || t1 != 4 || p1.Length != 12)
            throw new Exception("AuthState lost in batch");
        if (!framer.TryRead(out byte t2, out byte[] p2) || t2 != 7 || p2.Length != 21)
            throw new Exception("RemoteShot lost in batch");
        if (framer.TryRead(out _, out _))
            throw new Exception("Truncated trailing resolution incorrectly dispatched");
        framer.Append(input.AsSpan(split));
        if (!framer.TryRead(out byte t3, out byte[] p3) || t3 != 11)
            throw new Exception("Resolution lost after fragmented batch");
        Equal(ShotResolutionMessage.Parse(p3).Key, new ShotKey(1, 33));
        if (framer.TryRead(out _, out _)) throw new Exception("Leftover bytes after batch");
    }

    private static void FramingRejectsUnknown(Reconciler r)
    {
        var framer = new NetworkPacketFramer();
        framer.Append(new byte[] { 255 });
        try { framer.TryRead(out _, out _); }
        catch (System.IO.InvalidDataException) { return; }
        throw new Exception("Unknown message was silently accepted");
    }

    private static void SingleUseAuth(Reconciler r)
    {
        r.OnServerAuth(1, Vector2.Zero);
        var pos = new Vector2(30, 0);
        Apply(r, ref pos);
        At(pos, 0, 0);
        pos = new Vector2(40, 0);
        Apply(r, ref pos);
        At(pos, 40, 0);
    }
}
