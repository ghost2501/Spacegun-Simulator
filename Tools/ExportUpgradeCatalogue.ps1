[CmdletBinding()]
param(
	[string]$CsvPath = "Releases/UpgradeCatalogue_latest.csv",
	[string]$WeaponsUpgradesPath = "Spacegun Simulator/Config/WeaponsUpgrades.json",
	[string]$ProjectilesCatalogPath = "Spacegun Simulator/Config/ProjectilesCatalog.json"
)

$ErrorActionPreference = "Stop"

function Read-JsonFile {
	param([Parameter(Mandatory=$true)][string]$Path)
	if (-not (Test-Path -LiteralPath $Path)) {
		throw "File not found: $Path"
	}
	(Get-Content -LiteralPath $Path -Raw -Encoding UTF8) | ConvertFrom-Json
}

function To-CompactJson {
	param([Parameter(ValueFromPipeline=$true)]$Value)
	process {
		if ($null -eq $Value) { return "" }
		$Value | ConvertTo-Json -Depth 50 -Compress
	}
}

function Get-ExtraJson {
	param(
		[Parameter(Mandatory=$true)]$Item,
		[Parameter(Mandatory=$true)][string[]]$ExcludeKeys
	)
	$extra = [ordered]@{}
	foreach ($p in $Item.PSObject.Properties) {
		if ($ExcludeKeys -contains $p.Name) { continue }
		$extra[$p.Name] = $p.Value
	}
	if ($extra.Count -eq 0) { return "" }
	$extra | To-CompactJson
}

$weaponsUpgradesFullPath = (Resolve-Path -LiteralPath $WeaponsUpgradesPath).Path
$projectilesCatalogFullPath = (Resolve-Path -LiteralPath $ProjectilesCatalogPath).Path

$weaponsDoc = Read-JsonFile -Path $weaponsUpgradesFullPath
$projDoc = Read-JsonFile -Path $projectilesCatalogFullPath

$weaponUpgrades = @($weaponsDoc.Upgrades)
$projectileCores = @($projDoc.Cores)
$projectilePropulsion = @($projDoc.PropulsionSystems)
$projectileEnhancements = @($projDoc.Enhancements)

# Collect all cost keys across all items so the CSV has stable columns.
$allCostObjects = @(
	$weaponUpgrades | ForEach-Object { $_.Cost }
	$projectileCores | ForEach-Object { $_.Cost }
	$projectilePropulsion | ForEach-Object { $_.Cost }
	$projectileEnhancements | ForEach-Object { $_.Cost }
) | Where-Object { $_ -ne $null }

$costKeys = $allCostObjects |
	ForEach-Object { $_.PSObject.Properties.Name } |
	Sort-Object -Unique

function Add-CostColumns {
	param(
		[Parameter(Mandatory=$true)]$Row,
		$Cost
	)
	foreach ($k in $costKeys) {
		$val = $null
		if ($null -ne $Cost) {
			$prop = $Cost.PSObject.Properties | Where-Object { $_.Name -ieq $k } | Select-Object -First 1
			if ($null -ne $prop) { $val = $prop.Value }
		}
		$Row["Cost.$k"] = $val
	}
}

$rows = New-Object System.Collections.Generic.List[object]

$commonExclude = @(
	"Id","Name","Description","Cost",
	"Modifiers","Parameters",
	"RequiredTechLevel",
	"MinWeaponsTechLevel","MinProjectilesTechLevel",
	"RequiresPropulsion","RequiresGuidanceMod",
	"Prerequisites",
	"Slot"
)

foreach ($u in $weaponUpgrades) {
	$row = [ordered]@{
		Kind = "WeaponUpgrade"
		Subkind = "Upgrade"
		Slot = ""
		Id = $u.Id
		Name = $u.Name
		Description = $u.Description
		RequiredTechLevel = $null
		MinWeaponsTechLevel = $u.MinWeaponsTechLevel
		MinProjectilesTechLevel = $u.MinProjectilesTechLevel
		RequiresPropulsion = $u.RequiresPropulsion
		RequiresGuidanceMod = $u.RequiresGuidanceMod
		Prerequisites = if ($u.Prerequisites) { ($u.Prerequisites -join ";") } else { "" }
		MassKg = $null
		DeltaVCapacityMs = $null
		BurnDurationSeconds = $null
		ReferenceMassKg = $null
		HitToleranceBonus = $null
		Penetration = $null
		ImpactCoupling = $null
		DefenseBonus = $null
		ModifiersJson = ($u.Modifiers | To-CompactJson)
		ParametersJson = ($u.Parameters | To-CompactJson)
		ExtraJson = (Get-ExtraJson -Item $u -ExcludeKeys $commonExclude)
		SourceFile = $WeaponsUpgradesPath
	}
	Add-CostColumns -Row $row -Cost $u.Cost
	$rows.Add([pscustomobject]$row)
}

foreach ($c in $projectileCores) {
	$row = [ordered]@{
		Kind = "ProjectileMod"
		Subkind = "Core"
		Slot = ""
		Id = $c.Id
		Name = $c.Name
		Description = $c.Description
		RequiredTechLevel = $c.RequiredTechLevel
		MinWeaponsTechLevel = $null
		MinProjectilesTechLevel = $null
		RequiresPropulsion = $null
		RequiresGuidanceMod = $null
		Prerequisites = ""
		MassKg = $c.MassKg
		DeltaVCapacityMs = $null
		BurnDurationSeconds = $null
		ReferenceMassKg = $null
		HitToleranceBonus = $null
		Penetration = $null
		ImpactCoupling = $null
		DefenseBonus = $null
		ModifiersJson = ""
		ParametersJson = ""
		ExtraJson = (Get-ExtraJson -Item $c -ExcludeKeys $commonExclude)
		SourceFile = $ProjectilesCatalogPath
	}
	Add-CostColumns -Row $row -Cost $c.Cost
	$rows.Add([pscustomobject]$row)
}

foreach ($p in $projectilePropulsion) {
	$row = [ordered]@{
		Kind = "ProjectileMod"
		Subkind = "Propulsion"
		Slot = ""
		Id = $p.Id
		Name = $p.Name
		Description = $p.Description
		RequiredTechLevel = $p.RequiredTechLevel
		MinWeaponsTechLevel = $null
		MinProjectilesTechLevel = $null
		RequiresPropulsion = $null
		RequiresGuidanceMod = $null
		Prerequisites = ""
		MassKg = $null
		DeltaVCapacityMs = $p.DeltaVCapacityMs
		BurnDurationSeconds = $p.BurnDurationSeconds
		ReferenceMassKg = $p.ReferenceMassKg
		HitToleranceBonus = $null
		Penetration = $null
		ImpactCoupling = $null
		DefenseBonus = $null
		ModifiersJson = ""
		ParametersJson = ""
		ExtraJson = (Get-ExtraJson -Item $p -ExcludeKeys $commonExclude)
		SourceFile = $ProjectilesCatalogPath
	}
	Add-CostColumns -Row $row -Cost $p.Cost
	$rows.Add([pscustomobject]$row)
}

foreach ($e in $projectileEnhancements) {
	$row = [ordered]@{
		Kind = "ProjectileMod"
		Subkind = "Enhancement"
		Slot = $e.Slot
		Id = $e.Id
		Name = $e.Name
		Description = $e.Description
		RequiredTechLevel = $e.RequiredTechLevel
		MinWeaponsTechLevel = $null
		MinProjectilesTechLevel = $null
		RequiresPropulsion = $null
		RequiresGuidanceMod = $null
		Prerequisites = ""
		MassKg = $null
		DeltaVCapacityMs = $null
		BurnDurationSeconds = $null
		ReferenceMassKg = $null
		HitToleranceBonus = $e.HitToleranceBonus
		Penetration = $e.Penetration
		ImpactCoupling = $e.ImpactCoupling
		DefenseBonus = $e.DefenseBonus
		ModifiersJson = ""
		ParametersJson = ""
		ExtraJson = (Get-ExtraJson -Item $e -ExcludeKeys $commonExclude)
		SourceFile = $ProjectilesCatalogPath
	}
	Add-CostColumns -Row $row -Cost $e.Cost
	$rows.Add([pscustomobject]$row)
}

$outFull = (Join-Path (Get-Location) $CsvPath)
$outDir = Split-Path -Parent $outFull
if (-not (Test-Path -LiteralPath $outDir)) {
	New-Item -ItemType Directory -Path $outDir | Out-Null
}

$rows | Export-Csv -LiteralPath $outFull -NoTypeInformation -Encoding UTF8
Write-Output ("Wrote {0} rows -> {1}" -f $rows.Count, $CsvPath)
