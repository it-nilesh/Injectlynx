# Injectlynx

Injectlynx is an attribute-free, compile-time dependency injection toolkit for .NET 8, .NET 9, and .NET 10. Developers configure services with a strongly typed C# convention DSL, and a Roslyn source generator emits `Microsoft.Extensions.DependencyInjection` registrations during build.

No service attributes, runtime reflection scanning, or custom container are required.

[Contributing](CONTRIBUTING.md) · [MIT License](LICENSE) · [Security](SECURITY.md)

## Install

```bash
dotnet add package Injectlynx
```

During source-tree development, samples reference the local `src/Injectlynx` project and generator assemblies directly.

## Supported Targets

Injectlynx is validated for .NET 8, .NET 9, and .NET 10 application projects. The public DSL assembly targets `netstandard2.0`, and the Roslyn generator is packaged as an analyzer.

## Quick Start

Create a module-level convention class:

```csharp
using Injectlynx;

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

Use the generated extension method:

```csharp
builder.Services.AddInjectlynxServices();
```

By default, Injectlynx generates `AddInjectlynxServices()` for every project.

Use `GeneratedMethod("AddInfrastructureServices")` or another project-specific name when multiple libraries expose generated registrations to the same app, or when your team wants a more explicit startup call.

If you also use `GeneratedNamespace("MyApp.DependencyInjection")`, add `using MyApp.DependencyInjection;` in `Program.cs`. The method is generated at build time; developers do not write it manually.

For `OrderService : IOrderService`, Injectlynx generates a registration equivalent to:

```csharp
services.AddScoped<IOrderService, OrderService>();
```

## Supported DSL Scenarios

- Convention registration by namespace, class prefix/suffix, interface prefix/suffix, and open generic assignability.
- Registration strategies: matching interface, all implemented interfaces, self, or matching interface plus self.
- Singleton, scoped, and transient lifetimes.
- Exclusions with `ExcludeNamespace(...)` and `ExcludeType<T>()`.
- Explicit and keyed registrations with `Register<TService, TImplementation>()` and `WithKey(...)`.
- External/framework-provided service declarations for dependency diagnostics.
- Decorators with `Decorate<TService, TDecorator>()`.
- Architecture guardrails with `ForbidDependency()`.
- Diagnostic severity overrides with `Diagnostic("INJ401").AsWarning()`.
- Custom generated method and namespace names.
- Opt-in property and method injection for cases where constructor injection is not practical.

## Registration Examples

Concrete class only:

```csharp
services
    .FromNamespace("Shop.Infrastructure.Clients")
    .WhereNameEndsWith("Client")
    .AsSelf()
    .WithSingletonLifetime();
```

All implemented interfaces:

```csharp
services
    .FromNamespace("Shop.Application.Processors")
    .WhereNameEndsWith("Processor")
    .AsImplementedInterfaces()
    .WithScopedLifetime();
```

Only interfaces with a naming pattern:

```csharp
services
    .FromNamespace("Shop.Application.Stores")
    .WhereInterfaceNameStartsWith("IRead")
    .AsImplementedInterfaces()
    .WithTransientLifetime();
```

One specific interface from a multi-interface implementation:

```csharp
services
    .Register<IWriteOrderStore, OrderStore>()
    .WithScopedLifetime();
```

## Development Investigation

To inspect generated registrations during development:

```bash
dotnet build -p:InjectlynxDevelopmentReport=true
```

For normal console-visible output, temporarily use:

```bash
dotnet build -p:InjectlynxDevelopmentReport=warning
```

This emits `INJ900` diagnostics such as:

```text
OrderService -> IOrderService (Scoped)
```

Keep this disabled in CI and production builds.

## Samples

- `samples/MinimalApi`: broad DSL coverage, including member injection and open generic handlers.
- `samples/WebApi`: controller-based app with explicit/keyed registrations, decorators, external services, and architecture rules.
- `samples/WorkerService`: hosted background worker registration.
- `samples/NativeAot`: Native AOT-focused sample without runtime scanning.

Build a sample:

```bash
dotnet build samples/WebApi/WebApi.csproj --no-restore
```

## Future Tooling Roadmap

- Add `Injectlynx.Analyzers` only if generator diagnostics become too large to maintain inside the source generator.
- Add `Injectlynx.CodeFixes` after analyzer rules are stable and IDE quick fixes are worth maintaining.
- Add `Injectlynx.Cli` if users ask for graph or inspection tooling outside the IDE.

## Validation

```bash
dotnet build Injectlynx.slnx
dotnet test Injectlynx.slnx --no-build
dotnet pack src/Injectlynx/Injectlynx.csproj -c Debug --no-build -o artifacts/packages
bash eng/release/verify-packages.sh
bash eng/validation/validate-local-package.sh
```

See `docs/configuration/configuration.md` for the full DSL guide.
