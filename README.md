# Injectlynx: Compile-Time Dependency Injection for .NET

[![NuGet Version](https://img.shields.io/nuget/v/Injectlynx.svg)](https://www.nuget.org/packages/Injectlynx)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Injectlynx.svg)](https://www.nuget.org/packages/Injectlynx)

[NuGet](https://www.nuget.org/packages/Injectlynx) · [Source](https://github.com/it-nilesh/Injectlynx) · [Contributing](CONTRIBUTING.md) · [MIT License](LICENSE) · [Security](SECURITY.md)

Injectlynx is an attribute-free, convention-based compile-time dependency injection toolkit for .NET. It uses a Roslyn incremental source generator to create deterministic `Microsoft.Extensions.DependencyInjection` registrations at build time.

Developers configure services with a strongly typed C# DSL. Service implementation classes do not need attributes, runtime reflection scanning, or a custom container.

```bash
dotnet add package Injectlynx
```

## Contents

- [Why Injectlynx?](#why-injectlynx)
- [Supported targets](#supported-targets)
- [Quick start](#quick-start)
- [Registration patterns](#registration-patterns)
- [Member injection](#member-injection)
- [Diagnostics and investigation](#diagnostics-and-investigation)
- [Samples](#samples)
- [Build and test](#build-and-test)

## Why Injectlynx?

- Attribute-free: keep service classes clean and framework-independent.
- Compile-time generation: no runtime assembly scanning or reflection-based discovery.
- IDE friendly: configuration is normal C# with IntelliSense and refactoring support.
- Native AOT friendly: registrations are generated as source.
- Deterministic diagnostics: invalid conventions fail during build.
- Microsoft DI compatible: generated code targets `IServiceCollection`.

## Supported Targets

The public package targets `netstandard2.0`, `net8.0`, `net9.0`, and `net10.0`. Application samples and validation cover .NET 8, .NET 9, .NET 10, and Native AOT.

The generator is delivered as an analyzer inside the `Injectlynx` package, so most applications only install one package.

## Quick Start

Create a convention module in the project that owns the services:

```csharp
using Injectlynx;

namespace Shop.Application;

public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .FromNamespace("Shop.Application.Services")
            .WhereNameEndsWith("Service")
            .AsMatchingInterface()
            .WithScopedLifetime();
    }
}
```

If `OrderService : IOrderService`, Injectlynx generates a registration equivalent to:

```csharp
services.AddScoped<IOrderService, OrderService>();
```

Call the generated extension method during startup:

```csharp
builder.Services.AddInjectlynxServices();
```

`AddInjectlynxServices()` is the default generated method name. Developers do not write it manually. For project-specific startup methods, configure the module:

```csharp
services
    .ModuleName("Infrastructure")
    .GeneratedMethod("AddInfrastructureServices")
    .GeneratedNamespace("Shop.Infrastructure.DependencyInjection");
```

Then call it from the consuming app:

```csharp
using Shop.Infrastructure.DependencyInjection;

builder.Services.AddInfrastructureServices();
```

## Registration Patterns

Register concrete classes without interfaces:

```csharp
services
    .FromNamespace("Shop.Infrastructure.Clients")
    .WhereNameEndsWith("Client")
    .AsSelf()
    .WithSingletonLifetime();
```

Register by matching interface name:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

Register all implemented interfaces:

```csharp
services
    .FromNamespace("Shop.Application.Processors")
    .WhereNameEndsWith("Processor")
    .AsImplementedInterfaces()
    .WithTransientLifetime();
```

Register a specific interface when one class implements several interfaces:

```csharp
services
    .Register<IWriteOrderStore, OrderStore>()
    .WithScopedLifetime();
```

Other supported scenarios include exclusions, explicit registrations, keyed registrations, decorators, external/framework-provided service declarations, architecture rules, diagnostic severity overrides, custom generated method names, and custom generated namespaces.

## Member Injection

Constructor injection should remain the default. Use member injection only when adapting legacy code, framework-created objects, or initialization methods that cannot be expressed cleanly through constructors.

```csharp
services
    .For<OrderService>()
    .InjectProperty(static service => service.Clock)
    .InjectOptionalProperty(static service => service.Logger)
    .InjectMethod("Initialize")
    .WithConstantArgument("sampleName", "minimal-api")
    .WithServiceArgument<object>("state");
```

Method parameters can be provided from constants or resolved services. If a method takes `string sampleName, object state`, the configured constant value is passed for `sampleName`, and the service provider resolves `object` for `state`.

## Diagnostics and Investigation

To inspect generated registrations during development:

```bash
dotnet build -p:InjectlynxDevelopmentReport=true
```

To show the report in normal console output:

```bash
dotnet build -p:InjectlynxDevelopmentReport=warning
```

This emits development diagnostics such as:

```text
OrderService -> IOrderService (Scoped)
```

Keep the development report disabled in CI and production builds unless you are investigating registration behavior.

## Samples

- `samples/MinimalApi`: conventions, open generic handlers, constructor/property/method injection.
- `samples/WebApi`: custom generated method, explicit registrations, keyed services, decorators, external services, architecture rules.
- `samples/WorkerService`: hosted worker dependencies.
- `samples/NativeAot`: Native AOT validation without runtime scanning.
- `samples/PluginHost` and `samples/PluginSample`: opt-in runtime plugin loading through the main `Injectlynx` package.

Build a sample:

```bash
dotnet build samples/WebApi/WebApi.csproj
```

## Build and Test

```bash
dotnet restore Injectlynx.slnx
dotnet build Injectlynx.slnx --no-restore
dotnet test Injectlynx.slnx --no-build
dotnet pack src/Injectlynx/Injectlynx.csproj -c Debug --no-build -o artifacts/packages
```

Release validation scripts:

```bash
bash eng/release/verify-packages.sh
bash eng/validation/validate-local-package.sh
bash eng/validation/validate-native-aot.sh
bash eng/validation/validate-trimming.sh
```

## Documentation

- [Positioning](docs/guides/positioning.md)
- [Registration Approaches](docs/guides/registration-approaches.md)
- [Migrate From Manual Registration](docs/guides/migrate-from-manual-registration.md)
- [Migrate From Runtime Scanning](docs/guides/migrate-from-runtime-scanning.md)
- [Troubleshooting](docs/guides/troubleshooting.md)
- [Generated Output Examples](docs/reference/generated-output-examples.md)
- [Configuration DSL](docs/configuration/configuration.md)
- [Member Injection DSL](docs/configuration/member-injection-dsl.md)
- [Diagnostics](docs/diagnostics/diagnostics.md)
- [Analyzers And Code Fixes](docs/analyzers/analyzers.md)
- [Generator Architecture](docs/generator/generator.md)
- [Generator Transparency](docs/generator/transparency.md)
- [CLI Inspection Tooling](docs/cli/cli.md)
- [Performance And Benchmarks](docs/benchmarks/performance.md)
- [Native AOT](docs/native-aot/native-aot.md)
- [Dynamic Plugin Loading](docs/plugins/dynamic-plugin-loading.md)
- [Compatibility Matrix](docs/compatibility.md)
- [Release Process](docs/release.md)
- [Community](docs/community.md)
- [Common Architecture Styles](docs/guides/common-architecture-styles.md)
- [Packaging](docs/packaging/packaging.md)

## Package Notes

NuGet versions are released from Git tags using `vMAJOR.MINOR.PATCH`, for example `v1.0.1` publishes package version `1.0.1`.

## License

Injectlynx is licensed under the [MIT License](LICENSE).
