[CmdletBinding()]
param([string]$AiSystemPath = '', [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
if (!$AiSystemPath) { $AiSystemPath = Join-Path $PSScriptRoot '../../AI-System' }
$scanEnvPath = Join-Path $AiSystemPath '.env'
if (!(Test-Path -LiteralPath $scanEnvPath)) { throw 'Chạy AI-System/tools/start-scan.ps1 trước; hoặc truyền -AiSystemPath tới repo AI-System.' }
function Read-ScanEnv([string]$name) {
    $scanLine = @(Get-Content -LiteralPath $scanEnvPath | Where-Object { $_ -match ('^\s*' + [regex]::Escape($name) + '\s*=') })
    if (!$scanLine.Count) { return '' }
    return ($scanLine[-1] -split '=', 2)[1].Trim().Trim('"').Trim("'")
}
$scanKey = Read-ScanEnv 'SCAN_SERVICE_API_KEY'
if (!$scanKey -or $scanKey.StartsWith('replace-')) { throw 'Chưa có service key; chạy start-scan.ps1 trước.' }
$scanPort = Read-ScanEnv 'AI_SCAN_PORT'
if (!$scanPort) { $scanPort = '8002' }
if ($scanPort -notmatch '^\d{4,5}$') { throw 'AI_SCAN_PORT không hợp lệ.' }
$previousUrl = $env:AiScan__BaseUrl; $previousKey = $env:AiScan__ServiceKey
$previousDb = $env:ConnectionStrings__DefaultConnection
try {
    $env:AiScan__BaseUrl = "http://127.0.0.1:$scanPort"
    $env:AiScan__ServiceKey = $scanKey
    # Use exactly the same database as the scan worker; translate its Docker host for a host-run BE.
    Add-Type -AssemblyName System.Data
    $scanDb = New-Object System.Data.Common.DbConnectionStringBuilder
    $scanDbHost = Read-ScanEnv 'AI_SYSTEM_DB_HOST'
    if ($scanDbHost -eq 'host.docker.internal') { $scanDbHost = '127.0.0.1' }
    $scanDb['Host'] = $scanDbHost
    $scanDb['Port'] = (Read-ScanEnv 'AI_SYSTEM_DB_PORT')
    $scanDb['Database'] = (Read-ScanEnv 'AI_SYSTEM_DB_NAME')
    $scanDb['Username'] = (Read-ScanEnv 'AI_SYSTEM_DB_USER')
    $scanDb['Password'] = (Read-ScanEnv 'AI_SYSTEM_DB_PASSWORD')
    $env:ConnectionStrings__DefaultConnection = $scanDb.ConnectionString
    $scanBeProject = Join-Path $PSScriptRoot '../Smart_Museum_Guide_System/SMGS.API/SMGS.API.csproj'
    $scanRunArgs = @('run', '--project', $scanBeProject, '--launch-profile', 'http')
    if ($NoBuild) { $scanRunArgs += '--no-build' }
    Write-Host 'Khởi động BE với Scan CPU; service key chỉ truyền qua environment của process.'
    & dotnet @scanRunArgs
    if ($LASTEXITCODE -ne 0) { throw 'Backend chưa khởi động được; xem lỗi phía trên.' }
} finally { $env:AiScan__BaseUrl = $previousUrl; $env:AiScan__ServiceKey = $previousKey; $env:ConnectionStrings__DefaultConnection = $previousDb }
