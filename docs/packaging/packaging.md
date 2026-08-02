# Packaging

Normal .NET 8, .NET 9, and .NET 10 consumers should install the primary package:

```bash
dotnet add package Injectlynx
```

The primary package is represented by `src/Injectlynx/Injectlynx.csproj`. It packs:

- `Injectlynx.dll` under `lib/netstandard2.0`, `lib/net8.0`, `lib/net9.0`, and `lib/net10.0` with the public `IServiceConventionBuilder` DSL surface.
- `Injectlynx.Generator.dll` under `analyzers/dotnet/cs`.
- `Injectlynx.Analyzers.dll` and `Injectlynx.CodeFixes.dll` under `analyzers/dotnet/cs`.
- `Injectlynx.Core.dll` under `analyzers/dotnet/cs`.
- Portable PDBs for Injectlynx generator assemblies under `analyzers/dotnet/cs`.

The package targets `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0` for the public DSL assembly and ships the generator under `analyzers/dotnet/cs`.

```bash
dotnet pack src/Injectlynx/Injectlynx.csproj
```

## Local Consumer Validation

After packing, run validation scripts to prove fresh Web SDK consumers can install `Injectlynx` from `artifacts/packages`, load the generator, compile the C# convention DSL, and call the generated service-registration extension method.

```bash
dotnet pack src/Injectlynx/Injectlynx.csproj -c Debug -o artifacts/packages
bash eng/validation/validate-local-package.sh
bash eng/release/verify-packages.sh
bash eng/release/verify-vulnerabilities.sh
bash eng/release/generate-sbom.sh
bash eng/release/verify-package-manifests.sh
bash eng/validation/validate-trimming.sh
```

`eng/validation/validate-local-package.sh` creates fresh temporary consumers and builds them across `net8.0`, `net9.0`, and `net10.0`. It validates both the default `AddInjectlynxServices()` method and custom generated method/namespace usage.
