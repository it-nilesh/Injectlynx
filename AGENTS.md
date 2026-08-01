# Repository Guidelines

## Project Structure & Module Organization

Injectlynx is a .NET solution (`Injectlynx.slnx`) organized by package responsibility. Production code lives under `src/`: `Injectlynx.Core` holds immutable domain models, `Injectlynx.Generator` reads the C# convention DSL and emits Microsoft DI registrations, and `Injectlynx` is the primary NuGet packaging project. Tests mirror active projects under `tests/`. Samples live in `samples/MinimalApi`, `samples/WebApi`, `samples/WorkerService`, and `samples/NativeAot`; docs are grouped by topic under `docs/`; packaging and validation scripts are in `eng/`.

## Build, Test, and Development Commands

- `dotnet build Injectlynx.slnx` compiles all projects and analyzers.
- `dotnet test Injectlynx.slnx --no-build` runs the full test suite after a successful build.
- `dotnet pack src/Injectlynx/Injectlynx.csproj -c Debug --no-build -o artifacts/packages` creates the primary package.
- `bash eng/validation/validate-local-package.sh` builds a temporary consumer against the local `.nupkg`.
- `bash eng/validation/validate-native-aot.sh` publishes the Native AOT sample.

## Coding Style & Naming Conventions

Use C# with nullable reference types enabled and latest language features, as configured in `Directory.Build.props`. Keep projects deterministic and warning-clean because warnings are treated as errors. Use four-space indentation, `PascalCase` for public types and members, `camelCase` for locals and parameters, and `Async` suffixes for asynchronous methods. Prefer immutable models and deterministic ordering in generator, analyzer, and configuration code.

## Testing Guidelines

Tests use xUnit. Add focused tests for every behavioral change, especially generator output, DSL diagnostics, core model behavior, and package validation. Name test classes after the unit under test and use descriptive method names such as `Generator_ReadsStronglyTypedConventionDsl`.

## Commit & Pull Request Guidelines

This workspace is not currently a Git repository, so no local history convention is available. Use concise imperative commit messages, such as `Add configuration analyzer` or `Fix open generic registration`. Pull requests should include a short summary, affected projects, test results, linked issues, and screenshots only when UI or documentation rendering changes.

## Security & Configuration Tips

Do not commit credentials, personal tokens, or machine-specific files. Use the module-level C# convention DSL for new samples. Avoid runtime reflection or assembly scanning.
