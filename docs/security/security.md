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

## Dynamic Plugin Loading

Dynamic plugin loading is opt-in runtime behavior and has a different security profile from the default source-generated registration path.

- Load plugins only from host-controlled directories or explicit trusted assembly paths.
- Treat plugin configuration JSON and plugin manifests as untrusted input until parsed, normalized, and validated.
- Prefer explicit `manifestFiles` or `pluginAssemblies` for locked-down hosts; directory discovery is more convenient but expands the runtime trust boundary.
- Use `disabledPlugins` to block known-bad plugin names without deleting deployment files.
- Keep plugin dependencies local to the plugin folder unless they are intentionally shared with the host.
- Review `INJP` diagnostics during startup and fail fast with `ThrowOnError` for production hosts that require strict plugin policy.
- Use manifest `sha256` values for plugins distributed outside a trusted deployment pipeline.
- Do not treat SHA-256 checks as a replacement for code signing or repository trust; they only verify that a file matches an expected payload.
- Reject plugins that target a newer major .NET version than the host.
- Prefer collectible load contexts for long-running hosts that need unload handles, and test unload behavior before relying on it operationally.

## Website And Documentation

- Keep public website links focused on maintained docs only.
- Use canonical URLs and the sitemap for the production site at `https://injectlynx.inilesh.dev/`.
- Redirect retired standalone pages instead of leaving stale content indexed.
- Avoid publishing obsolete roadmap, article, benchmark, or community docs when they are not part of the maintained repository surface.

## Package Release Checklist

Before publishing public packages:

- Build, test, pack, and run active validation scripts in `eng/validation`.
- Run `bash eng/release/verify-packages.sh` to inspect package contents and generate checksums.
- Run `bash eng/release/verify-vulnerabilities.sh` to audit direct and transitive NuGet dependencies.
- Run `bash eng/release/generate-sbom.sh` to write the CycloneDX dependency SBOM.
- Run `bash eng/release/verify-package-manifests.sh` to write normalized package payload manifests.
- Review `docs/release.md` for package metadata, dry-run, and release-notes guidance.
- Publish with short-lived credentials from CI secrets.
- Record package versions, commit SHA, validation results, and release notes.
