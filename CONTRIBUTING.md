# Contributing

Thank you for helping improve Injectlynx. This project is a compile-time dependency injection toolkit for .NET, so changes should keep generation deterministic, fast, and friendly to Native AOT.

## Development Setup

Use the .NET SDK version supported by the repository and restore/build from the repository root:

```bash
dotnet build Injectlynx.slnx
dotnet test Injectlynx.slnx --no-build
```

## Contribution Guidelines

- Keep configuration attribute-free and based on the C# convention DSL.
- Do not add runtime assembly scanning, reflection-based discovery, or custom container behavior.
- Prefer small, focused changes with tests.
- Add or update generator tests for DSL parsing, diagnostics, generated registrations, and source output.
- Update docs when public behavior, diagnostics, samples, or package contents change.

## Pull Requests

Before opening a pull request, run:

```bash
dotnet build Injectlynx.slnx
dotnet test Injectlynx.slnx --no-build
dotnet pack src/Injectlynx/Injectlynx.csproj -c Debug --no-build -o artifacts/packages
bash eng/release/verify-packages.sh
bash eng/validation/validate-local-package.sh
```

In the PR description, include a summary, affected projects, validation results, and any linked issue.
