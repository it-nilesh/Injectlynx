# Migrate From Runtime Scanning

This guide shows how to replace runtime service scanning with Injectlynx compile-time conventions.

## 1. Identify The Runtime Scan Rule

A runtime scanning setup often contains rules such as:

```csharp
services.Scan(scan => scan
    .FromAssemblyOf<OrderService>()
    .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Service")))
    .AsMatchingInterface()
    .WithScopedLifetime());
```

Convert each rule into an Injectlynx convention.

## 2. Add A Compile-Time Convention

Create a convention class:

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

## 3. Replace Runtime Scan Startup Code

Remove the scanning call and use the generated method:

```csharp
builder.Services.AddInjectlynxServices();
```

The application still uses Microsoft DI. The difference is that discovery happens during build, not startup.

## 4. Convert Open Generic Rules

Runtime scanning for handlers can often become:

```csharp
services
    .FromNamespace("Shop.Application.Handlers")
    .AssignableToOpenGeneric(typeof(IRequestHandler<>))
    .AsImplementedInterfaces()
    .WithTransientLifetime();
```

## 5. Convert Exclusions

Runtime scanning exclusions can become:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .ExcludeNamespace("Shop.Application.Services.Internal")
    .ExcludeType<LegacyOrderService>()
    .AsMatchingInterface()
    .WithScopedLifetime();
```

## 6. Review Dynamic Loading Requirements

Injectlynx is compile-time by default. If the previous scanning setup discovered assemblies loaded only at runtime, keep that path separate from the default Injectlynx path.

For dynamically loaded plugins, plan an explicit plugin-loading design with clear Native AOT and trimming tradeoffs.

## 7. Build And Fix Diagnostics

Run:

```bash
dotnet build
```

If a convention cannot be read statically, fix the DSL shape. For example, use compile-time constant strings instead of local variables for namespace filters.

## Migration Checklist

- [ ] List existing scan rules.
- [ ] Convert each scan rule to a convention module.
- [ ] Replace startup scanning with generated extension method calls.
- [ ] Convert open generic rules.
- [ ] Convert exclusions.
- [ ] Keep runtime-loaded plugin behavior separate.
- [ ] Build and fix diagnostics.

