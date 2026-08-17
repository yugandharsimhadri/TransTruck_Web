# Runs the published API in Production mode on http://localhost:6041, the
# port the Cloudflare Tunnel forwards api.lorryowner.com to.
#
# The JWT signing key is never committed to git (this repo is public) and
# never has to be typed in by hand either: the first time this script runs
# on a machine it generates a random 512-bit key and saves it to
# C:\TransTruckWeb\secrets\jwt.key, then reuses that same file on every
# later run. That file has to stay stable across restarts - every signed-in
# session's token depends on it - but it must never be committed or copied
# off this machine.
#
# EnterpriseAdmin's login is unrelated to this key: that username/password
# is a fixed constant in AuthService.cs, not something generated or stored
# here, so it's identical on every deployment without any action needed.
#
# Usage:
#   .\deploy\run-api.ps1
# Run .\deploy\publish-api.ps1 first (and after every update).

$ErrorActionPreference = "Stop"

$publishDir = "C:\TransTruckWeb\publish"
$secretsDir = "C:\TransTruckWeb\secrets"
$keyFile = Join-Path $secretsDir "jwt.key"
$exe = Join-Path $publishDir "TransTrack.Api.exe"

if (-not (Test-Path $exe)) {
    Write-Host "No published build found at $publishDir." -ForegroundColor Red
    Write-Host "Run .\deploy\publish-api.ps1 first." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $secretsDir)) {
    New-Item -ItemType Directory -Path $secretsDir -Force | Out-Null
}

if (-not (Test-Path $keyFile)) {
    Write-Host "No JWT signing key yet - generating one (first run on this machine)." -ForegroundColor Yellow
    $bytes = New-Object byte[] 64
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    [Convert]::ToBase64String($bytes) | Set-Content -Path $keyFile -NoNewline -Encoding utf8
    Write-Host "Saved to $keyFile - back this file up; every signed-in session depends on it staying the same." -ForegroundColor Yellow
}

$env:Jwt__Key = Get-Content -Path $keyFile -Raw
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://localhost:6041"

Write-Host "Starting TransTrack.Api on http://localhost:6041 (Production) ..." -ForegroundColor Cyan
Push-Location $publishDir
try {
    & $exe
} finally {
    Pop-Location
}
