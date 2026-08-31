<p align="center">
  <img src="assets/OkularTabLauncher.png" width="128" height="128" alt="OkularTabLauncher icon">
</p>

# OkularTabLauncher

Open a PDF as a new tab in an existing Okular window on Windows.

[Leia em Português do Brasil](docs/README.pt-BR.md)

> [!IMPORTANT]
> Current workflow artifacts are unsigned development builds. Publishing the source and building it on GitHub does not, by itself, make Windows Smart App Control trust the executable. Code signing is a separate, protected release step described in [SIGNING_POLICY.md](SIGNING_POLICY.md).

## Why this exists

In some Windows installations, invoking `okular.exe --unique file.pdf` still opens a second Okular window instead of adding a tab to the window already in use. OkularTabLauncher preserves the working behavior by automating Okular's existing window:

1. restore and focus the largest visible Okular window;
2. send `Ctrl+O`;
3. identify the newly created Open dialog;
4. write the full Unicode PDF path directly into the file-name control;
5. activate the Open button.

If Okular is not running, the launcher starts it normally with the PDF.

## Safety properties

- Accepts exactly one absolute path with a `.pdf` extension.
- Verifies that the file exists.
- Does not invoke `cmd.exe`, PowerShell, or a shell to open the document.
- Does not use the clipboard.
- Records all top-level windows before sending `Ctrl+O` and only considers a newly created dialog.
- Serializes simultaneous invocations with `Local\OkularTabLauncherV2`.
- Produces no console window during normal use.

The launcher does not parse PDF contents. Document security remains the responsibility of Okular and its PDF backend.

## Requirements

- Windows 11 x64;
- Okular for Windows;
- .NET Framework 4.8 at runtime;
- PowerShell 7 and the pinned .NET SDK only when building from source.

When Okular is closed, its executable is resolved in this order:

1. `OKULAR_TAB_LAUNCHER_OKULAR_EXE` environment variable;
2. `D:\Scoop\apps\okular\current\bin\okular.exe`;
3. the default per-user Scoop directory;
4. common `%ProgramFiles%\Okular` locations.

## Build from source

```powershell
git clone <repository-url>
Set-Location OkularTabLauncher
pwsh -NoProfile -File .\scripts\build.ps1
```

The build script restores only locked dependencies, performs two builds with separate clean intermediate state, compares both executables byte for byte through SHA-256, and creates:

```text
artifacts/OkularTabLauncher.exe
artifacts/OkularTabLauncher.exe.sha256
```

Verify the result with:

```powershell
Get-FileHash -Algorithm SHA256 .\artifacts\OkularTabLauncher.exe
Get-Content .\artifacts\OkularTabLauncher.exe.sha256
```

The same script is used by GitHub Actions. The .NET SDK is pinned in `global.json`; the .NET Framework reference assemblies are pinned in `src/packages.lock.json`; third-party GitHub Actions are pinned to complete commit hashes.

## Test without changing file associations

Keep the installed launcher untouched and invoke the development build directly:

```powershell
& .\artifacts\OkularTabLauncher.exe 'C:\full\path\test.pdf'
$LASTEXITCODE
```

Test at least these cases before a release:

- Okular closed;
- Okular open with one or several tabs;
- Okular minimized;
- paths containing spaces, accents, OneDrive folders, and other valid Unicode characters;
- two PDFs opened nearly simultaneously;
- another application's Open dialog already visible;
- missing file, relative path, and a file that is not a PDF.

Logs are written to:

```text
%LOCALAPPDATA%\OkularTabLauncher\last-run.txt
%LOCALAPPDATA%\OkularTabLauncher\last-error.txt
```

Exit codes are `0` for success, `1` for an unexpected failure, `2` for invalid input, `3` for a mutex timeout, `4` when Okular cannot be found, and `5` for an automation failure.

## Install

Do not replace a working launcher merely to test an unsigned artifact. After validating a signed release:

1. back up the executable currently installed in `%LOCALAPPDATA%\OkularTabLauncher`;
2. verify the release SHA-256 and Authenticode signature;
3. copy the signed `OkularTabLauncher.exe` into that directory;
4. invoke it directly with a test PDF;
5. only then select it as the PDF application through Windows **Settings > Apps > Default apps**.

This project deliberately does not write the protected `UserChoice` registry value.

## Restore the previous PDF association

Open Windows **Settings > Apps > Default apps**, search for `.pdf`, and select the application that was used before OkularTabLauncher. Removing the executable alone does not reliably restore a previous association.

## Known limitations

- UI automation depends on Windows focus rules and the Open dialog exposed by the installed Okular/Qt version.
- Open-dialog title recognition currently includes Portuguese and English; native dialog class and control identifiers provide language-independent signals where available.
- The launcher targets one primary Okular window, selected by largest visible area.
- Applications running at different integrity levels may be isolated by Windows UIPI.
- An unsigned build can still be blocked by Smart App Control.

## Contributing and security

See [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Report vulnerabilities according to [SECURITY.md](SECURITY.md). Release signing rules are documented in [SIGNING_POLICY.md](SIGNING_POLICY.md).

## License and trademarks

The source code and the original OkularTabLauncher icon are licensed under the [MIT License](LICENSE).

Okular is a KDE project. OkularTabLauncher is an independent interoperability utility and is not affiliated with, endorsed by, or distributed by KDE or the Okular project. No Okular program files or artwork are included.
