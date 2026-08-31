# SPDX-License-Identifier: MIT

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'src\OkularTabLauncher.csproj'
$intermediatePath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src\obj'))
$artifactsPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$verificationPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsPath '.reproducibility'))
$firstBuildPath = Join-Path $verificationPath 'first'
$secondBuildPath = Join-Path $verificationPath 'second'

foreach ($path in @($artifactsPath, $verificationPath, $intermediatePath)) {
    if (-not $path.StartsWith($repositoryRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Generated output is outside the repository: $path"
    }
}

if (Test-Path -LiteralPath $artifactsPath) {
    Remove-Item -LiteralPath $artifactsPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $firstBuildPath, $secondBuildPath | Out-Null

$commonBuildArguments = @(
    'build'
    $projectPath
    '--configuration', 'Release'
    '--no-restore'
    '--no-incremental'
    '/p:ContinuousIntegrationBuild=true'
    '/p:Deterministic=true'
    "/p:PathMap=$repositoryRoot=/_/src"
)

function Invoke-CleanBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$BuildName
    )

    if (Test-Path -LiteralPath $intermediatePath) {
        Remove-Item -LiteralPath $intermediatePath -Recurse -Force
    }

    dotnet restore $projectPath --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "$BuildName restore failed with exit code $LASTEXITCODE."
    }

    dotnet @commonBuildArguments --output $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "$BuildName build failed with exit code $LASTEXITCODE."
    }
}

Invoke-CleanBuild -OutputPath $firstBuildPath -BuildName 'First clean'
Invoke-CleanBuild -OutputPath $secondBuildPath -BuildName 'Second clean'

$firstExecutable = Join-Path $firstBuildPath 'OkularTabLauncher.exe'
$secondExecutable = Join-Path $secondBuildPath 'OkularTabLauncher.exe'
$firstHash = (Get-FileHash -LiteralPath $firstExecutable -Algorithm SHA256).Hash
$secondHash = (Get-FileHash -LiteralPath $secondExecutable -Algorithm SHA256).Hash

if ($firstHash -ne $secondHash) {
    throw "Reproducibility check failed: $firstHash differs from $secondHash."
}

$artifactExecutable = Join-Path $artifactsPath 'OkularTabLauncher.exe'
$hashPath = Join-Path $artifactsPath 'OkularTabLauncher.exe.sha256'
Copy-Item -LiteralPath $firstExecutable -Destination $artifactExecutable
[System.IO.File]::WriteAllText(
    $hashPath,
    ($firstHash.ToLowerInvariant() + ' *OkularTabLauncher.exe' + "`n"),
    [System.Text.Encoding]::ASCII)

Remove-Item -LiteralPath $verificationPath -Recurse -Force

Write-Host "Build succeeded and is reproducible."
Write-Host "Executable: $artifactExecutable"
Write-Host "SHA-256:    $firstHash"
