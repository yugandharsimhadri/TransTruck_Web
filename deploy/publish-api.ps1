# Builds a fresh Release copy of the API into <root>\publish.
#
# The root defaults to C:\TransTruckWeb. To install on another drive, pass
# -Root or set TRANSTRUCKWEB_ROOT — the same variable the API itself reads for
# its data folders, so setting it once moves the executable and the data
# together:
#
#   .\deploy\publish-api.ps1 -Root E:\LorryOwner
#   $env:TRANSTRUCKWEB_ROOT = "E:\LorryOwner"    # applies to run-api.ps1 too
#
# Safe to re-run any time you pull new code - it never touches <root>\DB (the
# live SQLite database) or <root>\secrets (the JWT signing key run-api.ps1
# generates on first run). Both live outside this publish folder specifically
# so a republish can never wipe either one.
#
# Usage:
#   .\deploy\publish-api.ps1

param(
    # Falls back to the shared environment variable, then the original default,
    # so a machine that passes nothing behaves exactly as it always has.
    [string]$Root = $(if ($env:TRANSTRUCKWEB_ROOT) { $env:TRANSTRUCKWEB_ROOT } else { "C:\TransTruckWeb" })
)

$ErrorActionPreference = "Stop"

# Say plainly that the drive isn't there, rather than letting a path call fail
# partway down with "Cannot find drive", which reads like a bug in the script.
$rootDrive = [System.IO.Path]::GetPathRoot($Root)
if ($rootDrive -and -not (Test-Path -LiteralPath $rootDrive)) {
    Write-Host "Data root '$Root' is on drive $rootDrive, which isn't available on this machine." -ForegroundColor Red
    Write-Host "Plug it in (or pass a different -Root) and run again." -ForegroundColor Red
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "src\TransTrack.Api\TransTrack.Api.csproj"
# Pure string work, no provider, so any drive letter behaves the same.
$publishDir = [System.IO.Path]::Combine($Root, "publish")

Write-Host "Publishing TransTrack.Api (Release) to $publishDir ..." -ForegroundColor Cyan
dotnet publish $apiProject -c Release -o $publishDir

Write-Host ""
Write-Host "Done. Start it with:" -ForegroundColor Green
if ($Root -eq "C:\TransTruckWeb") {
    Write-Host "  .\deploy\run-api.ps1"
} else {
    Write-Host "  .\deploy\run-api.ps1 -Root $Root"
}
