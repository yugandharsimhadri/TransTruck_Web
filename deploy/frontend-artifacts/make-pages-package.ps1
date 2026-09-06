# Builds the Cloudflare PAGES upload package for the frontend.
#
# Deliberately ASCII-only: Windows PowerShell 5.1 reads .ps1 files as ANSI when
# there is no BOM, so a stray em-dash in a string terminates it early and the
# whole script fails to parse.
#
# Everything here was done by hand once and went wrong, so it is a script now:
#
#  1. Pins NEXT_PUBLIC_API_URL through the process environment. A dev machine
#     usually has an untracked .env.local holding localhost, and Next loads
#     .env.local at HIGHER precedence than the committed .env.production, so
#     building without this silently ships a bundle that calls localhost from
#     the visitor's browser, with no build error to warn you.
#
#  2. Patches the bundled worker to serve static assets from the ASSETS
#     binding. @opennextjs/cloudflare targets Workers Static Assets, where
#     Cloudflare serves matching files BEFORE invoking the worker, so its
#     worker has no code path for them at all. Cloudflare PAGES in advanced
#     mode routes every request to _worker.js instead, so without this shim
#     every /_next/static/* request reaches the Next handler and 404s. The
#     symptom is a blank white site whose HTML shell renders perfectly. That
#     is what happened on 2026-09-06.
#
#  3. Zips with forward-slash entry names. Compress-Archive writes Windows
#     backslashes, which are not valid ZIP path separators.
#
#  4. Refuses to produce a package that fails its own checks.
#
# Stop the Next dev server first: it spawns a workerd.exe that locks
# .open-next/assets on Windows and the build fails with EPERM.
#
# Usage, from the repo root:
#     .\deploy\frontend-artifacts\make-pages-package.ps1

param(
    [string]$ApiUrl = "https://loapi.lorryowner.com"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$webDir   = Join-Path $repoRoot "web\transtrack-web"
$zipPath  = Join-Path $PSScriptRoot "transtruck-web-pages-deploy.zip"
$stageDir = Join-Path ([System.IO.Path]::GetTempPath()) "lorryowner-pages-stage"

Write-Host "Building frontend for Cloudflare Pages" -ForegroundColor Cyan
Write-Host "  API URL: $ApiUrl"

Push-Location $webDir
try {
    $env:NEXT_PUBLIC_API_URL = $ApiUrl

    foreach ($d in @(".open-next", ".deploy-dryrun")) {
        if (Test-Path $d) { Remove-Item -Recurse -Force $d }
    }

    # These write warnings to stderr, and under ErrorActionPreference=Stop a
    # native command's stderr becomes a terminating error even on exit code 0.
    # Judge them by their exit code instead, which is the only honest signal.
    $ErrorActionPreference = "Continue"

    npx opennextjs-cloudflare build
    if ($LASTEXITCODE -ne 0) { $ErrorActionPreference = "Stop"; throw "opennextjs-cloudflare build failed" }

    npx wrangler deploy --dry-run --outdir=.deploy-dryrun
    if ($LASTEXITCODE -ne 0) { $ErrorActionPreference = "Stop"; throw "wrangler dry-run failed" }

    $ErrorActionPreference = "Stop"

    # Check 1: the client bundle must call the real API.
    $chunks = ".open-next\assets\_next\static\chunks\*.js"
    $host_  = $ApiUrl.Replace("https://", "").Replace("http://", "")
    $localhostHits = @(Select-String -Path $chunks -Pattern "localhost:5034" -SimpleMatch)
    $apiHits       = @(Select-String -Path $chunks -Pattern $host_ -SimpleMatch)

    if ($localhostHits.Count -gt 0) {
        throw "Client bundle still references localhost:5034. A stray .env.local won."
    }
    if ($apiHits.Count -lt 1) {
        throw "Client bundle never references $ApiUrl. The API URL did not reach the build."
    }
    Write-Host "  API URL check: ok (localhost 0, api $($apiHits.Count))" -ForegroundColor Green

    # Stage the assets.
    if (Test-Path $stageDir) { Remove-Item -Recurse -Force $stageDir }
    New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
    Copy-Item ".open-next\assets\*" -Destination $stageDir -Recurse -Force

    # Patch the worker so Pages can serve static assets.
    $worker = Get-Content ".deploy-dryrun\worker.js" -Raw
    $marker = "worker_default as default"

    if ($worker -notmatch [regex]::Escape($marker)) {
        throw "Could not find the worker default export to wrap. The adapter output shape changed; re-check this script against .deploy-dryrun\worker.js."
    }

    $shim = @'

// Cloudflare Pages asset shim, added by make-pages-package.ps1.
// On Workers, Cloudflare serves static assets before the worker runs, so the
// adapter never wrote a code path for them. Pages advanced mode routes every
// request here instead, so ask the ASSETS binding first and fall through to
// Next on a miss. Safe because the asset set holds no .html that could shadow
// an SSR route.
const __pagesWorker = {
  ...worker_default,
  async fetch(request, env, ctx) {
    if (env && env.ASSETS && request.method === "GET") {
      try {
        const hit = await env.ASSETS.fetch(request);
        if (hit && hit.status !== 404) return hit;
      } catch {
        // An asset lookup that throws must not take the whole page down.
      }
    }
    return worker_default.fetch(request, env, ctx);
  },
};
'@

    # Insert the shim immediately before the final export block, then point the
    # default export at it.
    $idx = $worker.LastIndexOf("export {")
    if ($idx -lt 0) { throw "No export block found in the worker bundle." }
    $worker = $worker.Substring(0, $idx) + $shim + "`r`n" + $worker.Substring($idx)
    $worker = $worker.Replace($marker, "__pagesWorker as default")

    [System.IO.File]::WriteAllText((Join-Path $stageDir "_worker.js"), $worker, (New-Object System.Text.UTF8Encoding($false)))

    # Check 2: the shim actually landed.
    $patched = [System.IO.File]::ReadAllText((Join-Path $stageDir "_worker.js"))
    if (-not $patched.Contains("__pagesWorker as default") -or -not $patched.Contains("env.ASSETS.fetch(request)")) {
        throw "The Pages asset shim did not apply cleanly."
    }
    Write-Host "  ASSETS shim: applied" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Zip with forward-slash entry names.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

$fs = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::CreateNew)
try {
    $archive = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $root = (Resolve-Path $stageDir).Path.TrimEnd('\') + '\'
        foreach ($file in Get-ChildItem $stageDir -Recurse -File) {
            $name = $file.FullName.Substring($root.Length).Replace('\', '/')
            $entry = $archive.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
            $in  = [System.IO.File]::OpenRead($file.FullName)
            $out = $entry.Open()
            try { $in.CopyTo($out) } finally { $out.Dispose(); $in.Dispose() }
        }
    }
    finally { $archive.Dispose() }
}
finally { $fs.Dispose() }

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host ""
Write-Host "Package: $zipPath ($sizeMb MB)" -ForegroundColor Green
Write-Host "Upload its CONTENTS as a new deployment on the existing Pages project." -ForegroundColor Green
