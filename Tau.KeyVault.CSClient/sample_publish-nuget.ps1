param(
    [string]$Source = "https://nuget.yoursite.com/v3/index.json",
    [string]$ApiKey = "your-key",
    [string]$Configuration = "Release"
)

# move further down to uncomment the second server if a second server is being used
#$Source2 = "https://nuget2.yoursite2.com/v3/index.json"

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$versionFile = Join-Path $projectRoot "nuget_version.txt"
$releaseDir = Join-Path $projectRoot "bin\$Configuration"

if (-not (Test-Path $versionFile)) {
    throw "version.txt not found in project root."
}

Write-Host "=== Reading version file ==="
$version = (Get-Content $versionFile).Trim()

if ($version -notmatch "^\d+\.\d+\.\d+$") {
    throw "Version format invalid. Expected format: Major.Minor.Patch (e.g. 1.0.0)"
}

$parts = $version.Split(".")
$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]

# Increment patch
$patch++
$newVersion = "$major.$minor.$patch"

Write-Host "Old version: $version"
Write-Host "New version: $newVersion"

# Save new version back to file
Set-Content $versionFile $newVersion

Write-Host "=== Cleaning old packages ==="
if (Test-Path $releaseDir) {
    Get-ChildItem $releaseDir -Filter "*.nupkg" -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

Write-Host "=== Packing version $newVersion ==="
dotnet pack -c $Configuration -p:PackageVersion=$newVersion

Write-Host "=== Locating package ==="
$nupkg = Get-ChildItem $releaseDir -Filter "*.nupkg" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $nupkg) {
    throw "No package found after packing."
}

Write-Host "Publishing $($nupkg.Name)"
dotnet nuget push $nupkg.FullName --source $Source --api-key $ApiKey

### NB!!!
### Use this if publishing to a secondary nuget server
#dotnet nuget push $nupkg.FullName --source $Source2 --api-key $ApiKey

Write-Host "=== Publish complete ==="
