param(
    [string]$ExpectedCommit = '0d59460cc592f1589600e11e4e9e04478667b629',
    [string]$ExpectedTag = 'v0.4.1.3',
    [string]$ExpectedPackageVersion = '0.4.1.3',
    [string]$ExpectedUiPackageHash = 'e24369d5736eb3c10939de6c1e3f7565c06b34943a98dce4df9ec84a9662189c'
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Prepare-NavigationCore.ps1')
$expectedSource = ".packages/SentinelCore/$ExpectedTag"
$expectedUiContentHash = 'MX3hT+wWTH7iBU4RPl8SEzoyIUuH9Gn6EZ80pb4qYa2LtXMC9dfPqS6wSAbLQJbrt7811TuHzZpp+98l6DJlVg=='
$expectedCoreContentHash = 'bZVNaPeyIgB1pjoINesYvoHVttCNH6nxXN0qheOzivXZNRLpOuNGNEG5jWMjuFFkuB9ttdZrZTbGrG5097G/3A=='

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
    throw 'The Sentinel Core UI lock entry does not match the exact 0.4.1.3 package.'
}
if ($coreLock.resolved -ne $ExpectedPackageVersion `
    -or $coreLock.contentHash -ne $expectedCoreContentHash) {
    throw 'The transitive Sentinel Core lock entry does not match the exact 0.4.1.3 package.'
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
    -or [string]$metadata.repository.branch -notin @('main', 'refs/heads/main', "refs/tags/$ExpectedTag")) {
    throw "The pinned UI package provenance does not match $ExpectedTag at $ExpectedCommit (observed branch: $($metadata.repository.branch), commit: $($metadata.repository.commit))."
}

$publishedPackagePath = Join-Path $env:RUNNER_TEMP "MarshalTitan.SentinelCore.UI.$ExpectedPackageVersion.nupkg"
$packageUrl = "https://github.com/MarshalTitan/SentinelCore/releases/download/$ExpectedTag/MarshalTitan.SentinelCore.UI.$ExpectedPackageVersion.nupkg"
Invoke-WebRequest -Uri $packageUrl -OutFile $publishedPackagePath
$publishedHash = (Get-FileHash -LiteralPath $publishedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($publishedHash -ne $ExpectedUiPackageHash) {
    throw "Published Sentinel Core UI package hash '$publishedHash' does not match '$ExpectedUiPackageHash'."
}

Write-Host "Verified MarshalTitan.SentinelCore.UI $ExpectedPackageVersion from $ExpectedTag at $ExpectedCommit ($publishedHash)."
