# SPDX-License-Identifier: MIT

[CmdletBinding()]
param(
    [string]$ExecutablePath = (Join-Path $PSScriptRoot '..\artifacts\OkularSessionLauncher.exe'),
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
$source = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "OkularSessionLauncher executable not found: $source"
}

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$installDirectory = Join-Path $localAppData 'OkularSessionLauncher'
$target = Join-Path $installDirectory 'OkularSessionLauncher.exe'
$startupDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
$shortcutPath = Join-Path $startupDirectory 'Okular Session Monitor.lnk'

New-Item -ItemType Directory -Force -Path $installDirectory | Out-Null

$running = Get-Process -Name 'OkularSessionLauncher' -ErrorAction SilentlyContinue |
    Where-Object {
        try {
            [System.IO.Path]::GetFullPath($_.Path) -eq [System.IO.Path]::GetFullPath($target)
        }
        catch {
            $false
        }
    }

if ($running) {
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

if (Test-Path -LiteralPath $target -PathType Leaf) {
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupDirectory = Join-Path $installDirectory "Backups\$timestamp"
    New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
    Copy-Item -LiteralPath $target -Destination (Join-Path $backupDirectory 'OkularSessionLauncher.exe')
}

Copy-Item -LiteralPath $source -Destination $target -Force

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $target
$shortcut.Arguments = '--monitor'
$shortcut.WorkingDirectory = $installDirectory
$shortcut.Description = 'Automatically saves and restores Okular tab sessions'
$shortcut.Save()

if (-not $NoStart) {
    Start-Process `
        -FilePath $target `
        -ArgumentList '--monitor' `
        -WorkingDirectory $installDirectory `
        -WindowStyle Hidden
}

Write-Output "Installed: $target"
Write-Output "Startup shortcut: $shortcutPath"
Write-Output 'Existing session and log files were preserved.'
