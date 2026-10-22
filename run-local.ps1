# Run from any directory. Ctrl+C stops the API and its local routing bridge.
$ErrorActionPreference = 'Stop'
$previousOsrm = $env:Osrm__BaseUrl
$bridge = $null
try {
    $nodePath = (Get-Command node -ErrorAction Stop).Source
    $bridgePath = Join-Path $PSScriptRoot 'scripts/osrm-local-bridge.mjs'
    $bridge = Start-Process -FilePath $nodePath -ArgumentList ('"' + $bridgePath + '"') -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 500
    if ($bridge.HasExited) { throw 'OSRM bridge could not start. Check whether port 5168 is occupied.' }
    $env:Osrm__BaseUrl = 'http://127.0.0.1:5168/'
    & dotnet run --project (Join-Path $PSScriptRoot 'Smart_Museum_Guide_System/SMGS.API') --launch-profile lan
} finally {
    $env:Osrm__BaseUrl = $previousOsrm
    if ($bridge -and !$bridge.HasExited) { Stop-Process -Id $bridge.Id }
}
