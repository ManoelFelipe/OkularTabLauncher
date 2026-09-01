# SPDX-License-Identifier: MIT

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$tabProject = Join-Path $repositoryRoot 'src\OkularTabLauncher.csproj'
$sessionProject = Join-Path $repositoryRoot 'src\OkularSessionLauncher\OkularSessionLauncher.csproj'
$tabIntermediate = Join-Path $repositoryRoot 'src\obj'
$sessionIntermediate = Join-Path $repositoryRoot 'src\OkularSessionLauncher\obj'
$artifactsPath = Join-Path $repositoryRoot 'artifacts'
$verificationPath = Join-Path $artifactsPath '.reproducibility'

foreach ($path in @($tabIntermediate, $sessionIntermediate, $artifactsPath, $verificationPath)) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith(
        $repositoryRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Generated output is outside the repository: $fullPath"
    }
}

if (Test-Path -LiteralPath $artifactsPath) {
    Remove-Item -LiteralPath $artifactsPath -Recurse -Force
}

function Invoke-LockedRestore {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Project,

        [Parameter(Mandatory = $true)]
        [string]$IntermediatePath
    )

    if (Test-Path -LiteralPath $IntermediatePath) {
        Remove-Item -LiteralPath $IntermediatePath -Recurse -Force
    }

    dotnet restore $Project --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "Locked restore failed for $Project with exit code $LASTEXITCODE."
    }
}

function Invoke-TabBuild {
    param([Parameter(Mandatory = $true)][string]$OutputPath)

    Invoke-LockedRestore -Project $tabProject -IntermediatePath $tabIntermediate
    dotnet build $tabProject `
        --configuration Release `
        --no-restore `
        --no-incremental `
        --output $OutputPath `
        /p:ContinuousIntegrationBuild=true `
        /p:Deterministic=true `
        "/p:PathMap=$repositoryRoot=/_/src"

    if ($LASTEXITCODE -ne 0) {
        throw "OkularTabLauncher build failed with exit code $LASTEXITCODE."
    }
}

function Invoke-SessionPublish {
    param([Parameter(Mandatory = $true)][string]$OutputPath)

    Invoke-LockedRestore -Project $sessionProject -IntermediatePath $sessionIntermediate
    dotnet publish $sessionProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained false `
        --no-restore `
        --output $OutputPath `
        /p:PublishSingleFile=true `
        /p:ContinuousIntegrationBuild=true `
        /p:Deterministic=true `
        "/p:PathMap=$repositoryRoot=/_/src"

    if ($LASTEXITCODE -ne 0) {
        throw "OkularSessionLauncher publish failed with exit code $LASTEXITCODE."
    }
}

function Publish-VerifiedArtifact {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FirstExecutable,
        [Parameter(Mandatory = $true)][string]$SecondExecutable
    )

    $firstHash = (Get-FileHash -LiteralPath $FirstExecutable -Algorithm SHA256).Hash
    $secondHash = (Get-FileHash -LiteralPath $SecondExecutable -Algorithm SHA256).Hash
    if ($firstHash -ne $secondHash) {
        throw "$Name reproducibility check failed: $firstHash differs from $secondHash."
    }

    $artifactExecutable = Join-Path $artifactsPath "$Name.exe"
    $hashPath = Join-Path $artifactsPath "$Name.exe.sha256"
    Copy-Item -LiteralPath $FirstExecutable -Destination $artifactExecutable
    [System.IO.File]::WriteAllText(
        $hashPath,
        ($firstHash.ToLowerInvariant() + " *$Name.exe`n"),
        [System.Text.Encoding]::ASCII)

    Write-Output "$Name SHA-256: $firstHash"
}

$tabFirst = Join-Path $verificationPath 'tab-first'
$tabSecond = Join-Path $verificationPath 'tab-second'
$sessionFirst = Join-Path $verificationPath 'session-first'
$sessionSecond = Join-Path $verificationPath 'session-second'
New-Item -ItemType Directory -Force -Path @(
    $tabFirst,
    $tabSecond,
    $sessionFirst,
    $sessionSecond
) | Out-Null

Invoke-TabBuild -OutputPath $tabFirst
Invoke-TabBuild -OutputPath $tabSecond
Invoke-SessionPublish -OutputPath $sessionFirst
Invoke-SessionPublish -OutputPath $sessionSecond

Publish-VerifiedArtifact `
    -Name 'OkularTabLauncher' `
    -FirstExecutable (Join-Path $tabFirst 'OkularTabLauncher.exe') `
    -SecondExecutable (Join-Path $tabSecond 'OkularTabLauncher.exe')

Publish-VerifiedArtifact `
    -Name 'OkularSessionLauncher' `
    -FirstExecutable (Join-Path $sessionFirst 'OkularSessionLauncher.exe') `
    -SecondExecutable (Join-Path $sessionSecond 'OkularSessionLauncher.exe')

Remove-Item -LiteralPath $verificationPath -Recurse -Force
Write-Output "Build succeeded. Both unsigned executables are reproducible."
Write-Output "Artifacts: $artifactsPath"
