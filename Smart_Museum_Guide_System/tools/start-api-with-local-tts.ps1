param([string]$SpeechBaseUrl = 'http://127.0.0.1:8000/', [string]$ApiUrl = 'http://127.0.0.1:5169')
$ErrorActionPreference = 'Stop'
$speechBackendRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$speechKeyFile = Join-Path $speechBackendRoot '..\..\AI-System\state\tts-service-key.txt'
if (!(Test-Path -LiteralPath $speechKeyFile)) { throw 'Chạy AI-System/tools/start-local-tts.ps1 trước.' }
$speechPrevious = @{}
foreach ($name in @('ArtifactSpeech__Enabled', 'ArtifactSpeech__BaseUrl', 'ArtifactSpeech__ServiceKey')) {
    $speechPrevious[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:ArtifactSpeech__Enabled = 'true'
    $env:ArtifactSpeech__BaseUrl = $SpeechBaseUrl
    $env:ArtifactSpeech__ServiceKey = [System.IO.File]::ReadAllText($speechKeyFile).Trim()
    Push-Location -LiteralPath $speechBackendRoot
    try { dotnet run --project SMGS.API --urls $ApiUrl }
    finally { Pop-Location }
} finally {
    foreach ($name in $speechPrevious.Keys) { [Environment]::SetEnvironmentVariable($name, $speechPrevious[$name], 'Process') }
}
