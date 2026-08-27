# Runs the published API in Production mode on http://localhost:6041, the
# port the Cloudflare Tunnel forwards loapi.lorryowner.com to.
#
# The JWT signing key is never committed to git (this repo is public) and
# never has to be typed in by hand either: the first time this script runs
# on a machine it generates a random 512-bit key and saves it to
# <root>\secrets\jwt.key, then reuses that same file on every
# later run. That file has to stay stable across restarts - every signed-in
# session's token depends on it - but it must never be committed or copied
# off this machine.
#
# EnterpriseAdmin's login is unrelated to this key: that username/password
# is a fixed constant in AuthService.cs, not something generated or stored
# here, so it's identical on every deployment without any action needed.
#
# The root defaults to C:\TransTruckWeb. To run from another drive, pass -Root
# or set TRANSTRUCKWEB_ROOT. This script passes the root through to the API,
# so the executable, the signing key, the database, the backups and the
# uploaded documents all move together rather than half of them staying
# behind on C: - which is what makes a restored backup point at documents
# that were never copied across.
#
# Usage:
#   .\deploy\run-api.ps1
#   .\deploy\run-api.ps1 -Root E:\LorryOwner
# Run .\deploy\publish-api.ps1 first (and after every update).

param(
    [string]$Root = $(if ($env:TRANSTRUCKWEB_ROOT) { $env:TRANSTRUCKWEB_ROOT } else { "C:\TransTruckWeb" })
)

$ErrorActionPreference = "Stop"

# Say plainly that the drive isn't there. Join-Path resolves the drive through
# the provider and would otherwise fail with "Cannot find drive" partway down,
# which reads like a bug in the script rather than an unplugged disk.
$rootDrive = [System.IO.Path]::GetPathRoot($Root)
if ($rootDrive -and -not (Test-Path -LiteralPath $rootDrive)) {
    Write-Host "Data root '$Root' is on drive $rootDrive, which isn't available on this machine." -ForegroundColor Red
    Write-Host "Plug it in (or pass a different -Root) and run again." -ForegroundColor Red
    exit 1
}

# [IO.Path]::Combine rather than Join-Path: pure string work, no provider, so
# it behaves the same whatever the drive letter.
$publishDir = [System.IO.Path]::Combine($Root, "publish")
$secretsDir = [System.IO.Path]::Combine($Root, "secrets")
$keyFile = [System.IO.Path]::Combine($secretsDir, "jwt.key")
$exe = [System.IO.Path]::Combine($publishDir, "TransTrack.Api.exe")

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
# Hand the same root to the API so its data folders follow the executable.
# appsettings.json still wins over this for any path set explicitly there.
$env:TRANSTRUCKWEB_ROOT = $Root
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://localhost:6041"

Write-Host "Starting TransTrack.Api on http://localhost:6041 (Production), data root $Root ..." -ForegroundColor Cyan
Push-Location $publishDir
try {
    & $exe
} finally {
    Pop-Location
}
