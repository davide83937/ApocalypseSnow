# Compara i campioni spaziali prodotti dalla fisica C# e Go a tick identici.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Push-Location Server
    try {
        $goLines = & go test -run "^TestShotTrajectoryParityFixtures$" -count=1 -v 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Go failed: $($goLines -join ' ')" }
    } finally { Pop-Location }

    $csLines = & dotnet run --project Tests\ReconcilerRegression\ReconcilerRegression.csproj --no-restore -- --trajectory-fixtures 2>&1
    if ($LASTEXITCODE -ne 0) { throw "C# failed: $($csLines -join ' ')" }

    $ci = [System.Globalization.CultureInfo]::InvariantCulture
    $pattern = 'PATH_(GO|CS)\|(?<key>[^|]+)\|(?<x>-?[\d.]+)\|(?<y>-?[\d.]+)\|(?<sx>-?[\d.]+)\|(?<sy>-?[\d.]+)\|(?<z>-?[\d.]+)'
    $go = @{}
    foreach ($line in $goLines) {
        if ("$line" -match $pattern) {
            $go[$Matches.key] = @(
                [double]::Parse($Matches.x,$ci),[double]::Parse($Matches.y,$ci),
                [double]::Parse($Matches.sx,$ci),[double]::Parse($Matches.sy,$ci),
                [double]::Parse($Matches.z,$ci))
        }
    }
    $checked = 0; $maxDifference = 0.0; $worst = ""
    foreach ($line in $csLines) {
        if ("$line" -match $pattern) {
            $key = $Matches.key
            if (-not $go.ContainsKey($key)) { throw "Missing Go trajectory: $key" }
            $cs = @([double]::Parse($Matches.x,$ci),[double]::Parse($Matches.y,$ci),
                [double]::Parse($Matches.sx,$ci),[double]::Parse($Matches.sy,$ci),
                [double]::Parse($Matches.z,$ci))
            for ($i=0; $i -lt 5; $i++) {
                $delta = [math]::Abs($cs[$i] - $go[$key][$i])
                if ($delta -gt $maxDifference) { $maxDifference = $delta; $worst="$key metric[$i]" }
                if ($delta -gt 0.15) {
                    throw "TRAJECTORY DIFFERENCE $key metric $i CS=$($cs[$i]) Go=$($go[$key][$i]) delta=$delta px"
                }
            }
            $checked++
        }
    }
    if ($checked -lt 50 -or $checked -ne $go.Count) {
        throw "Fixture count mismatch: C# $checked Go $($go.Count)"
    }
    Write-Output ("PASS: {0} spatial trajectory samples; max difference {1:F6}px ({2})" -f $checked,$maxDifference,$worst)
} finally {
    Pop-Location
}
