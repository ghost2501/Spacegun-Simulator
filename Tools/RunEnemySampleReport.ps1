[CmdletBinding()]
param(
	[int]$Seed = 12345,
	[int]$Waves = 25,
	[int]$SamplesPerWave = 5,
	[string]$CsvPath = "Releases/EnemySamples_latest.csv",
	[switch]$Build
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "Generating enemy sample CSV..." -ForegroundColor Cyan
Write-Host "  Seed=$Seed Waves=$Waves SamplesPerWave=$SamplesPerWave" -ForegroundColor DarkGray
Write-Host "  CsvPath=$CsvPath" -ForegroundColor DarkGray

if ($Build -or -not (Test-Path "Releases/_build/SpacegunSimulator.dll")) {
	Write-Host "Building Release into Releases/_build..." -ForegroundColor Cyan
	& dotnet build "Spacegun Simulator/SpacegunSimulator.csproj" -c Release /p:UseAppHost=false -o "Releases/_build" -v minimal
}

& dotnet exec "Releases/_build/SpacegunSimulator.dll" -- `
	--enemy-sample-csv $CsvPath `
	--enemy-sample-seed $Seed `
	--enemy-samples-per-wave $SamplesPerWave `
	--enemy-sample-waves $Waves

Write-Host "Done." -ForegroundColor Green
