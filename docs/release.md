# Release Process

NuGet versions are released from Git tags using `vMAJOR.MINOR.PATCH`, for example `v1.0.1` publishes package version `1.0.1`.

## Release Notes

Release notes are generated from `CHANGELOG.md`:

```bash
bash eng/release/generate-release-notes.sh 1.0.1 CHANGELOG.md release-notes.md
```

The release workflow calls the same script before creating or updating the GitHub release.

## Validation

Run the release validation scripts before publishing:

```bash
dotnet restore Injectlynx.slnx
dotnet build Injectlynx.slnx -c Release --no-restore
dotnet test Injectlynx.slnx -c Release --no-build --no-restore
dotnet pack src/Injectlynx/Injectlynx.csproj -c Release --no-build -o artifacts/packages
bash eng/release/verify-packages.sh
bash eng/release/verify-vulnerabilities.sh
bash eng/release/generate-sbom.sh
bash eng/release/verify-package-manifests.sh
bash eng/release/verify-reproducible-packages.sh
bash eng/validation/validate-local-package.sh
```

## Release Checklist

- Confirm `CHANGELOG.md` has user-facing entries.
- Confirm package metadata, README, and docs match the released behavior.
- Confirm sample projects build on supported target frameworks.
- Confirm Native AOT and trimming notes are accurate for the release.
- Confirm generated release notes before publishing the GitHub release.

## NuGet Metadata Checklist

Before pushing a package, review:

- `PackageId`, `Description`, `Authors`, license, project URL, repository URL, and tags.
- `PackageReadmeFile` and the packaged `README.md`.
- Analyzer assets under `analyzers/dotnet/cs`.
- Runtime assemblies under every supported `lib/` target framework.
- Build-transitive assets under `buildTransitive/`.
- Release notes generated from `CHANGELOG.md`.

`eng/release/verify-packages.sh` validates required package entries, including the packaged README.

## Dry Run

Use a local dry run before creating a release tag:

```bash
dotnet restore Injectlynx.slnx
dotnet build Injectlynx.slnx -c Release --no-restore
dotnet test Injectlynx.slnx -c Release --no-build --no-restore
dotnet pack src/Injectlynx/Injectlynx.csproj -c Release --no-build -o artifacts/packages -p:Version=1.0.1
PACKAGE_VERSION=1.0.1 bash eng/release/verify-packages.sh
PACKAGE_VERSION=1.0.1 bash eng/release/verify-package-manifests.sh
bash eng/release/generate-release-notes.sh 1.0.1 CHANGELOG.md release-notes.md
```

The dry run should produce package artifacts, checksums, manifests, and release notes without publishing anything.
