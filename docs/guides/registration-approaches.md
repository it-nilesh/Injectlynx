# Registration Approaches

This guide explains how Injectlynx compares with manual `IServiceCollection` registration and runtime scanning.

## Manual Registration

Manual registration is explicit and familiar:

```csharp
services.AddScoped<IOrderService, OrderService>();
services.AddScoped<IInvoiceService, InvoiceService>();
services.AddSingleton<IClockService, ClockService>();
```

Manual registration works well for small applications. It becomes harder to maintain when the application grows across feature modules, infrastructure libraries, decorators, keyed services, and open generic handlers.

Common issues:

- Startup files become long registration lists.
- New services can be forgotten.
- Interface renames can leave stale registrations.
- Similar modules repeat the same registration pattern.

## Runtime Scanning

Runtime scanning reduces manual registration code:

```csharp
services.Scan(...);
```

Runtime scanning is convenient, but it discovers services while the application starts. That can make registration behavior harder to inspect during build, and it can create extra friction for trimming-sensitive or Native AOT-oriented applications.

Common tradeoffs:

- Discovery happens at runtime.
- Behavior can depend on loaded assemblies.
- Reflection-based scanning can be harder to trim.
- Errors may appear later than a compiler diagnostic.

## Injectlynx Generated Registrations

Injectlynx keeps the Microsoft DI model but moves registration discovery to build time:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

Generated output is normal Microsoft DI registration code:

```csharp
services.AddScoped<IOrderService, OrderService>();
services.AddScoped<IInvoiceService, InvoiceService>();
```

This keeps the consuming application familiar:

```csharp
builder.Services.AddInjectlynxServices();
```

## Decision Guide

Use manual registration when:

- The application has only a few services.
- Every registration needs custom logic.
- The team prefers fully hand-written startup code.

Use runtime scanning when:

- Runtime discovery is required.
- Assemblies are intentionally loaded dynamically.
- Native AOT and trimming are not design goals.

Use Injectlynx when:

- The application already uses Microsoft DI.
- Most services follow consistent naming or namespace conventions.
- The team wants fewer startup registrations without runtime scanning.
- Build-time diagnostics and deterministic generated output matter.

