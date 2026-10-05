# Drops every throwaway GaiaSkyline test database left on the shared LocalDB instance.
#
# Local E2E runs ("fresh DB per run") and aborted test runs can leave GaiaSkyline_E2E_*/
# GaiaSkyline_Web_*/GaiaSkyline_Tests_*/... databases attached with AUTO_CLOSE ON; dozens of those
# degrade the whole instance (every touch pays a database open/recovery). The dev database
# ("GaiaSkyline") is always kept. Run this whenever run-ci-gates.ps1 reports leftovers.
[CmdletBinding()]
param(
    # Also kill stray GaiaSkyline.Web.exe processes that keep connections open.
    [switch]$KillStrayProcesses
)

$ErrorActionPreference = 'Stop'
$server = '(localdb)\mssqllocaldb'

if ($KillStrayProcesses) {
    Get-Process -Name 'GaiaSkyline.Web' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Stopping stray GaiaSkyline.Web.exe (pid $($_.Id))"
        try { Stop-Process -Id $_.Id -Force -Confirm:$false -ErrorAction Stop } catch { Write-Warning "Could not stop pid $($_.Id): $_" }
    }
}

& sqllocaldb start MSSQLLocalDB | Out-Null

$names = & sqlcmd -S $server -h -1 -W -Q "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name LIKE 'GaiaSkyline[_]%' AND name <> 'GaiaSkyline'"
$names = @($names | Where-Object { $_ -and $_.Trim() -ne '' } | ForEach-Object { $_.Trim() })

if ($names.Count -eq 0) {
    Write-Host 'No leftover GaiaSkyline test databases.' -ForegroundColor Green
    exit 0
}

foreach ($name in $names) {
    Write-Host "Dropping $name"
    & sqlcmd -S $server -b -Q "ALTER DATABASE [$name] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$name];"
    if ($LASTEXITCODE -ne 0) { Write-Warning "Failed to drop $name" }
}

Write-Host "Dropped $($names.Count) leftover test database(s)." -ForegroundColor Green
