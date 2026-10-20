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

    // Esegue tutti i test e segnala eventuali errori.
    private static int Main(string[] args)
    {
        if (args.Contains("--flight-fixtures"))
        {
            foreach (var (name, chargeMs, shotType) in FlightCases())
            {
                // Stessi metodi chiamati da Shot() e dal costruttore reale di Ball.
                float gravity = PenguinShotHandler.ComputeGravityForLowShot(1000f);
                float startVerticalSpeed = PenguinShotHandler.ComputeVerticalSpeed(chargeMs / 1000f, shotType, gravity);
                float seconds = Ball.ComputeFlightDurationSeconds(startVerticalSpeed, gravity);
                float independent = PenguinShotHandler.PredictFullFlightSeconds(chargeMs, shotType);
                if (MathF.Abs(seconds - independent) > 0.0001f)
                    throw new Exception($"Ball flight and independent C# model mismatch: {name}");
                uint ticks = ShotFlightTiming.LandingTicks(seconds, 30);
                Console.WriteLine($"FLIGHT_CS|{name}|{seconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}|{ticks}");
            }
            return 0;
        }
        if (args.Contains("--trajectory-fixtures"))
        {
            if (GameWorld.Width != 800 || GameWorld.Height != 480 ||
                GameWorld.ShotColliderWidth != 20 || GameWorld.ShotColliderHeight != 25)
                throw new Exception("Incorrect C# world / ball geometry");

            Vector2[] origins = [new(100, 300), new(550, 25), new(314, 74)];
            Vector2[] targets = [new(700, 120), new(120, 430), new(400, 240)];
            int[] charges = [250, 700, 1500];
            uint[] ticks = [1, 3, 5, 7, 10, 15, 22];
            string F(float v) => v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);

            for (int i = 0; i < origins.Length; i++)
            foreach (int charge in charges)
            foreach (ShotType kind in new[] { ShotType.Left, ShotType.Right })
            {
                var launch = PenguinShotHandler.ComputeLaunch(origins[i], targets[i], charge / 1000f, kind);
                float duration = Ball.ComputeFlightDurationSeconds(launch.VerticalSpeed, launch.Gravity);
                uint landingTicks = ShotFlightTiming.LandingTicks(duration, 30);
                foreach (uint tick in ticks)
                {
                    if (tick >= landingTicks) continue;
                    var sample = Ball.SampleTrajectory(launch.Start, launch.GroundVelocity,
                        launch.VerticalSpeed, launch.Gravity, tick / 30f);
                    string key = $"p{i}-{(kind == ShotType.Left ? "left" : "right")}-{charge}-{tick}";
                    Console.WriteLine($"PATH_CS|{key}|{F(sample.Screen.X)}|{F(sample.Screen.Y)}|" +
                                      $"{F(sample.Shadow.X)}|{F(sample.Shadow.Y)}|{F(sample.Height)}");
                }
            }
            return 0;
        }
        var reconciler = new Reconciler(null!);
        (string Name, Action<Reconciler> Run)[] tests =
        [
            ("No auth: never touch predicted position", NoAuthDoesNotCorrect),
            ("Auth only: authoritative position wins", AuthWithoutPending),
            ("Older ACK cannot rewind after newer ACK", OlderAckNeverRewinds),
            ("False predicted HIT unfreezes immediately", FalsePredictedHitUnfreezes),
            ("Wire MISS rejects predicted freeze without ghost movement", WireMissRejectsPredictedFreeze),
            ("Rejected HIT preserves another active freeze", FalseHitDoesNotClearOtherFreeze),
            ("ACK removes acknowledged moves, replay keeps the rest", AckAndReplay),
            ("Replay matches prediction: no correction", AccuratePrediction),
            ("Dead zone ignores sub-2px mismatch", DeadZone),
            ("Soft correction interpolates medium mismatch", SoftCorrection),
            ("Large mismatch snaps to replayed position", HardCorrection),
            ("Reload, freezing, egg actions skip movement in replay", ActionLocks),
            ("Late HIT removes previously predicted movements", RetroactiveFrozenInputs),
            ("Predicted freeze, confirmed freeze and false HIT use tick deadlines", FreezeTimelines),
            ("Server flight duration maps to independent local timelines", FlightTimelineAcrossClients),
            ("Quantized flight duration does not gain a ghost tick", FlightTickQuantization),
            ("Ball fragments expire and terminal outcomes do not remove early", BallVisualLifecycleBoundaries),
            ("Remote shot before hit even when TCP messages arrive together", RemoteShotAndHitSameTcpBatch),
            ("Confirmed freeze is half-open and late confirmations never restart it", AuthoritativeFreezeBoundaries),
            ("Confirmed HIT without initial shot tick uses current local tick", ConfirmedHitWithoutStartTick),
            ("Local egg drop on E fires once, retains carried tag until event", LocalEggDropOnE),
            ("Frozen penguins cannot pick up eggs", FrozenPenguinsCannotPickUpEggs),
            ("Remote egg pickup uses only WithEgg received from network", RemotePickupNeedsNetworkConfirmation),
            ("Remote egg drop notifies before clearing its tag", RemoteEggDropPreservesTag),
            ("Egg delivery emits only one score event", EggDeliveryScoresOnlyOnce),
            ("Authoritative tick replaces an incorrect predicted impact", ConfirmedFreezeReplacesPrediction),
            ("Movement replay skips exactly the local frozen ticks", LocalFreezeReplayBoundary),
            ("Remote penguin obeys predicted freeze and clears it on rejection", RemotePredictedFreeze),
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

    private static IEnumerable<(string Name, int ChargeMs, ShotType Type)> FlightCases()
    {
        foreach (int ms in new[] { 50, 100, 250, 500, 750, 1000, 1250, 1500 })
        {
            yield return ($"left-{ms}", ms, ShotType.Left);
            yield return ($"right-{ms}", ms, ShotType.Right);
        }
    }

    // Controlla una posizione con un piccolo margine di tolleranza.
    private static void At(Vector2 actual, float x, float y, float tolerance = 0.03f)
    {
        if (MathF.Abs(actual.X - x) > tolerance || MathF.Abs(actual.Y - y) > tolerance)
            throw new Exception($"Expected ({x:F3}, {y:F3}), got ({actual.X:F3}, {actual.Y:F3})");
    }

    // Il drop con E notifica EggsEvent prima di azzerare il riferimento all'uovo.
    private static void LocalEggDropOnE(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguin");
        collider.isWithEgg = true;
        string carriedEgg = "egg7";
        string eggSeenDuringEvent = string.Empty;
        int dropEvents = 0;

        collider.eggPutEvent += (sender, args) =>
        {
            dropEvents++;
            eggSeenDuringEvent = carriedEgg;
        };

        StateStruct state = new StateStruct
        {
            Old = StateList.WithEgg,
            Current = StateList.WithEgg | StateList.TakingEgg
        };
        collider.putEgg(state, ref carriedEgg);

        Equal(dropEvents, 1);
        Equal(eggSeenDuringEvent, "egg7");
        Equal(carriedEgg == null, true);
        Equal(collider.isWithEgg, false);

        // E ancora premuto nel tick successivo non deve causare un secondo drop.
        state.Old = state.Current;
        state.Current = StateList.TakingEgg;
        collider.putEgg(state, ref carriedEgg);
        Equal(dropEvents, 1);
    }

    private static void FrozenPenguinsCannotPickUpEggs(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguin");
        string egg = default!;
        int picks = 0;
        collider.eggTakenEvent += (_, _) => picks++;

        var takingEgg = new StateStruct { Current = StateList.TakingEgg };
        collider.isFrozen = true;
        collider.HandleEggPickup("egg5", takingEgg, 1.1f, ref egg);
        Equal(picks, 0);
        Equal(egg == null, true);

        collider.isFrozen = false;
        takingEgg.Current |= StateList.Freezing;
        collider.HandleEggPickup("egg5", takingEgg, 1.1f, ref egg);
        Equal(picks, 0);
        Equal(egg == null, true);

        takingEgg.Current &= ~StateList.Freezing;
        collider.HandleEggPickup("egg5", takingEgg, 1.1f, ref egg);
        Equal(picks, 1);
        Equal(egg, "egg5");
    }

    private static void RemotePickupNeedsNetworkConfirmation(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguinRed");
        string egg = default!;
        int picks = 0;
        collider.eggTakenEvent += (_, _) => picks++;

        var networkState = new StateStruct { Current = StateList.TakingEgg };
        collider.HandleEggPickup("egg8", networkState, 3f, ref egg);
        Equal(picks, 0);
        Equal(egg == null, true);

        networkState.Current |= StateList.WithEgg;
        collider.isFrozen = true;
        collider.HandleEggPickup("egg8", networkState, 3f, ref egg);
        Equal(picks, 0);

        collider.isFrozen = false;
        collider.HandleEggPickup("egg8", networkState, 0.1f, ref egg);
        Equal(picks, 1);
        Equal(egg, "egg8");
        collider.HandleEggPickup("egg8", networkState, 0.1f, ref egg);
        Equal(picks, 1);
    }

    private static void RemoteEggDropPreservesTag(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguinRed");
        collider.isWithEgg = true;
        string egg = "egg9";
        int puts = 0;
        string eggSeenByEvent = "";
        collider.eggPutEvent += (_, _) =>
        {
            puts++;
            eggSeenByEvent = egg;
        };

        var remoteState = new StateStruct { Current = StateList.None };
        collider.SyncStateWithNetwork(remoteState, ref egg);
        Equal(puts, 1);
        Equal(eggSeenByEvent, "egg9");
        Equal(egg == null, true);
        Equal(collider.isWithEgg, false);
        collider.SyncStateWithNetwork(remoteState, ref egg);
        Equal(puts, 1);
    }

    private static void EggDeliveryScoresOnlyOnce(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguin");
        collider.isWithEgg = true;
        string egg = "egg12";
        int scores = 0;
        collider.eggDeleteEvent += (_, tag) =>
        {
            Equal(tag, "egg12");
            scores++;
        };
        var deliveryState = new StateStruct
        {
            Current = StateList.WithEgg | StateList.PuttingEgg
        };
        collider.HandleEggDelivery("blueP", ref deliveryState, 0.60f, ref egg);
        Equal(scores, 0);
        collider.HandleEggDelivery("blueP", ref deliveryState, 0.45f, ref egg);
        Equal(scores, 1);
        collider.HandleEggDelivery("blueP", ref deliveryState, 0.20f, ref egg);
        Equal(scores, 1);
        Equal(egg == null, true);
        Equal(collider.isWithEgg, false);
    }

    // Controlla che due valori siano uguali.
    private static void Equal<T>(T actual, T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new Exception($"Expected {expected}, got {actual}");
    }

    // Un HIT server resta valido anche se il client non ha osservato RemoteShot.
    // La durata e la fine del freeze restano sulla timeline locale.
    private static void ConfirmedHitWithoutStartTick(Reconciler reconciler)
    {
        const uint currentLocalTick = 205;
        uint recoveredHitTick = FreezeTimeline.ComputeConfirmedImpactTick(
            hasObservedStartTick: false, observedStartTick: 0,
            serverFlightTicks: 18, currentLocalTick: currentLocalTick);
        Equal(recoveredHitTick, currentLocalTick);

        var freeze = new FreezeTimeline();
        var shotKey = new ShotKey(2, 40);
        freeze.Predict(shotKey, currentLocalTick - 10);
        freeze.ConfirmLocal(shotKey, recoveredHitTick);
        Equal(freeze.IsFrozen(currentLocalTick), true);
        Equal(freeze.IsFrozen(currentLocalTick + 89), true);
        Equal(freeze.IsFrozen(currentLocalTick + 90), false);

        uint normalHitTick = FreezeTimeline.ComputeConfirmedImpactTick(
            hasObservedStartTick: true, observedStartTick: 100,
            serverFlightTicks: 18, currentLocalTick: currentLocalTick);
        Equal(normalHitTick, 118u);
    }

    // Applica il Reconciler con i parametri di movimento del gioco.
    private static void Apply(Reconciler r, ref Vector2 position, Func<Vector2, bool>? isBlocked = null)
        => r.ReconcileLocalPosition(ref position, Speed, Dt, isBlocked);

    // Verifica che senza auth la posizione non cambi.
    private static void WireMissRejectsPredictedFreeze(Reconciler r)
    {
        var freeze = new FreezeTimeline();
        var key = new ShotKey(1, 33);
        freeze.Predict(key, 100);
        Vector2 pos = new Vector2(100, 80);
        for (uint tick = 100; tick <= 104; tick++)
        {
            Equal(freeze.IsFrozen(tick), true);
            r.RecordInputForReplay(tick, StateList.None);
        }
        byte[] packet = ResolutionPacket(outcome: ShotOutcome.Expired, hitPlayerId: 0);
        var framer = new NetworkPacketFramer();
        framer.Append(packet);
        if (!framer.TryRead(out _, out var payload))
            throw new Exception("MISS packet not received");
        var result = ShotResolutionMessage.Parse(payload);
        Equal(result.Key, key);
        Equal(result.Outcome, ShotOutcome.Expired);
        freeze.RejectPrediction(result.Key);
        Equal(freeze.IsFrozen(105), false);
        r.ReceiveAuthoritativePosition(99, pos);
        Apply(r, ref pos);
        At(pos, 100, 80);
        r.RecordInputForReplay(105, StateList.Right);
        var lastAuthPos = pos;
        pos.X += Step; // il client predice il nuovo input visibile
        r.ReceiveAuthoritativePosition(104, lastAuthPos);
        Apply(r, ref pos);
        At(pos, 100 + Step, 80);
    }

    private static void FalsePredictedHitUnfreezes(Reconciler r)
    {
        var f = new FreezeTimeline();
        var key = new ShotKey(2, 40);
        f.Predict(key, 100);
        Equal(f.IsFrozen(105), true);
        r.RecordInputForReplay(105, StateList.None); // client sent neutral movement while predicting HIT
        f.RejectPrediction(key);
        Equal(f.IsFrozen(105), false);
        r.ReceiveAuthoritativePosition(104, Vector2.Zero);
        Vector2 pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 0, 0); // no invisible input replay
    }

    private static void FalseHitDoesNotClearOtherFreeze(Reconciler r)
    {
        var f = new FreezeTimeline();
        var rejected = new ShotKey(2, 41);
        var confirmed = new ShotKey(2, 42);
        f.Predict(rejected, 100);
        f.Predict(confirmed, 103);
        f.RejectPrediction(rejected);
        Equal(f.IsFrozen(105), true);
        f.ConfirmLocal(confirmed, 103);
        Equal(f.IsFrozen(105), true);
        Equal(f.IsFrozen(192), true);
        Equal(f.IsFrozen(193), false);
    }

    private static void OlderAckNeverRewinds(Reconciler r)
    {
        Vector2 pos = Vector2.Zero;
        r.ReceiveAuthoritativePosition(30, new Vector2(200, 100));
        Apply(r, ref pos);
        r.ReceiveAuthoritativePosition(29, new Vector2(5, 5));
        Apply(r, ref pos);
        At(pos, 200, 100);
    }

    private static void NoAuthDoesNotCorrect(Reconciler r)
    {
        r.RecordInputForReplay(1, StateList.Right);
        var pos = new Vector2(40, 10);
        Apply(r, ref pos);
        At(pos, 40, 10);
    }

    // Verifica la correzione senza input in sospeso.
    private static void AuthWithoutPending(Reconciler r)
    {
        r.ReceiveAuthoritativePosition(9, new Vector2(20, 30));
        var pos = new Vector2(0, 0);
        Apply(r, ref pos);
        At(pos, 20, 30);
    }

    // Verifica che gli input confermati vengano scartati.
    private static void AckAndReplay(Reconciler r)
    {
        r.RecordInputForReplay(1, StateList.Right);
        r.RecordInputForReplay(2, StateList.Up);
        r.RecordInputForReplay(3, StateList.Right);
        r.ReceiveAuthoritativePosition(2, new Vector2(100, 100));
        var pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 100 + Step, 100);
    }

    // Verifica che una prediction corretta non venga modificata.
    private static void AccuratePrediction(Reconciler r)
    {
        r.RecordInputForReplay(1, StateList.Right);
        r.RecordInputForReplay(2, StateList.Right);
        r.RecordInputForReplay(3, StateList.Right);
        r.ReceiveAuthoritativePosition(1, new Vector2(Step, 0));
        var pos = new Vector2(Step * 3, 0);
        Apply(r, ref pos);
        At(pos, Step * 3, 0);
    }

    // Verifica la soglia che ignora gli errori minimi.
    private static void DeadZone(Reconciler r)
    {
        r.ReceiveAuthoritativePosition(1, new Vector2(100, 100));
        var pos = new Vector2(101, 100);
        Apply(r, ref pos);
        At(pos, 101, 100);
    }

    // Verifica la correzione graduale degli errori medi.
    private static void SoftCorrection(Reconciler r)
    {
        r.ReceiveAuthoritativePosition(1, new Vector2(100, 100));
        var pos = new Vector2(108, 100);
        Apply(r, ref pos);
        At(pos, 106, 100);
    }

    // Verifica lo snap sugli errori grandi.
    private static void HardCorrection(Reconciler r)
    {
        r.ReceiveAuthoritativePosition(1, new Vector2(100, 100));
        var pos = new Vector2(120, 100);
        Apply(r, ref pos);
        At(pos, 100, 100);
    }

    // Verifica gli stati che bloccano il movimento nel replay.
    private static void ActionLocks(Reconciler r)
    {
        r.RecordInputForReplay(1, StateList.Right | StateList.Reload);
        r.RecordInputForReplay(2, StateList.Right | StateList.Freezing);
        r.RecordInputForReplay(3, StateList.Right | StateList.TakingEgg);
        r.RecordInputForReplay(4, StateList.Right | StateList.PuttingEgg);
        r.RecordInputForReplay(5, StateList.Right);
        r.ReceiveAuthoritativePosition(0, Vector2.Zero);
        var pos = new Vector2(100, 0);
        Apply(r, ref pos);
        At(pos, Step, 0);
    }

    // Verifica che un HIT tardivo annulli solo gli input nell'intervallo congelato.
    private static void RetroactiveFrozenInputs(Reconciler r)
    {
        for (uint seq = 41; seq <= 131; seq++)
            r.RecordInputForReplay(seq, StateList.Right);
        r.MarkInputsAsFrozen(41, 130);
        r.ReceiveAuthoritativePosition(40, new Vector2(100, 30));
        var pos = new Vector2(500, 30);
        Apply(r, ref pos);
        At(pos, 100 + Step, 30);
    }

    // Verifica che una prediction smentita non prolunghi il freeze.
    private static void FreezeTimelines(Reconciler r)
    {
        var freeze = new FreezeTimeline();
        var first = new ShotKey(1, 17);
        var second = new ShotKey(1, 22);
        freeze.Predict(first, 50);
        Equal(freeze.IsFrozen(70), true);
        freeze.RejectPrediction(first);
        Equal(freeze.IsFrozen(70), false);
        freeze.Predict(second, 51);
        freeze.ConfirmLocal(second, 51);
        Equal(freeze.IsFrozen(60), true);
        Equal(freeze.IsFrozen(140), true);
        Equal(freeze.IsFrozen(141), false);
    }

    private static void FlightTimelineAcrossClients(Reconciler r)
    {
        uint flightTicks = 12;
        uint impactA = FreezeTimeline.ComputeImpactTick(15, flightTicks);
        uint impactB = FreezeTimeline.ComputeImpactTick(83, flightTicks);
        Equal(impactA, 27u);
        Equal(impactB, 95u);
        var a = new FreezeTimeline();
        var b = new FreezeTimeline();
        a.ConfirmLocal(new ShotKey(1, 7), impactA);
        b.ConfirmLocal(new ShotKey(1, 7), impactB);
        Equal(a.IsFrozen(27), true);
        Equal(b.IsFrozen(95), true);
        Equal(a.IsFrozen(117), false);
        Equal(b.IsFrozen(185), false);
    }

    private static void BallVisualLifecycleBoundaries(Reconciler r)
    {
        Equal(GameWorld.Width, 800);
        Equal(GameWorld.Height, 480);
        Equal(GameWorld.ShotColliderWidth, 20);
        Equal(GameWorld.ShotColliderHeight, 25);
        Equal(Ball.LandingHoldSeconds, 0.5f);
        Equal(Ball.LandingVisualSeconds, 3f);
        Equal(Ball.IsLandingFragmentVisible(0f), true);
        Equal(Ball.IsLandingFragmentVisible(2.999f), true);
        Equal(Ball.IsLandingFragmentVisible(3f), false);
        Equal(Ball.IsLandingFragmentVisible(3.001f), false);
        Equal(Ball.LandingFragmentOpacity(-0.01f), 0f);
        Equal(Ball.LandingFragmentOpacity(0f), 1f);
        Equal(Ball.LandingFragmentOpacity(0.499f), 1f);
        Equal(Ball.LandingFragmentOpacity(0.5f), 1f);
        Equal(Ball.LandingFragmentOpacity(1.75f), 0.5f);
        Equal(Ball.LandingFragmentOpacity(3f), 0f);
        Equal(Ball.LandingFragmentOpacity(3.1f), 0f);
        Equal(Ball.ShouldDrawLandingFragment(false, false, 1.75f), false);
        // Un MISS deve apparire dal PRIMO frame di atterraggio, ancora prima
        // che il server risponda, e continuare senza salti dopo la conferma.
        Equal(Ball.ShouldDrawLandingFragment(true, false, 0f), true);
        Equal(Ball.ShouldDrawLandingFragment(true, false, 0.15f), true);
        Equal(Ball.ShouldDrawLandingFragment(true, false, 0.50f), true);
        Equal(Ball.ShouldDrawLandingFragment(true, false, 1.75f), true);
        Equal(Ball.ShouldDrawLandingFragment(true, false, 2.99f), true);
        Equal(Ball.ShouldDrawLandingFragment(true, false, 3f), false);
        // HIT predetto e poi negato dal server: NON ripristinare il frammento,
        // nemmeno nell'intervallo in cui un normale MISS sarebbe visibile.
        Equal(Ball.ShouldDrawLandingFragment(true, true, 0f), false);
        Equal(Ball.ShouldDrawLandingFragment(true, true, 0.5f), false);
        Equal(Ball.ShouldDrawLandingFragment(true, true, 1.75f), false);
        Equal(Ball.ShouldDrawLandingFragment(true, true, 2.99f), false);
        // Ciclo di vita indipendente da ShotResolved: il renderer segue soltanto
        // gli eventi locali HIT o atterraggio e il cronometro della dissolvenza.
        Equal(Ball.IsLocalVisualComplete(false, false, 0f), false);
        Equal(Ball.IsLocalVisualComplete(true, false, 0f), true);
        Equal(Ball.IsLocalVisualComplete(true, true, 0f), true);
        Equal(Ball.IsLocalVisualComplete(false, true, 0f), false);
        Equal(Ball.IsLocalVisualComplete(false, true, 0.5f), false);
        Equal(Ball.IsLocalVisualComplete(false, true, 2.999f), false);
        Equal(Ball.IsLocalVisualComplete(false, true, 3f), true);
        Equal(Ball.IsLocalVisualComplete(false, true, 9f), true);
        // Identica scadenza locale per HIT, ostacolo ed espirazione.
        foreach (var outcome in new[] { ShotOutcome.Penguin, ShotOutcome.Obstacle, ShotOutcome.Expired })
        {
            uint impact = FreezeTimeline.ComputeImpactTick(180, 12);
            Equal(ShotFlightTiming.IsFutureImpact(impact, 180), true);
            Equal(ShotFlightTiming.IsFutureImpact(impact, 191), true);
            Equal(ShotFlightTiming.IsFutureImpact(impact, 192), false);
            Equal(ShotFlightTiming.IsFutureImpact(impact, 195), false);
        }
    }

    private static void FlightTickQuantization(Reconciler r)
    {
        Equal(ShotFlightTiming.LandingTicks(0.5f, 30), 15u);
        Equal(ShotFlightTiming.LandingTicks(MathF.BitIncrement(0.5f), 30), 15u);
        Equal(ShotFlightTiming.LandingTicks(0.5001f, 30), 16u);
        Equal(ShotFlightTiming.LandingTicks(0.86602545f, 30), 26u);
        Equal(ShotFlightTiming.HasLanded(180, 194, 0.50000006f, 30), false);
        Equal(ShotFlightTiming.HasLanded(180, 195, 0.50000006f, 30), true);
        Equal(ShotFlightTiming.HasLanded(180, 196, 0.50000006f, 30), true);
    }

    private static void RemoteShotAndHitSameTcpBatch(Reconciler r)
    {
        byte[] remote = new byte[22];
        remote[0] = (byte)MessageType.RemoteShot;
        remote[13] = (byte)ShotType.Left;
        BinaryPrimitives.WriteUInt32LittleEndian(remote.AsSpan(14, 4), 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(remote.AsSpan(18, 4), 33u);
        var resolved = ResolutionPacket(owner: 1, shotTick: 33, outcome: ShotOutcome.Penguin, hitPlayerId: 2);
        var framer = new NetworkPacketFramer();
        framer.Append(remote.Concat(resolved).ToArray());
        if (!framer.TryRead(out byte type1, out byte[] startPayload) || type1 != (byte)MessageType.RemoteShot)
            throw new Exception("RemoteShot must be delivered before ShotResolved");
        var shot = RemoteShotMessage.Parse(startPayload);
        uint localObservedStart = 180;
        if (!framer.TryRead(out byte type2, out byte[] resolvedPayload) || type2 != (byte)MessageType.ShotResolved)
            throw new Exception("ShotResolved missing from same TCP batch");
        var hit = ShotResolutionMessage.Parse(resolvedPayload);
        Equal(hit.Key, shot.Key);
        Equal(hit.FlightTicks, 12u);
        uint hitTick = FreezeTimeline.ComputeImpactTick(localObservedStart, hit.FlightTicks);
        Equal(hitTick, 192u);
        var freeze = new FreezeTimeline();
        freeze.ConfirmLocal(shot.Key, hitTick);
        Equal(freeze.IsFrozen(180), false);
        Equal(freeze.IsFrozen(191), false);
        Equal(freeze.IsFrozen(192), true);
        Equal(freeze.IsFrozen(281), true);
        Equal(freeze.IsFrozen(282), false);
        Equal(freeze.IsFrozen(300), false);
    }

    private static void AuthoritativeFreezeBoundaries(Reconciler r)
    {
        var freeze = new FreezeTimeline();
        var key = new ShotKey(1, 7);
        freeze.ConfirmLocal(key, 150);
        Equal(freeze.IsFrozen(140), false);
        Equal(freeze.IsFrozen(149), false);
        Equal(freeze.IsFrozen(150), true);
        Equal(freeze.IsFrozen(167), true);
        Equal(freeze.IsFrozen(239), true);
        Equal(freeze.IsFrozen(240), false);
        Equal(freeze.IsFrozen(300), false);
        Equal(Math.Max(0L, 240L - 167L), 73L);
        Equal(Math.Max(0L, 240L - 300L), 0L);
    }

    private static void ConfirmedFreezeReplacesPrediction(Reconciler r)
    {
        var freeze = new FreezeTimeline();
        var first = new ShotKey(1, 7);
        var second = new ShotKey(2, 8);
        freeze.Predict(first, 140);
        Equal(freeze.IsFrozen(145), true);
        Equal(freeze.TryGetPredictedHitTick(first, out uint predictedTick), true);
        Equal(predictedTick, 140u);
        freeze.ConfirmLocal(first, 150);
        Equal(freeze.IsFrozen(145), false);
        Equal(freeze.IsFrozen(150), true);
        Equal(freeze.IsFrozen(240), false);
        freeze.ConfirmLocal(second, 200);
        Equal(freeze.IsFrozen(241), true);
        Equal(freeze.IsFrozen(290), false);
    }

    private static void LocalFreezeReplayBoundary(Reconciler r)
    {
        for (uint seq = 149; seq <= 241; seq++)
            r.RecordInputForReplay(seq, StateList.Right);
        r.MarkInputsAsFrozen(150, 239);
        r.ReceiveAuthoritativePosition(148, new Vector2(100, 30));
        var pos = new Vector2(500, 30);
        Apply(r, ref pos);
        At(pos, 100 + 3 * Step, 30);
    }

    // Il pinguino remoto si blocca subito e riparte se l'HIT viene smentito.
    private static void RemotePredictedFreeze(Reconciler r)
    {
        var timeline = new FreezeTimeline();
        var manager = new MovementsManagerRed();
        var input = new StateStruct();
        var key = new ShotKey(1, 77);

        timeline.Predict(key, 100);
        manager.UpdateInput(ref input, timeline.IsFrozen(101), false, Dt, Vector2.Zero);
        Equal(input.IsPressed(StateList.Freezing), true);

        timeline.RejectPrediction(key);
        manager.UpdateInput(ref input, timeline.IsFrozen(102), false, Dt, Vector2.Zero);
        Equal(input.IsPressed(StateList.Freezing), false);
    }

    // Verifica che il replay non attraversi gli ostacoli.
    private static void ObstacleCollision(Reconciler r)
    {
        // Speed 30 => 1 pixel/step; all candidates at X>=2 are blocked.
        r.RecordInputForReplay(1, StateList.Right);
        r.RecordInputForReplay(2, StateList.Right);
        r.RecordInputForReplay(3, StateList.Right);
        r.ReceiveAuthoritativePosition(0, Vector2.Zero);
        var pos = new Vector2(30, 0);
        r.ReconcileLocalPosition(ref pos, 30f, Dt, candidate => candidate.X >= 1.99f);
        At(pos, 1, 0);
    }

    // Verifica la normalizzazione del movimento diagonale.
    private static void DiagonalMovement(Reconciler r)
    {
        r.RecordInputForReplay(1, StateList.Right | StateList.Up);
        r.ReceiveAuthoritativePosition(0, Vector2.Zero);
        var pos = new Vector2(40, 0);
        Apply(r, ref pos);
        var expected = Step / MathF.Sqrt(2f);
        At(pos, expected, -expected);
    }

    // Verifica il recupero degli input accumulati.
    private static void InputBurst(Reconciler r)
    {
        for (uint seq = 1; seq <= 20; seq++)
            r.RecordInputForReplay(seq, StateList.Right);
        r.ReceiveAuthoritativePosition(10, new Vector2(10 * Step, 0));
        var pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 20 * Step, 0);
    }

    // Verifica due ACK successivi senza perdere input.
    private static void ConsecutiveAcknowledgements(Reconciler r)
    {
        for (uint seq = 1; seq <= 20; seq++)
            r.RecordInputForReplay(seq, StateList.Right);

        r.ReceiveAuthoritativePosition(10, new Vector2(10 * Step, 0));
        var pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 20 * Step, 0);

        for (uint seq = 21; seq <= 30; seq++)
            r.RecordInputForReplay(seq, StateList.Right);

        r.ReceiveAuthoritativePosition(20, new Vector2(20 * Step, 0));
        Apply(r, ref pos);
        At(pos, 30 * Step, 0);
    }

    // Verifica la pulizia degli input tra due partite.
    private static void ResetClearsPending(Reconciler r)
    {
        r.RecordInputForReplay(1, StateList.Right);
        r.Reset();
        r.ReceiveAuthoritativePosition(0, Vector2.Zero);
        var pos = new Vector2(20, 0);
        Apply(r, ref pos);
        At(pos, 0, 0);
    }

    private readonly record struct Snapshot(uint Ack, Vector2 Position);

    // Verifica che venga usato solo lo snapshot piu recente.
    private static void LatestSnapshot(Reconciler r)
    {
        var queue = new ConcurrentQueue<Snapshot>();
        queue.Enqueue(new Snapshot(1, Vector2.One));
        queue.Enqueue(new Snapshot(2, Vector2.One * 2));
        queue.Enqueue(new Snapshot(3, Vector2.One * 3));
        var count = 0;
        r.ConsumeLatestSnapshot(queue, snapshot =>
        {
            count++;
            Equal(snapshot.Ack, 3u);
            At(snapshot.Position, 3, 3);
        });
        Equal(count, 1);
        Equal(queue.Count, 0);
    }

    // Verifica che ID giocatore e tick distinguano le palle.
    private static void ShotKeyEquality(Reconciler r)
    {
        var key = new ShotKey(1, 33);
        Equal(key, new ShotKey(1, 33));
        if (key == new ShotKey(2, 33) || key == new ShotKey(1, 34))
            throw new Exception("ShotKey collision between different shooter/tick pairs");
    }

    // Verifica unicita e lunghezza dei tag delle palle.
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

    // Verifica la lettura del messaggio di tiro remoto.
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

    // Verifica il rifiuto di un messaggio incompleto.
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

    // Prepara un pacchetto di esito per i test.
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
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(18, 4), 40u);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(22, 4), BitConverter.SingleToInt32Bits(123.5f));
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(26, 4), BitConverter.SingleToInt32Bits(45.25f));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(30, 4), 12u);
        return packet;
    }

    // Verifica la lettura dei tre esiti possibili.
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
            Equal(decoded.HitAck, 40u);
            Equal(decoded.FlightTicks, 12u);
            At(decoded.HitPosition, 123.5f, 45.25f);
        }
    }

    // Verifica il rifiuto degli esiti incoerenti.
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

    // Verifica un esito TCP ricevuto un byte alla volta.
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

    // Verifica messaggi accorpati e spezzati.
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

    // Verifica il rifiuto di tipi di messaggio sconosciuti.
    private static void FramingRejectsUnknown(Reconciler r)
    {
        var framer = new NetworkPacketFramer();
        framer.Append(new byte[] { 255 });
        try { framer.TryRead(out _, out _); }
        catch (System.IO.InvalidDataException) { return; }
        throw new Exception("Unknown message was silently accepted");
    }

    // Verifica che la stessa auth non venga applicata due volte.
    private static void SingleUseAuth(Reconciler r)
    {
        r.ReceiveAuthoritativePosition(1, Vector2.Zero);
        var pos = new Vector2(30, 0);
        Apply(r, ref pos);
        At(pos, 0, 0);
        pos = new Vector2(40, 0);
        Apply(r, ref pos);
        At(pos, 40, 0);
    }
}
