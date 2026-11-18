# Run from any directory. Ctrl+C stops the API and any bridge started here.
$ErrorActionPreference = 'Stop'
$previousOsrm = $env:Osrm__BaseUrl
$bridge = $null
$bridgeUrl = 'http://127.0.0.1:5168/'

function Test-OsrmBridge {
    $ProgressPreference = 'SilentlyContinue'
    try {
        $content = (Invoke-WebRequest -Uri ($bridgeUrl + 'health') -UseBasicParsing -TimeoutSec 2).Content
        if ($content -is [byte[]]) { $content = [Text.Encoding]::UTF8.GetString($content) }
        $content -ceq 'SMGS OSRM local bridge'
    } catch {
        $false
    }
}

try {
    if (!(Test-OsrmBridge)) {
        $nodePath = (Get-Command node -ErrorAction Stop).Source
        $bridgePath = Join-Path $PSScriptRoot 'scripts/osrm-local-bridge.mjs'
        $bridge = Start-Process -FilePath $nodePath -ArgumentList ('"' + $bridgePath + '"') -WindowStyle Hidden -PassThru
        Start-Sleep -Milliseconds 500
        if (!(Test-OsrmBridge)) { throw 'OSRM bridge health check failed on port 5168. Check whether another service is using the port.' }
    }
    $env:Osrm__BaseUrl = $bridgeUrl
    & dotnet run --project (Join-Path $PSScriptRoot 'Smart_Museum_Guide_System/SMGS.API') --launch-profile lan
} finally {
    $env:Osrm__BaseUrl = $previousOsrm
    if ($bridge -and !$bridge.HasExited) { Stop-Process -Id $bridge.Id }
}
