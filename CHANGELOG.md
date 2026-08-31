# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project intends to follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html) after its first public release.

## [Unreleased]

### Added

- SDK-style .NET Framework 4.8 project.
- Deterministic, path-independent double-build verification and SHA-256 artifact generation.
- GitHub Actions build workflow with immutable action references.
- Strict absolute `.pdf` path validation and explicit exit codes.
- Safer Open-dialog selection based on windows created after `Ctrl+O`.
- Configurable Okular path with Scoop and Program Files fallbacks.
- Public security and signing policies.
- Original project icon licensed under MIT.

### Changed

- Error logs are cleared only after the launcher observes a successful start or a closed Open dialog.
- Mutex ownership is tracked before release.
