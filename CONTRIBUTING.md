# Contributing

Thank you for helping improve OkularTabLauncher.

## Before changing code

- Keep the project limited to opening a PDF as a tab in an existing Okular window on Windows.
- Do not replace the Win32 automation with `okular.exe --unique` without reproducible evidence that it creates a tab in the affected Windows environment.
- Do not add code that disables or bypasses Smart App Control, changes the protected PDF `UserChoice` value, invokes a command shell, or downloads executable content.
- Avoid new runtime dependencies unless the benefit and security impact are documented.

## Development workflow

1. Create a focused branch.
2. Make the smallest behavior-preserving change possible.
3. Run `pwsh -NoProfile -File .\scripts\build.ps1`.
4. Perform the relevant manual tests from the README.
5. Remove personal paths and document names from logs before sharing them.
6. Explain behavior changes, risks, and test evidence in the pull request.

Treat compiler warnings as errors. Preserve Unicode paths, the `WinExe` output type, the mutex name, and error logging unless a migration plan is included.

## Pull requests

A pull request should contain one logical change, pass GitHub Actions, and avoid generated binaries. Changes affecting window selection, focus, dialog identification, process launching, or signing require an explicit security analysis.
