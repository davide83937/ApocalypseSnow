# Reconciler regression baseline

Run from the repository root on Windows x64:

~~~powershell
dotnet run --project Tests/ReconcilerRegression/ReconcilerRegression.csproj -c Debug
~~~

This executable tests the production Reconciler, PhysicsWrapper, and native libPhysicsDll.dll
without launching MonoGame or opening a socket. It has no test-framework dependencies.

Baseline behaviors: pending inputs and ACK filtering; native replay; normalized diagonals;
2 px dead zone; 12 px snap threshold and 25% soft correction; obstacle rejection; current
movement locks (including Freezing); backlog replay; reset; and latest snapshot selection.

These tests record existing behavior, including the fact that Freezing in the recorded
client input suppresses movement during replay. They do not validate authoritative freeze
or shot-result reconciliation, which are not yet implemented.

Server-side mature-event/burst tests live in Server/game_room_sync_test.go and run with:

~~~powershell
cd Server
go test -count=1 ./...
~~~

## Authoritative shot outcomes

The same test executable also covers ShotKey, MsgRemoteShot, MsgShotResolved
(18 bytes total), and TCP stream reassembly across partial/coalesced packets.
The server test file Server/shot_resolution_test.go covers the three terminal
outcomes (expired, obstacle, penguin), both recipients, duplicate protection and
self-collision exclusion. Both players receive the outcome keyed by
(OwnerPlayerId, ClientShotTick); ServerTick is server metadata, NOT a local
client tick.

Shot resolution currently records the result in Events.ShotResolutions but
does not change freezing, movement, or ball rendering. Server-rejected shots
that never become an authoritative Shot are outside these three terminal
outcomes and will need explicit treatment before predictive freezing.
