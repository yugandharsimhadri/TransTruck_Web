# Builds a fresh Release copy of the API into C:\TransTruckWeb\publish.
#
# Safe to re-run any time you pull new code - it never touches
# C:\TransTruckWeb\DB (the live SQLite database) or C:\TransTruckWeb\secrets
# (the JWT signing key run-api.ps1 generates on first run). Both live
# outside this publish folder specifically so a republish can never wipe
# either one.
#
# Usage:
#   .\deploy\publish-api.ps1

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "src\TransTrack.Api\TransTrack.Api.csproj"
$publishDir = "C:\TransTruckWeb\publish"

Write-Host "Publishing TransTrack.Api (Release) to $publishDir ..." -ForegroundColor Cyan
dotnet publish $apiProject -c Release -o $publishDir

Write-Host ""
Write-Host "Done. Start it with:" -ForegroundColor Green
Write-Host "  .\deploy\run-api.ps1"
