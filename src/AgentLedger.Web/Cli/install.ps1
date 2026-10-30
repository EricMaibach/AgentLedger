# Installs the agentledger CLI from this ledger and connects this machine (ADR 0024).
#   $env:AGENTLEDGER_CODE='XXXX-XXXX'; irm <ledger>/install.ps1 | iex
# Without a code: installs or updates the CLI and its hooks, keeping the machine's connection if it has one.
# Installs to %LOCALAPPDATA%\AgentLedger\bin and adds it to your user PATH; needs no administrator rights.
$ErrorActionPreference = 'Stop'

$LedgerUrl = '__AGENTLEDGER_URL__'
$Code = $env:AGENTLEDGER_CODE
$Platform = 'win-x64'

$BinDir = Join-Path $env:LOCALAPPDATA 'AgentLedger\bin'
New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
$Exe = Join-Path $BinDir 'agentledger.exe'
$New = Join-Path $BinDir 'agentledger.exe.new'

Write-Host "Downloading agentledger for $Platform from $LedgerUrl..."
Invoke-WebRequest -UseBasicParsing -Uri "$LedgerUrl/cli/$Platform" -OutFile $New
$Expected = ((Invoke-WebRequest -UseBasicParsing -Uri "$LedgerUrl/cli/$Platform.sha256").Content -split ' ')[0].Trim()
$Actual = (Get-FileHash -Algorithm SHA256 -Path $New).Hash.ToLowerInvariant()
if ($Expected -ne $Actual) {
  Remove-Item -Force $New
  throw 'agentledger install: the download does not match its checksum; nothing was installed.'
}
Move-Item -Force -Path $New -Destination $Exe
Write-Host "Installed $Exe"

$UserPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if (-not (($UserPath -split ';') -contains $BinDir)) {
  [Environment]::SetEnvironmentVariable('Path', (($UserPath, $BinDir) -ne '' -join ';'), 'User')
  $env:Path = "$env:Path;$BinDir"
  Write-Host "Added $BinDir to your user PATH (new terminals pick it up)."
}

$SetupArgs = @('setup', '--url', $LedgerUrl)
if ($Code) { $SetupArgs += @('--code', $Code) }
& $Exe @SetupArgs
Remove-Item Env:AGENTLEDGER_CODE -ErrorAction SilentlyContinue
