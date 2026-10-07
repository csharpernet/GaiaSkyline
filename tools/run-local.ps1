# Starts GaiaSkyline locally for a click-through of the whole site (see docs/LOCAL-WALKTHROUGH.md).
# It seeds a local owner login, points email links at the running app, optionally starts smtp4dev, and
# runs the site. Stop it with Ctrl+C in this window.
#
#   powershell -ExecutionPolicy Bypass -File tools/run-local.ps1
#
# Switches:
#   -NoEmail     don't try to start smtp4dev (start it yourself, or use your own SMTP)
#   -OwnerEmail / -OwnerPassword   override the seeded local owner login

[CmdletBinding()]
param(
    [switch]$NoEmail,
    [string]$OwnerEmail = 'owner@gaiaskyline.local',
    [string]$OwnerPassword = 'LocalDev!2026',
    [string]$Url = 'https://localhost:7020'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $repoRoot 'src/GaiaSkyline.Web'

Write-Host '--- GaiaSkyline local run ---' -ForegroundColor Cyan

# 1. Seed the local owner + point email links at the running app (idempotent; User Secrets only).
Write-Host 'Configuring local secrets (owner login, email base URL)...'
dotnet user-secrets --project $webProject set 'Owner:Email' $OwnerEmail | Out-Null
dotnet user-secrets --project $webProject set 'Owner:Password' $OwnerPassword | Out-Null
dotnet user-secrets --project $webProject set 'Email:SiteBaseUrl' $Url | Out-Null

# 2. Email sink (smtp4dev) on SMTP localhost:2525, web UI http://localhost:5000.
if (-not $NoEmail) {
    if (Get-Command smtp4dev -ErrorAction SilentlyContinue) {
        Write-Host 'Starting smtp4dev (web UI http://localhost:5000)...'
        Start-Process smtp4dev -WindowStyle Minimized
    }
    else {
        Write-Warning 'smtp4dev not found. Install it (dotnet tool install -g Rnwood.Smtp4dev) or run your own SMTP on localhost:2525, then re-run. Continuing without it.'
    }
}

# 3. Run the site (migrates + seeds content on first start). Ctrl+C stops it.
Write-Host ''
Write-Host "Owner login : $OwnerEmail / $OwnerPassword  (first login sets up the 2FA code)" -ForegroundColor Green
Write-Host "Public site : $Url/en   (also /pt-pt /es /fr /de)" -ForegroundColor Green
Write-Host "Admin       : $Url/admin" -ForegroundColor Green
Write-Host "Background  : $Url/hangfire     Email inbox: http://localhost:5000" -ForegroundColor Green
Write-Host ''
Write-Host 'Starting the app — leave this window open; press Ctrl+C to stop.' -ForegroundColor Cyan

Push-Location $repoRoot
try {
    dotnet run --project $webProject --launch-profile https
}
finally {
    Pop-Location
}
