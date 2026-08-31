# Signing policy

## Status

Code signing is not enabled yet. GitHub Actions currently produces an unsigned, reproducibility-checked development artifact. No claim is made that SignPath Foundation or another certificate authority has approved this project.

## Goals

Release signing must provide:

- an auditable link from a public commit and version tag to the unsigned build;
- a protected signing operation that cannot be triggered by untrusted pull-request code;
- Authenticode verification and timestamping;
- published SHA-256 hashes for distributed files;
- no private certificate keys or signing secrets in the repository.

## Proposed release requirements

A release may be signed only when all of the following are true:

1. the source is in the public repository;
2. the commit is reachable from a protected, annotated version tag;
3. the normal build workflow succeeds;
4. two clean builds of the unsigned executable have identical SHA-256 hashes;
5. required manual Windows/Okular tests are recorded for the release candidate;
6. the signing workflow uses a reviewed configuration and protected environment;
7. the signed executable is verified after signing;
8. both release notes and checksums identify the exact artifact.

Pull-request workflows must never receive signing credentials or sign artifacts. The project will prefer a service designed for open-source code signing, such as SignPath Foundation, if the project is accepted and the service's current requirements can be met.

## Reproducibility and signatures

The reproducibility check applies to the **unsigned** executable. Authenticode adds a signature and usually a trusted timestamp, so a signed file has a different SHA-256 hash. The signed release hash must therefore be generated after signing and published separately from the unsigned build hash.

## User verification

For a future signed release:

```powershell
Get-AuthenticodeSignature .\OkularTabLauncher.exe | Format-List
Get-FileHash -Algorithm SHA256 .\OkularTabLauncher.exe
```

Users should require a valid Authenticode status, the expected publisher, and a hash matching the release checksum. A signature does not bypass Windows policy; Windows remains the final authority on whether an executable may run.

## Incident response

If signing credentials, workflows, or released artifacts may have been compromised, signing and publication will stop. The affected release will be identified publicly, relevant credentials or service access will be revoked, and a corrected release will use a new version number.
