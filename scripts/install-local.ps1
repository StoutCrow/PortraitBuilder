<#
Builds the Release version, runs the tests, and copies the result over the installed copy.
Your settings (settings.txt), saved portraits (saved.txt), CK2 path (gamedir.txt) and log are left alone.
It does NOT commit or push anything.

  powershell -File scripts\install-local.ps1
  powershell -File scripts\install-local.ps1 -InstallDir D:\Apps\PortraitBuilder

With no -InstallDir it reads .local\install-dir.txt (ignored by git, one line: the folder to update).
#>
param([string]$InstallDir)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

if (-not $InstallDir) {
    $file = Join-Path $repo '.local\install-dir.txt'
    if (-not (Test-Path $file)) { throw "No -InstallDir given and $file does not exist." }
    $InstallDir = (Get-Content $file -Raw).Trim()
}
if (-not (Test-Path $InstallDir)) { throw "Install folder not found: $InstallDir" }

# A running copy locks its files.
Get-Process -Name 'PortraitBuilder.WinForms' -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Path -like "$InstallDir*") { Write-Host "Closing the running installed copy"; $_ | Stop-Process -Force }
}

Write-Host "Building (Release)..."
dotnet build src\PortraitBuilder.WinForms -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed; installed copy left as it was." }

Write-Host "Testing..."
dotnet test test\PortraitBuilder.Tests --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Tests failed; installed copy left as it was." }

Write-Host "Updating $InstallDir"
robocopy "src\PortraitBuilder.WinForms\bin\Release\net10.0-windows" $InstallDir /E /XF log.txt settings.txt gamedir.txt saved.txt /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copy failed (robocopy exit code $LASTEXITCODE)." }

Write-Host "Done. Nothing was committed. Review the app, then commit and push when you are happy."
$global:LASTEXITCODE = 0
