param(
    [string]$ExpectedCommit = '2dc7cd22a7337420918a3639f9c91139651ca9e2',
    [string]$ExpectedTag = 'v0.3.1.1',
    [string]$ExpectedPackageVersion = '0.3.1.1',
    [string]$ExpectedUiPackageHash = '4d21d7c868f48fd082e0bcbd42be136a4611dfb6b9e4588dcef5dde7037d6057'
)

$ErrorActionPreference = 'Stop'
$expectedSource = ".packages/SentinelCore/$ExpectedTag"
$expectedUiContentHash = 'PX/5Ey10VDjv7OZCqH5PaX02I47SemjIUhUgpg2k732onrZ1Rtv0YQQo6cf4fpyYa/KdWEbby8WsBVOUDTJmqw=='
$expectedCoreContentHash = 'WZwywGZ9txL4fwo7nxIm5r3coK2PES9g+fN5N+/wMOT4E1Ad4EGdCHQ8tC4fjlSpzbaS9cfipSsrUbWrH2+g8g=='

[xml]$nuget = Get-Content -LiteralPath 'NuGet.Config' -Raw
$source = @($nuget.configuration.packageSources.add) |
    Where-Object key -eq 'SentinelCore' |
    Select-Object -First 1
if ($null -eq $source -or [string]$source.value -ne $expectedSource) {
    throw "SentinelCore package source must be '$expectedSource'."
}

[xml]$project = Get-Content -LiteralPath 'ClassySentinel.csproj' -Raw
$reference = @($project.Project.ItemGroup.PackageReference) |
    Where-Object Include -eq 'MarshalTitan.SentinelCore.UI' |
    Select-Object -First 1
if ($null -eq $reference -or [string]$reference.Version -ne $ExpectedPackageVersion) {
    throw "MarshalTitan.SentinelCore.UI must be pinned to '$ExpectedPackageVersion'."
}

$lock = Get-Content -LiteralPath 'packages.lock.json' -Raw | ConvertFrom-Json
$dependencies = $lock.dependencies.'net10.0-windows7.0'
$uiLock = $dependencies.'MarshalTitan.SentinelCore.UI'
$coreLock = $dependencies.'MarshalTitan.SentinelCore'
if ($uiLock.requested -ne "[$ExpectedPackageVersion, )" `
    -or $uiLock.resolved -ne $ExpectedPackageVersion `
    -or $uiLock.dependencies.'MarshalTitan.SentinelCore' -ne $ExpectedPackageVersion `
    -or $uiLock.contentHash -ne $expectedUiContentHash) {
    throw 'The Sentinel Core UI lock entry does not match the exact 0.3.1.1 package.'
}
if ($coreLock.resolved -ne $ExpectedPackageVersion `
    -or $coreLock.contentHash -ne $expectedCoreContentHash) {
    throw 'The transitive Sentinel Core lock entry does not match the exact 0.3.1.1 package.'
}

$vendoredPackagePath = Join-Path $expectedSource "MarshalTitan.SentinelCore.UI.$ExpectedPackageVersion.nupkg"
if (-not (Test-Path -LiteralPath $vendoredPackagePath)) {
    throw "Vendored Sentinel Core UI package is missing: '$vendoredPackagePath'."
}
$vendoredHash = (Get-FileHash -LiteralPath $vendoredPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($vendoredHash -ne $ExpectedUiPackageHash) {
    throw "Vendored Sentinel Core UI package hash '$vendoredHash' does not match '$ExpectedUiPackageHash'."
}

$archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $vendoredPackagePath))
try {
    $entry = $archive.Entries |
        Where-Object FullName -eq 'MarshalTitan.SentinelCore.UI.nuspec' |
        Select-Object -First 1
    if ($null -eq $entry) {
        throw 'The vendored UI package does not contain its expected nuspec.'
    }

    $reader = [System.IO.StreamReader]::new($entry.Open())
    try {
        [xml]$nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
}
finally {
    $archive.Dispose()
}

$metadata = $nuspec.package.metadata
if ([string]$metadata.id -ne 'MarshalTitan.SentinelCore.UI' `
    -or [string]$metadata.version -ne $ExpectedPackageVersion `
    -or [string]$metadata.repository.commit -ne $ExpectedCommit `
    -or [string]$metadata.repository.branch -ne "refs/tags/$ExpectedTag") {
    throw "The vendored UI package provenance does not match $ExpectedTag at $ExpectedCommit."
}

$publishedPackagePath = Join-Path $env:RUNNER_TEMP "MarshalTitan.SentinelCore.UI.$ExpectedPackageVersion.nupkg"
$packageUrl = "https://github.com/MarshalTitan/SentinelCore/releases/download/$ExpectedTag/MarshalTitan.SentinelCore.UI.$ExpectedPackageVersion.nupkg"
Invoke-WebRequest -Uri $packageUrl -OutFile $publishedPackagePath
$publishedHash = (Get-FileHash -LiteralPath $publishedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($publishedHash -ne $ExpectedUiPackageHash) {
    throw "Published Sentinel Core UI package hash '$publishedHash' does not match '$ExpectedUiPackageHash'."
}

Write-Host "Verified MarshalTitan.SentinelCore.UI $ExpectedPackageVersion from $ExpectedTag at $ExpectedCommit ($publishedHash)."
