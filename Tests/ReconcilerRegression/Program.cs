using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            ("Durante il freeze gli ACK avanzano senza spostare il pinguino", FrozenPositionSkipsReplay),
            ("Replay matches prediction: no correction", AccuratePrediction),
            ("Dead zone ignores sub-2px mismatch", DeadZone),
            ("Soft correction interpolates medium mismatch", SoftCorrection),
            ("Large mismatch snaps to replayed position", HardCorrection),
            ("Only reload and freezing stop movement in replay", ActionLocks),
            ("Local movement continues during pickup, deposit and drop", MovementLocksMatchEggActions),
            ("Egg actions do not stop movement in position replay", EggReleaseMatchesReplay),
            ("Egg pickup requires continuous hitbox overlap", PickupRequiresContinuousOverlap),
            ("Locally frozen input masks skip movement in replay", RecordedFrozenInputs),
            ("HIT confirmation does not assume HitAck+90 input freeze", HitConfirmationDoesNotFreezeByAck),
            ("New input replay respects each locally recorded freeze mask", LocalFreezeMasksControlFutureReplay),
            ("Predicted freeze, confirmed freeze and false HIT use tick deadlines", FreezeTimelines),
            ("Server flight duration maps to independent local timelines", FlightTimelineAcrossClients),
            ("Quantized flight duration does not gain a ghost tick", FlightTickQuantization),
            ("Freeze ice fades across the 90 active ticks only", FreezeVisualFade),
            ("Ball fragments expire and terminal outcomes do not remove early", BallVisualLifecycleBoundaries),
            ("Remote shot before hit even when TCP messages arrive together", RemoteShotAndHitSameTcpBatch),
            ("Confirmed freeze is half-open and late confirmations never restart it", AuthoritativeFreezeBoundaries),
            ("Expired authoritative HIT does not drop a newly carried egg", ExpiredHitDoesNotDropEgg),
            ("Confirmed HIT drops eggs at the same authoritative impact position", ConfirmedHitEggDropPosition),
            ("Un HIT confermato in anticipo aspetta il tick di impatto", FutureHitWaitsForImpact),
            ("Predicted freeze cannot drop an egg before HIT confirmation", PredictedFreezeDoesNotDropEgg),
            ("Remote WithEgg loss during freeze waits for HIT confirmation", RemoteEggWaitsForConfirmedHit),
            ("Un HIT remoto ignora vecchi stati WithEgg", RemoteEggRequiresNewPickupAfterHit),
            ("Local egg drop on E fires once, retains carried tag until event", LocalEggDropOnE),
            ("Frozen penguins cannot pick up eggs", FrozenPenguinsCannotPickUpEggs),
            ("Multiple eggs: pick the smallest numeric ID exactly once", MultipleEggsPickLowestIdOnce),
            ("Multiple eggs: lock target throughout the pickup timer", MultipleEggsKeepLockedTarget),
            ("Multiple eggs: leaving locked egg resets pickup timer", MultipleEggsResetWhenTargetLeaves),
            ("Multiple eggs: remote picks lowest ID only after network confirmation", RemoteMultipleEggsNeedNetworkConfirmation),
            ("Remote egg pickup uses only WithEgg received from network", RemotePickupNeedsNetworkConfirmation),
            ("Remote egg drop notifies before clearing its tag", RemoteEggDropPreservesTag),
            ("Egg delivery emits only one score event", EggDeliveryScoresOnlyOnce),
            ("Egg delivery needs uninterrupted platform overlap", EggDeliveryRequiresContinuousOverlap),
            ("Authoritative tick replaces an incorrect predicted impact", ConfirmedFreezeReplacesPrediction),
            ("Movement replay skips exactly the locally recorded frozen ticks", LocalFreezeReplayBoundary),
            ("Remote penguin obeys predicted freeze and clears it on rejection", RemotePredictedFreeze),
            ("Remote snapshots respect authoritative freeze", RemoteSnapshotBlockedByServerFreeze),
            ("Penetration resolution is replayed every tick", ObstacleCollision),
            ("Native penetration resolution does not alter collider state", CollisionQueryIsReadOnly),
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
            ("Server match status packet survives fragmented TCP", MatchStatusTcpFraming),
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

    // Il server fornisce la stessa HitPosition a entrambi i client:
    // il drop deve leggerla sia per il pinguino locale sia per quello remoto.
    private static void ConfirmedHitEggDropPosition(Reconciler reconciler)
    {
        static Penguin DummyPenguin(string tag, Vector2 start)
        {
            var penguin = (Penguin)RuntimeHelpers.GetUninitializedObject(typeof(Penguin));
            typeof(Penguin).GetField("LocalFreeze")!.SetValue(penguin, new FreezeTimeline());
            penguin._penguinColliderHandler = new PenguinColliderHandler(tag);
            penguin._position = start;
            return penguin;
        }

        foreach (bool localVictim in new[] { true, false })
        {
            reconciler.Reset();
            var local = DummyPenguin("penguin", new Vector2(400, 300));
            var remote = DummyPenguin("penguinRed", new Vector2(700, 210));
            var victim = localVictim ? local : remote;
            victim._myEgg = "egg7";
            victim._penguinColliderHandler.isWithEgg = true;

            var match = (Events)RuntimeHelpers.GetUninitializedObject(typeof(Events));
            typeof(Events).GetField("PendingShotResolutions")!
                .SetValue(match, new Queue<ShotResolutionMessage>());

            ShotKey key = new(localVictim ? 2u : 1u, 197u);
            if (localVictim)
            {
                typeof(Events).GetField("_remoteShotStartTicks", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(match, new Dictionary<ShotKey, uint> { [key] = 197u });
            }

            Vector2 droppedAt = default;
            string? eggDuringDrop = null;
            int dropCount = 0;
            victim._penguinColliderHandler.eggPutEvent += (_, _) =>
            {
                droppedAt = victim._position;
                eggDuringDrop = victim._myEgg;
                dropCount++;
            };

            reconciler.AttachMatchContext(1, local, remote, match);
            match.PendingShotResolutions.Enqueue(new ShotResolutionMessage(
                key, ShotOutcome.Penguin, localVictim ? 1u : 2u, 199u, 196u,
                new Vector2(123, 87), 2u));
            reconciler.ReadAuthShot(200);
            // Leggere il verdetto non deve ancora applicare la caduta.
            Equal(dropCount, 0);
            reconciler.ReconcileShot(200);

            Equal(dropCount, 1);
            Equal(eggDuringDrop, "egg7");
            At(droppedAt, 123, 87);
            Equal(victim._myEgg, (string?)null);
            Equal(victim._penguinColliderHandler.isWithEgg, false);
        }
    }

    // La conferma puo' arrivare prima dell'impatto: il freeze e' programmato,
    // ma la perdita dell'uovo avviene soltanto quando il tick viene oltrepassato.
    private static void FutureHitWaitsForImpact(Reconciler reconciler)
    {
        static Penguin DummyPenguin(string tag, Vector2 start)
        {
            var penguin = (Penguin)RuntimeHelpers.GetUninitializedObject(typeof(Penguin));
            typeof(Penguin).GetField("LocalFreeze")!.SetValue(penguin, new FreezeTimeline());
            penguin._penguinColliderHandler = new PenguinColliderHandler(tag);
            penguin._position = start;
            return penguin;
        }

        var local = DummyPenguin("penguin", new Vector2(300, 200));
        var remote = DummyPenguin("penguinRed", new Vector2(400, 250));
        remote._myEgg = "egg6";
        remote._penguinColliderHandler.isWithEgg = true;
        int drops = 0;
        remote._penguinColliderHandler.eggPutEvent += (_, _) => drops++;

        var match = (Events)RuntimeHelpers.GetUninitializedObject(typeof(Events));
        typeof(Events).GetField("PendingShotResolutions")!
            .SetValue(match, new Queue<ShotResolutionMessage>());
        reconciler.AttachMatchContext(1, local, remote, match);

        match.PendingShotResolutions.Enqueue(new ShotResolutionMessage(
            new ShotKey(1, 200), ShotOutcome.Penguin, 2, 205, 200,
            new Vector2(123, 87), 5));
        reconciler.ReadAuthShot(202);
        Equal(remote.LocalFreeze.IsFrozen(205), true);
        Equal(drops, 0);
        At(remote._position, 400, 250);

        reconciler.ReconcileShot(205); // Il tick d'impatto non e' ancora completato.
        Equal(drops, 0);
        Equal(remote._myEgg, "egg6");

        reconciler.ReconcileShot(206);
        Equal(drops, 1);
        Equal(remote._myEgg, (string?)null);
        At(remote._position, 123, 87);

        reconciler.ReconcileShot(207);
        Equal(drops, 1);
    }

    private static void PredictedFreezeDoesNotDropEgg(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguin") { isWithEgg = true };
        string egg = "egg3";
        int drops = 0;
        collider.eggPutEvent += (_, _) => drops++;

        // Stesso frame: WithEgg sparisce dal mask e Freezing compare.
        // Un HIT solamente predetto non deve causare un drop.
        var state = new StateStruct { Old = StateList.WithEgg, Current = StateList.Freezing };
        collider.putEgg(state, ref egg);
        Equal(drops, 0);
        Equal(egg, "egg3");
        Equal(collider.isWithEgg, true);

        collider.HandleHitByBall(ref egg);
        Equal(drops, 1);
        Equal(egg, (string?)null);
    }

    private static void RemoteEggWaitsForConfirmedHit(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguinRed") { isWithEgg = true };
        string egg = "egg4";
        int drops = 0;
        collider.eggPutEvent += (_, _) => drops++;

        var state = new StateStruct { Current = StateList.Freezing };
        collider.SyncStateWithNetwork(state, ref egg);
        Equal(drops, 0);
        Equal(egg, "egg4");

        // Il bit di freeze del server arriva anche prima della conseguenza
        // pianificata sulla timeline locale.
        state.Current = StateList.None;
        collider.SyncStateWithNetwork(state, ref egg, serverFrozen: true);
        Equal(drops, 0);
        Equal(egg, "egg4");

        collider.HandleHitByBall(ref egg);
        Equal(drops, 1);
        Equal(egg, (string?)null);
        collider.SyncStateWithNetwork(state, ref egg);
        Equal(drops, 1);
    }

    // I vecchi snapshot WithEgg non possono far sparire nuovamente l'uovo dopo un HIT.
    private static void RemoteEggRequiresNewPickupAfterHit(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguinRed") { isWithEgg = true };
        string egg = "egg4";
        int drops = 0;
        int pickups = 0;
        collider.eggPutEvent += (_, _) => drops++;
        collider.eggTakenEvent += (_, _) => pickups++;
        var eggOnGround = new List<string> { "egg4" };

        collider.HandleHitByBall(ref egg);
        Equal(drops, 1);
        Equal(egg, (string?)null);
        collider.isFrozen = false;

        // Vecchio WithEgg dopo la conferma: non e' una nuova raccolta.
        var stale = new StateStruct { Current = StateList.WithEgg };
        collider.HandleEggPickup(eggOnGround, stale, Dt, ref egg);
        Equal(pickups, 0);
        Equal(egg, (string?)null);
        Equal(drops, 1);

        // Il server comunica la perdita dell'uovo anche se il pinguino e' congelato.
        collider.isFrozen = true;
        var released = new StateStruct { Current = StateList.Freezing };
        collider.HandleEggPickup(eggOnGround, released, Dt, ref egg);
        Equal(pickups, 0);
        collider.isFrozen = false;

        // Solo un nuovo WithEgg dopo il rilascio autorizza un pickup.
        collider.HandleEggPickup(eggOnGround, stale, Dt, ref egg);
        Equal(pickups, 1);
        Equal(egg, "egg4");
        Equal(collider.isWithEgg, true);
        Equal(drops, 1);
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
        collider.HandleEggPickup(["egg5"], takingEgg, 1.1f, ref egg);
        Equal(picks, 0);
        Equal(egg == null, true);

        collider.isFrozen = false;
        takingEgg.Current |= StateList.Freezing;
        collider.HandleEggPickup(["egg5"], takingEgg, 1.1f, ref egg);
        Equal(picks, 0);
        Equal(egg == null, true);

        takingEgg.Current &= ~StateList.Freezing;
        collider.HandleEggPickup(["egg5"], takingEgg, 1.1f, ref egg);
        Equal(picks, 1);
        Equal(egg, "egg5");
    }

    // Più collisioni nello stesso frame non devono accelerare il timer.
    // L'ID viene confrontato numericamente: egg3 precede egg10 ed egg12.
    private static void MultipleEggsPickLowestIdOnce(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguin");
        var state = new StateStruct { Current = StateList.TakingEgg };
        string egg = null!;
        int picks = 0;
        string pickedTag = "";
        collider.eggTakenEvent += (_, tag) => { picks++; pickedTag = tag; };

        collider.HandleEggPickup(["egg12", "egg3", "egg10"], state, 0.40f, ref egg);
        collider.HandleEggPickup(["egg10", "egg12", "egg3"], state, 0.40f, ref egg);
        Equal(picks, 0);
        collider.HandleEggPickup(["egg12", "egg10", "egg3"], state, 0.21f, ref egg);
        Equal(picks, 1);
        Equal(pickedTag, "egg3");
        Equal(egg, "egg3");
        Equal(collider.isWithEgg, true);

        state.Current |= StateList.WithEgg;
        collider.HandleEggPickup(["egg12", "egg10"], state, 1.1f, ref egg);
        Equal(picks, 1);
    }

    // Un uovo con ID più basso che entra nella collisione NON sostituisce
    // quello selezionato quando il giocatore ha iniziato a raccogliere.
    private static void MultipleEggsKeepLockedTarget(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguin");
        var state = new StateStruct { Current = StateList.TakingEgg };
        string egg = null!;
        collider.HandleEggPickup(["egg12", "egg9"], state, 0.65f, ref egg);
        collider.HandleEggPickup(["egg1", "egg12", "egg9"], state, 0.36f, ref egg);
        Equal(egg, "egg9");
    }

    // Se perdiamo il contatto con l'uovo bloccato, il tempo pregresso
    // non può essere trasferito all'altro uovo rimasto in collisione.
    private static void MultipleEggsResetWhenTargetLeaves(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguin");
        var state = new StateStruct { Current = StateList.TakingEgg };
        string egg = null!;
        int picks = 0;
        collider.eggTakenEvent += (_, _) => picks++;

        collider.HandleEggPickup(["egg2", "egg8"], state, 0.65f, ref egg);
        collider.HandleEggPickup(["egg8"], state, 0.35f, ref egg);
        collider.HandleEggPickup(["egg8"], state, 0.60f, ref egg);
        Equal(picks, 0);
        Equal(egg == null, true);
        collider.HandleEggPickup(["egg8"], state, 0.41f, ref egg);
        Equal(picks, 1);
        Equal(egg, "egg8");
    }

    // Il remoto non simula il timer: attende WithEgg e, in presenza
    // di più uova, usa la stessa selezione deterministica per ID.
    private static void RemoteMultipleEggsNeedNetworkConfirmation(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguinRed");
        var state = new StateStruct { Current = StateList.TakingEgg };
        string egg = null!;
        int picks = 0;
        collider.eggTakenEvent += (_, _) => picks++;

        collider.HandleEggPickup(["egg11", "egg2"], state, 2f, ref egg);
        Equal(picks, 0);
        state.Current = StateList.WithEgg;
        collider.HandleEggPickup(["egg11", "egg2"], state, 0.01f, ref egg);
        Equal(picks, 1);
        Equal(egg, "egg2");
        collider.HandleEggPickup(["egg11", "egg2"], state, 0.01f, ref egg);
        Equal(picks, 1);
    }

    private static void RemotePickupNeedsNetworkConfirmation(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguinRed");
        string egg = default!;
        int picks = 0;
        collider.eggTakenEvent += (_, _) => picks++;

        var networkState = new StateStruct { Current = StateList.TakingEgg };
        collider.HandleEggPickup(["egg8"], networkState, 3f, ref egg);
        Equal(picks, 0);
        Equal(egg == null, true);

        networkState.Current |= StateList.WithEgg;
        collider.isFrozen = true;
        collider.HandleEggPickup(["egg8"], networkState, 3f, ref egg);
        Equal(picks, 0);

        collider.isFrozen = false;
        collider.HandleEggPickup(["egg8"], networkState, 0.1f, ref egg);
        Equal(picks, 1);
        Equal(egg, "egg8");
        collider.HandleEggPickup(["egg8"], networkState, 0.1f, ref egg);
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

    // Se esci dalla piattaforma mentre tieni Spazio, il secondo riparte da zero.
    private static void EggDeliveryRequiresContinuousOverlap(Reconciler reconciler)
    {
        var collider = new PenguinColliderHandler("penguin") { isWithEgg = true };
        string egg = "egg8";
        int deliveries = 0;
        collider.eggDeleteEvent += (_, _) => deliveries++;
        var state = new StateStruct { Current = StateList.WithEgg | StateList.PuttingEgg };
        collider.HandleEggDelivery("blueP", ref state, 0.6f, ref egg);
        collider.HandleEggDelivery(null, ref state, 0.2f, ref egg);
        collider.HandleEggDelivery("blueP", ref state, 0.5f, ref egg);
        Equal(deliveries, 0);
        collider.HandleEggDelivery("blueP", ref state, 0.51f, ref egg);
        Equal(deliveries, 1);
        // Dopo il punto, il cleanup di 0.2 s termina anche uscendo dalla base.
        collider.HandleEggDelivery(null, ref state, 0.21f, ref egg);
        Equal(egg == null, true);
        Equal(collider.isWithEgg, false);
    }

    // Il verdetto finale deve essere letto solo quando tutto il payload TCP e' arrivato.
    private static void MatchStatusTcpFraming(Reconciler reconciler)
    {
        byte[] packet = new byte[17];
        packet[0] = (byte)MessageType.MatchStatus;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1, 4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(5, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(9, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(13, 4), 1);
        var framer = new NetworkPacketFramer();
        framer.Append(packet.AsSpan(0, 5));
        Equal(framer.TryRead(out _, out _), false);
        framer.Append(packet.AsSpan(5));
        Equal(framer.TryRead(out byte type, out byte[] payload), true);
        Equal(type, (byte)MessageType.MatchStatus);
        Equal(payload.Length, 16);
        Equal(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, 4)), 3u);
        Equal(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4, 4)), 2u);
        Equal(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(8, 4)), 0u);
        Equal(BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(12, 4)), 1u);
        Equal(framer.TryRead(out _, out _), false);
    }

    private static void FreezeVisualFade(Reconciler reconciler)
    {
        var timeline = new FreezeTimeline();
        var first = new ShotKey(1, 10);
        timeline.Predict(first, 100);
        Equal(timeline.GetVisualOpacity(99), 0f);
        Equal(timeline.GetVisualOpacity(100), 1f);
        Equal(MathF.Abs(timeline.GetVisualOpacity(145) - 0.5f) < 0.0001f, true);
        Equal(MathF.Abs(timeline.GetVisualOpacity(189) - 1f / 90f) < 0.0001f, true);
        Equal(timeline.GetVisualOpacity(190), 0f);

        // Un altro HIT ricrea il ghiaccio; un MISS lo elimina senza code grafiche.
        var second = new ShotKey(2, 20);
        timeline.Predict(second, 160);
        Equal(timeline.GetVisualOpacity(160), 1f);
        timeline.RejectPrediction(second);
        Equal(MathF.Abs(timeline.GetVisualOpacity(160) - 30f / 90f) < 0.0001f, true);
        timeline.RejectPrediction(first);
        Equal(timeline.GetVisualOpacity(160), 0f);
        timeline.ConfirmLocal(new ShotKey(1, 30), 200);
        Equal(timeline.GetVisualOpacity(200), 1f);
        Equal(timeline.GetVisualOpacity(290), 0f);
    }

    // Controlla che due valori siano uguali.
    private static void Equal<T>(T actual, T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new Exception($"Expected {expected}, got {actual}");
    }

    // Un esito vecchio non deve intervenire sulle uova attualmente trasportate.
    private static void ExpiredHitDoesNotDropEgg(Reconciler reconciler)
    {
        static Penguin DummyPenguin(string tag)
        {
            var penguin = (Penguin)RuntimeHelpers.GetUninitializedObject(typeof(Penguin));
            typeof(Penguin).GetField("LocalFreeze")!.SetValue(penguin, new FreezeTimeline());
            penguin._penguinColliderHandler = new PenguinColliderHandler(tag);
            return penguin;
        }

        var local = DummyPenguin("penguin");
        var opponent = DummyPenguin("penguinRed");
        var events = (Events)RuntimeHelpers.GetUninitializedObject(typeof(Events));
        typeof(Events).GetField("PendingShotResolutions")!.SetValue(
            events, new Queue<ShotResolutionMessage>());
        reconciler.AttachMatchContext(1, local, opponent, events);

        local._myEgg = "egg42";
        local._penguinColliderHandler.isWithEgg = true;

        // HIT avvenuto al tick 12: al tick 200 i 90 tick di freeze sono scaduti.
        events.PendingShotResolutions.Enqueue(new ShotResolutionMessage(
            new ShotKey(1, 10), ShotOutcome.Penguin, 1, 12, 10,
            new Vector2(50, 50), 2));
        reconciler.ReadAuthShot(200);
        reconciler.ReconcileShot(200);
        Equal(local._myEgg, "egg42");
        Equal(local._penguinColliderHandler.isWithEgg, true);

        // HIT recente: il server impone invece il rilascio dell'uovo.
        events.PendingShotResolutions.Enqueue(new ShotResolutionMessage(
            new ShotKey(1, 197), ShotOutcome.Penguin, 1, 198, 11,
            new Vector2(50, 50), 2));
        reconciler.ReadAuthShot(200);
        Equal(local._myEgg, "egg42");
        reconciler.ReconcileShot(200);
        Equal(local._myEgg, (string?)null);
        Equal(local._penguinColliderHandler.isWithEgg, false);
    }

    // Applica il Reconciler con i parametri di movimento del gioco.
    private static void Apply(Reconciler r, ref Vector2 position, Func<Vector2, Vector2>? resolveOverlap = null)
        => r.ReconcilePosition(ref position, Speed, Dt, resolveOverlap);

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
            r.RecordPendingInput(tick, StateList.None);
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
        r.RecordPendingInput(105, StateList.Right);
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
        r.RecordPendingInput(105, StateList.None); // client sent neutral movement while predicting HIT
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
        r.RecordPendingInput(1, StateList.Right);
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
        r.RecordPendingInput(1, StateList.Right);
        r.RecordPendingInput(2, StateList.Up);
        r.RecordPendingInput(3, StateList.Right);
        r.ReceiveAuthoritativePosition(2, new Vector2(100, 100));
        var pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 100 + Step, 100);
    }

    // Durante il freeze il client conserva la posizione dell'impatto, ma elimina
    // comunque gli input confermati. Dopo lo sblocco riprende il normale replay.
    private static void FrozenPositionSkipsReplay(Reconciler r)
    {
        var position = new Vector2(50, 30); // Posizione dell'HIT gia' applicata.
        r.RecordPendingInput(101, StateList.Right);
        r.RecordPendingInput(102, StateList.Right);
        r.RecordPendingInput(103, StateList.Freezing | StateList.Right);

        // Uno snapshot piu' recente non deve spostare il pinguino congelato.
        r.ReceiveAuthoritativePosition(102, new Vector2(300, 300));
        r.ReconcilePosition(ref position, Speed, Dt, isCurrentlyFrozen: true);
        At(position, 50, 30);

        // Gli ACK sono stati consumati anche se il replay e' sospeso.
        var inputs = (System.Collections.IList)typeof(Reconciler)
            .GetField("_unacknowledgedMovementInputs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(r)!;
        Equal(inputs.Count, 1);

        // Lo sblocco non deve rieseguire gli input vecchi o congelati.
        r.RecordPendingInput(104, StateList.Freezing | StateList.Right);
        r.ReceiveAuthoritativePosition(104, new Vector2(50, 30));
        r.ReconcilePosition(ref position, Speed, Dt, isCurrentlyFrozen: true);
        At(position, 50, 30);
        Equal(inputs.Count, 0);

        // Il primo movimento successivo allo sblocco torna a essere applicabile.
        r.RecordPendingInput(105, StateList.Right);
        position.X += Step; // Il movimento viene prima predetto normalmente dal client.
        r.ReceiveAuthoritativePosition(104, new Vector2(50, 30));
        r.ReconcilePosition(ref position, Speed, Dt, isCurrentlyFrozen: false);
        At(position, 50 + Step, 30);
    }

    // Verifica che una prediction corretta non venga modificata.
    // Verifica anche il caso di contatto appena tangente (non collisione).
    private static void CollisionQueryIsReadOnly(Reconciler r)
    {
        var manager = (CollisionManager)RuntimeHelpers.GetUninitializedObject(typeof(CollisionManager));
        var colliders = new List<CollisionRecordIn>
        {
            new("obstacle", 150, 150, 100, 100),
            new("penguin", 100, 100, 96, 64)
        };
        typeof(CollisionManager).GetField("_collisionRecordIns", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(manager, colliders);

        // Risoluzione minima lungo l'asse X, nessun mutamento dei collider.
        At(manager.ResolveObstacleOverlaps(new Vector2(80, 150), 96, 64), 52, 150);
        At(manager.ResolveObstacleOverlaps(new Vector2(220, 150), 96, 64), 248, 150);
        // Appena tangente: nessuna correzione e nessun tremolio.
        At(manager.ResolveObstacleOverlaps(new Vector2(248, 150), 96, 64), 248, 150);
        At(manager.ResolveObstacleOverlaps(new Vector2(300, 300), 96, 64), 300, 300);
        // Risoluzione verticale e collisione non dipendente dall'ordine nella lista.
        At(manager.ResolveObstacleOverlaps(new Vector2(150, 95), 96, 64), 150, 68);
        At(manager.ResolveObstacleOverlaps(new Vector2(150, 205), 96, 64), 150, 232);

        // 120 tick contro il muro: sempre a filo, senza oscillare.
        var contact = new Vector2(52, 150);
        for (int tick = 0; tick < 120; tick++)
        {
            contact.X += Step;
            contact = manager.ResolveObstacleOverlaps(contact, 96, 64);
            At(contact, 52, 150);
        }

        Equal(colliders.Count, 2);
        Equal(colliders[0]._x, 150f);
        Equal(colliders[0]._y, 150f);
        Equal(colliders[1]._x, 100f);
        Equal(colliders[1]._y, 100f);
    }

    private static void AccuratePrediction(Reconciler r)
    {
        r.RecordPendingInput(1, StateList.Right);
        r.RecordPendingInput(2, StateList.Right);
        r.RecordPendingInput(3, StateList.Right);
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
        r.RecordPendingInput(1, StateList.Right | StateList.Reload);
        r.RecordPendingInput(2, StateList.Right | StateList.Freezing);
        r.RecordPendingInput(3, StateList.Right | StateList.TakingEgg);
        r.RecordPendingInput(4, StateList.Right | StateList.PuttingEgg);
        r.RecordPendingInput(5, StateList.Right);
        r.ReceiveAuthoritativePosition(0, Vector2.Zero);
        var pos = new Vector2(100, 0);
        Apply(r, ref pos);
        At(pos, Step * 3, 0);
    }

    // Prediction locale: E raccoglie solo senza uovo, E con uovo lo rilascia subito.
    // Spazio ed E non bloccano il movimento; reload e freeze si'.
    private static void MovementLocksMatchEggActions(Reconciler r)
    {
        var input = new PenguinInputHandler("penguin");
        foreach (var (mask, expectedX) in new (StateList Mask, float X)[]
        {
            (StateList.Right, Step),
            (StateList.Right | StateList.TakingEgg, Step),
            (StateList.Right | StateList.TakingEgg | StateList.WithEgg, Step),
            (StateList.Right | StateList.PuttingEgg | StateList.WithEgg, Step),
            (StateList.Right | StateList.Reload, 0f),
            (StateList.Right | StateList.Freezing, 0f)
        })
        {
            input._stateStruct.Current = mask;
            var position = Vector2.Zero;
            input.UpdateMovement(Dt, ref position);
            At(position, expectedX, 0f);
        }
    }

    // Il replay deve muoversi durante E/Spazio indipendentemente da WithEgg.
    private static void EggReleaseMatchesReplay(Reconciler r)
    {
        r.RecordPendingInput(1, StateList.Right | StateList.TakingEgg | StateList.WithEgg);
        r.RecordPendingInput(2, StateList.Right | StateList.TakingEgg);
        r.RecordPendingInput(3, StateList.Right | StateList.PuttingEgg | StateList.WithEgg);
        r.ReceiveAuthoritativePosition(0, Vector2.Zero);
        var position = new Vector2(100f, 0f);
        Apply(r, ref position);
        At(position, Step * 3, 0f);
    }

    // Allontanarsi da TUTTE le uova interrompe la raccolta, anche tenendo premuto E.
    private static void PickupRequiresContinuousOverlap(Reconciler r)
    {
        var handler = new PenguinColliderHandler("penguin");
        var state = new StateStruct { Current = StateList.TakingEgg };
        string egg = null!;
        var overlaps = new List<string> { "egg5" };
        handler.HandleEggPickup(overlaps, state, 0.6f, ref egg);
        handler.HandleEggPickup(new List<string>(), state, 0.2f, ref egg);
        handler.HandleEggPickup(overlaps, state, 0.5f, ref egg);
        if (handler.isWithEgg || egg != null)
            throw new Exception("Pickup progress survived loss of all hitbox overlap");
        handler.HandleEggPickup(overlaps, state, 0.51f, ref egg);
        if (!handler.isWithEgg || egg != "egg5")
            throw new Exception("Continuous overlap did not complete pickup");
    }

    // Il replay salta soltanto gli input gia' registrati come congelati dal client.
    private static void RecordedFrozenInputs(Reconciler r)
    {
        for (uint seq = 41; seq <= 131; seq++)
            r.RecordPendingInput(seq, seq <= 130 ? StateList.Freezing | StateList.Right : StateList.Right);
        r.ReceiveAuthoritativePosition(40, new Vector2(100, 30));
        var pos = new Vector2(500, 30);
        Apply(r, ref pos);
        At(pos, 100 + Step, 30);
    }

    // La conferma HIT registra l'ACK autoritativo senza attribuire un freeze
    // arbitrario ai 90 input successivi (che potrebbero essere stati generati prima).
    private static void HitConfirmationDoesNotFreezeByAck(Reconciler r)
    {
        var local = (Penguin)RuntimeHelpers.GetUninitializedObject(typeof(Penguin));
        var remote = (Penguin)RuntimeHelpers.GetUninitializedObject(typeof(Penguin));
        typeof(Penguin).GetField("LocalFreeze")!.SetValue(local, new FreezeTimeline());
        typeof(Penguin).GetField("LocalFreeze")!.SetValue(remote, new FreezeTimeline());
        local._penguinColliderHandler = new PenguinColliderHandler("penguin");
        remote._penguinColliderHandler = new PenguinColliderHandler("penguinRed");
        var events = (Events)RuntimeHelpers.GetUninitializedObject(typeof(Events));
        typeof(Events).GetField("PendingShotResolutions")!.SetValue(
            events, new Queue<ShotResolutionMessage>());
        r.AttachMatchContext(1, local, remote, events);

        for (uint seq = 101; seq <= 201; seq++)
            r.RecordPendingInput(seq, StateList.Right);

        events.PendingShotResolutions.Enqueue(new ShotResolutionMessage(
            new ShotKey(1, 198), ShotOutcome.Penguin, 1, 200, 100,
            Vector2.Zero, 2));
        r.ReadAuthShot(201);
        r.ReconcileShot(201);
        var position = new Vector2(600, 0);
        Apply(r, ref position);
        At(position, 101 * Step, 0);
    }

    // Il client registra la propria prediction del freeze sull'input corrente.
    // Non deve usare l'ACK del server per congelare ulteriori input futuri.
    private static void LocalFreezeMasksControlFutureReplay(Reconciler r)
    {
        for (uint seq = 130; seq <= 205; seq++)
            r.RecordPendingInput(seq, seq < 190 ? StateList.Freezing | StateList.Right : StateList.Right);
        r.ReceiveAuthoritativePosition(100, Vector2.Zero);
        var position = new Vector2(500, 0);
        Apply(r, ref position);
        At(position, 16 * Step, 0);
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
            r.RecordPendingInput(seq, seq >= 150 && seq <= 239 ? StateList.Freezing | StateList.Right : StateList.Right);
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

    // Frozen server snapshots must not move an unfrozen-looking remote.
    private static void RemoteSnapshotBlockedByServerFreeze(Reconciler r)
    {
        var remote = (Penguin)RuntimeHelpers.GetUninitializedObject(typeof(Penguin));
        typeof(Penguin).GetField("LocalFreeze")!.SetValue(remote, new FreezeTimeline());
        remote._position = new Vector2(100f, 200f);
        var events = (Events)RuntimeHelpers.GetUninitializedObject(typeof(Events));
        typeof(Events).GetField("_redPenguin", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(events, remote);
        var receive = typeof(Events).GetMethod("HandleRedState",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        receive.Invoke(events, new object[] { 180f, 240f, (int)StateList.Freezing });
        At(remote._position, 100f, 200f);
        At(events.LatestRemotePosition, 180f, 240f);

        receive.Invoke(events, new object[] { 220f, 250f, (int)StateList.Right });
        At(remote._position, 220f, 250f);
    }

    // Verifica che il replay non attraversi gli ostacoli.
    private static void ObstacleCollision(Reconciler r)
    {
        // Speed 30 => 1 pixel/step; il muro limita la posizione al bordo X=1.
        r.RecordPendingInput(1, StateList.Right);
        r.RecordPendingInput(2, StateList.Right);
        r.RecordPendingInput(3, StateList.Right);
        r.ReceiveAuthoritativePosition(0, Vector2.Zero);
        var pos = new Vector2(30, 0);
        r.ReconcilePosition(ref pos, 30f, Dt,
            candidate => new Vector2(MathF.Min(candidate.X, 1f), candidate.Y));
        At(pos, 1, 0);
    }

    // Verifica la normalizzazione del movimento diagonale.
    private static void DiagonalMovement(Reconciler r)
    {
        r.RecordPendingInput(1, StateList.Right | StateList.Up);
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
            r.RecordPendingInput(seq, StateList.Right);
        r.ReceiveAuthoritativePosition(10, new Vector2(10 * Step, 0));
        var pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 20 * Step, 0);
    }

    // Verifica due ACK successivi senza perdere input.
    private static void ConsecutiveAcknowledgements(Reconciler r)
    {
        for (uint seq = 1; seq <= 20; seq++)
            r.RecordPendingInput(seq, StateList.Right);

        r.ReceiveAuthoritativePosition(10, new Vector2(10 * Step, 0));
        var pos = Vector2.Zero;
        Apply(r, ref pos);
        At(pos, 20 * Step, 0);

        for (uint seq = 21; seq <= 30; seq++)
            r.RecordPendingInput(seq, StateList.Right);

        r.ReceiveAuthoritativePosition(20, new Vector2(20 * Step, 0));
        Apply(r, ref pos);
        At(pos, 30 * Step, 0);
    }

    // Verifica la pulizia degli input tra due partite.
    private static void ResetClearsPending(Reconciler r)
    {
        r.RecordPendingInput(1, StateList.Right);
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
