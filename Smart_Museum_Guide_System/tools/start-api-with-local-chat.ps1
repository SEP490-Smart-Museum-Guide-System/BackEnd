param([string]$ChatBaseUrl = 'http://127.0.0.1:8003/', [string]$ApiUrl = 'http://127.0.0.1:5169')
$ErrorActionPreference = 'Stop'
$guideBackendRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$guideKeyFile = Join-Path $guideBackendRoot '..\..\AI-System\state\chat-service-key.txt'
if (!(Test-Path -LiteralPath $guideKeyFile)) { throw 'Chạy AI-System/tools/start-local-chat.ps1 trước để tạo khóa nội bộ.' }
$guideBackendKey = [System.IO.File]::ReadAllText($guideKeyFile).Trim()
if ($guideBackendKey.Length -lt 32) { throw 'Khóa chatbot local không hợp lệ.' }
$guidePrevious = @{}
foreach ($name in @('ArtifactGuide__Enabled', 'ArtifactGuide__BaseUrl', 'ArtifactGuide__ServiceKey')) {
    $guidePrevious[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:ArtifactGuide__Enabled = 'true'
    $env:ArtifactGuide__BaseUrl = $ChatBaseUrl
    $env:ArtifactGuide__ServiceKey = $guideBackendKey
    Push-Location -LiteralPath $guideBackendRoot
    try { dotnet run --project SMGS.API --urls $ApiUrl }
    finally { Pop-Location }
} finally {
    foreach ($name in $guidePrevious.Keys) { [Environment]::SetEnvironmentVariable($name, $guidePrevious[$name], 'Process') }
}
