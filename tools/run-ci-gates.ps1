# Runs the FULL CI gate set locally, mirroring .github/workflows/ci.yml step by step — same
# configuration, same runsettings (sequential test assemblies), same browser-quality environment and
# the same Playwright isolation (CI=1 → Playwright's one-worker CI mode, so cross-spec contamination
# shows up here exactly as it would on GitHub). Run it from anywhere; it works out the repo root.
#
#   powershell -ExecutionPolicy Bypass -File tools/run-ci-gates.ps1                # everything
#   powershell -ExecutionPolicy Bypass -File tools/run-ci-gates.ps1 -SkipLighthouse
#   powershell -ExecutionPolicy Bypass -File tools/run-ci-gates.ps1 -SkipBrowser   # build-and-test job only
#
# The browser-quality phase uses the fixed database GaiaSkyline_LocalGates (dropped before AND after,
# so the E2E owner/password seeding is always fresh) and finishes with a leftover-database check that
# fails if any throwaway GaiaSkyline_* database remains attached — the guard against the LocalDB
# degradation documented in docs/runbook.md.
[CmdletBinding()]
param(
    [switch]$SkipBrowser,
    [switch]$SkipLighthouse,
    [int]$Port = 5080
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot
$server = '(localdb)\mssqllocaldb'
$gatesDb = 'GaiaSkyline_LocalGates'
$baseUrl = "http://localhost:$Port"
$failed = @()

function Invoke-Step {
    param([string]$Name, [scriptblock]$Body)
    Write-Host ''
    Write-Host "===== $Name" -ForegroundColor Cyan
    & $Body
    if ($LASTEXITCODE -ne 0) { throw "Gate failed: $Name" }
    Write-Host "OK: $Name" -ForegroundColor Green
}

function Remove-GatesDb {
    & sqlcmd -S $server -b -Q "IF DB_ID('$gatesDb') IS NOT NULL BEGIN ALTER DATABASE [$gatesDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$gatesDb]; END" | Out-Null
}

# ---- build-and-test job ----
Invoke-Step 'Restore' { dotnet restore GaiaSkyline.sln }
Invoke-Step 'Build (Release, warnings are errors)' { dotnet build GaiaSkyline.sln --configuration Release --no-restore }
Invoke-Step 'Format check' { dotnet format GaiaSkyline.sln --verify-no-changes --no-restore }
Invoke-Step 'Start LocalDB' { sqllocaldb start MSSQLLocalDB }
Invoke-Step 'Tests (unit + LocalDB integration, sequential assemblies)' {
    dotnet test GaiaSkyline.sln --configuration Release --no-build --settings tests.runsettings
}
Invoke-Step 'Vulnerable package scan (fail on High/Critical)' {
    $report = dotnet list GaiaSkyline.sln package --vulnerable --include-transitive 2>&1 | Out-String
    Write-Host $report
    if ($report -match '\b(High|Critical)\b') { $global:LASTEXITCODE = 1 } else { $global:LASTEXITCODE = 0 }
}

# ---- browser-quality job ----
if (-not $SkipBrowser) {
    $script:appProcess = $null
    try {
        Invoke-Step 'Fresh browser-quality database' { Remove-GatesDb; $global:LASTEXITCODE = 0 }

        # The exact environment ci.yml gives the app and Playwright, with a throwaway owner password.
        $password = "GaiaE2E-$(Get-Random)$(Get-Random)-Xq9z"
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:Features__SeedContentOnStartup = 'true'
        $env:ConnectionStrings__Default = "Server=$server;Database=$gatesDb;Trusted_Connection=True;TrustServerCertificate=True"
        $env:BASE_URL = $baseUrl
        $env:E2E__Enabled = 'true'
        $env:E2E_SEAM = '1'
        $env:Owner__Email = 'owner.e2e@gaiaskyline.test'
        $env:OWNER_EMAIL = 'owner.e2e@gaiaskyline.test'
        $env:E2E__OwnerTotpKey = 'JBSWY3DPEHPK3PXP'
        $env:OWNER_TOTP_KEY = 'JBSWY3DPEHPK3PXP'
        $env:Owner__Password = $password
        $env:OWNER_PASSWORD = $password

        Invoke-Step 'Start the app (Release, migrated + seeded)' {
            $script:appProcess = Start-Process -FilePath 'dotnet' -PassThru -WindowStyle Hidden `
                -ArgumentList "run --project src/GaiaSkyline.Web/GaiaSkyline.Web.csproj --configuration Release --no-build --urls $baseUrl" `
                -RedirectStandardOutput (Join-Path $repoRoot 'app-gates.log') -RedirectStandardError (Join-Path $repoRoot 'app-gates.err.log')
            $up = $false
            foreach ($i in 1..90) {
                try {
                    Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl/health/live" -TimeoutSec 2 | Out-Null
                    $up = $true; break
                } catch { Start-Sleep -Seconds 2 }
            }
            if ($up) { $global:LASTEXITCODE = 0 } else { Get-Content app-gates.log -Tail 40; $global:LASTEXITCODE = 1 }
        }

        if (-not $SkipLighthouse) {
            Invoke-Step 'Lighthouse CI (Core Web Vitals budgets)' {
                npx --yes "@lhci/cli@0.14.x" autorun --config=./lighthouserc.json
            }
        }

        Invoke-Step 'Playwright + axe (CI mode: one worker, same ordering)' {
            Push-Location (Join-Path $repoRoot 'tests/e2e')
            try {
                $env:CI = '1'
                npm install
                if ($LASTEXITCODE -eq 0) { npx playwright install chromium }
                if ($LASTEXITCODE -eq 0) { npx playwright test }
            } finally {
                Remove-Item Env:CI -ErrorAction SilentlyContinue
                Pop-Location
            }
        }
    } finally {
        if ($script:appProcess -and -not $script:appProcess.HasExited) { Stop-Process -Id $script:appProcess.Id -Force -Confirm:$false }
        Remove-GatesDb
    }
}

# ---- leftover-database guard ----
Invoke-Step 'Leftover test-database check' {
    $names = & sqlcmd -S $server -h -1 -W -Q "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name LIKE 'GaiaSkyline[_]%' AND name <> 'GaiaSkyline'"
    $names = @($names | Where-Object { $_ -and $_.Trim() -ne '' })
    if ($names.Count -gt 0) {
        Write-Host "Leftover test databases degrade LocalDB over time:" -ForegroundColor Red
        $names | ForEach-Object { Write-Host "  $_" }
        Write-Host 'Run tools/cleanup-test-dbs.ps1 to drop them.'
        $global:LASTEXITCODE = 1
    } else {
        $global:LASTEXITCODE = 0
    }
}

Write-Host ''
Write-Host 'ALL CI GATES GREEN' -ForegroundColor Green
