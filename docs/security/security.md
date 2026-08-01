# Security and Supply Chain

Injectlynx runs at build time inside consumer projects. Security work must preserve deterministic generation, bounded file access, and transparent package contents.

## Configuration Input

- Keep the C# convention DSL declarative.
- The generator may read syntax, constants, and `typeof(...)` metadata, but must not execute `Configure` methods.
- Reject invalid required strings before creating core models.
- Treat generated identifiers and namespaces as untrusted input until normalized or emitted through safe generation helpers.

## Generator Safety

- Do not add runtime assembly scanning, dynamic graph discovery, or reflection-based service discovery.
- Keep generator behavior deterministic: stable ordering, no environment-dependent output, and no network or filesystem probing.
- Report diagnostics for unsafe or ambiguous cases instead of guessing registrations.
- Keep Roslyn symbols out of long-lived core models.

## Package Release Checklist

Before publishing public packages:

- Build, test, pack, and run active validation scripts in `eng/validation`.
- Run `bash eng/release/verify-packages.sh` to inspect package contents and generate checksums.
- Run `bash eng/release/verify-vulnerabilities.sh` to audit direct and transitive NuGet dependencies.
- Run `bash eng/release/generate-sbom.sh` to write the CycloneDX dependency SBOM.
- Run `bash eng/release/verify-package-manifests.sh` to write normalized package payload manifests.
- Publish with short-lived credentials from CI secrets.
- Record package versions, commit SHA, validation results, and release notes.
