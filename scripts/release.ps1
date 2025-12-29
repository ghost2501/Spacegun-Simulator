Param(
  [Parameter(Mandatory = $true)]
  [string]$Version,

  [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Usage:
#   .\scripts\release.ps1 0.0.4-pre-alpha.3
# This creates an annotated git tag like v0.0.4-pre-alpha.3 and pushes it.
# Pushing the tag triggers .github/workflows/release.yml which builds and
# uploads release assets to GitHub Releases.

if ($Version.StartsWith('v')) {
  $tag = $Version
} else {
  $tag = "v$Version"
}

Write-Host "Preparing release tag: $tag"

$repoRoot = (git rev-parse --show-toplevel).Trim()
if (-not $repoRoot) { throw 'Not in a git repository.' }

$branch = (git branch --show-current).Trim()
if (-not $branch) { $branch = '(detached HEAD)' }

Write-Host "Repo: $repoRoot"
Write-Host "Branch: $branch"

# Ensure clean working tree
$status = git status --porcelain
if ($status) {
  throw "Working tree is not clean. Commit or stash changes before tagging.\n$status"
}

# Ensure tag does not already exist
$existing = (git tag --list $tag).Trim()
if ($existing) {
  throw "Tag already exists: $tag"
}

$message = "Release $tag"

if ($DryRun) {
  Write-Host "[DryRun] Would run: git tag -a $tag -m \"$message\""
  Write-Host "[DryRun] Would run: git push origin $tag"
  exit 0
}

git tag -a $tag -m $message
Write-Host "Created tag $tag"

git push origin $tag
Write-Host "Pushed tag $tag to origin"
