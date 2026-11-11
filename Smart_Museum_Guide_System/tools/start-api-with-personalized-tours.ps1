param([string]$TourBaseUrl = 'http://127.0.0.1:8004/', [string]$ApiUrl = 'http://127.0.0.1:5169', [string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$backendRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$keyFile = Join-Path $backendRoot '..\..\AI-System\state\tour-service-key.txt'
if (!(Test-Path -LiteralPath $keyFile)) { throw 'Chạy AI-System/tools/start-local-tour.ps1 hoặc start-ai.ps1 trước.' }
$key = [IO.File]::ReadAllText($keyFile).Trim()
if ($key.Length -lt 32) { throw 'Khóa nội bộ AI Tour không hợp lệ.' }
$oldUrl = $env:PersonalizedTours__BaseUrl
$oldKey = $env:PersonalizedTours__ServiceKey
Push-Location -LiteralPath $backendRoot
try {
    $env:PersonalizedTours__BaseUrl = $TourBaseUrl
    $env:PersonalizedTours__ServiceKey = $key
    & $DotNet run --project SMGS.API --urls $ApiUrl
    if ($LASTEXITCODE -ne 0) { throw 'Backend dừng do lỗi; kiểm tra cấu hình DB local.' }
} finally {
    $env:PersonalizedTours__BaseUrl = $oldUrl
    $env:PersonalizedTours__ServiceKey = $oldKey
    Pop-Location
}
