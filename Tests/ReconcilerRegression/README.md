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
