# Confronto C# (formula di tiro del client A) e Go (fisica server reale).
# Casi condivisi: charge 50..1500ms, tiro sinistro/destro, 30 Hz.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Push-Location Server
    try {
        $goOutput = & go test -run "^TestShotFlightComparisonFixtures$" -count=1 -v 2>&1
        if ($LASTEXITCODE -ne 0) { throw ("Go test failed: " + ($goOutput -join "`n")) }
    } finally { Pop-Location }

    $csOutput = & dotnet run --project Tests\ReconcilerRegression\ReconcilerRegression.csproj --no-restore -- --flight-fixtures 2>&1
    if ($LASTEXITCODE -ne 0) { throw ("C# fixtures failed: " + ($csOutput -join "`n")) }

    $ci = [System.Globalization.CultureInfo]::InvariantCulture
    $server = @{}
    foreach ($line in $goOutput) {
        if ("$line" -match 'FLIGHT_GO\|(?<key>[^|]+)\|(?<sec>[0-9.]+)\|(?<ticks>[0-9]+)') {
            $server[$Matches.key] = @([double]::Parse($Matches.sec,$ci), [int]::Parse($Matches.ticks))
        }
    }
    $checked = 0
    foreach ($line in $csOutput) {
        if ("$line" -match 'FLIGHT_CS\|(?<key>[^|]+)\|(?<sec>[0-9.]+)\|(?<ticks>[0-9]+)') {
            $key = $Matches.key
            if (-not $server.ContainsKey($key)) { throw "Go fixture missing: $key" }
            $localSeconds = [double]::Parse($Matches.sec,$ci)
            $localTicks = [int]::Parse($Matches.ticks)
            $remoteSeconds = $server[$key][0]
            $remoteTicks = $server[$key][1]
            $delta = [math]::Abs($localSeconds - $remoteSeconds)
            if ($delta -gt 0.0001 -or $localTicks -ne $remoteTicks) {
                throw "MISMATCH $key C#=($localSeconds,$localTicks) Go=($remoteSeconds,$remoteTicks) delta=$delta"
            }
            "{0,-12} A={1,10:F7}s Go={2,10:F7}s | delta={3,10:F7}s | ticks={4}" -f $key,$localSeconds,$remoteSeconds,$delta,$localTicks
            $checked++
        }
    }
    if ($checked -ne 16 -or $server.Count -ne 16) { throw "Fixture count mismatch: C# $checked, Go $($server.Count)" }
    "PASS: $checked/16 comparative flight tests C# vs Go (seconds and ticks)"
} finally { Pop-Location }
