# Configuration

Injectlynx uses an attribute-free, strongly typed C# convention DSL. Service implementation classes do not need Injectlynx attributes, and the generator does not execute configuration code.

## Minimal Example

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

The generator reads this restricted fluent shape from syntax and semantic constants. It does not instantiate the builder, run reflection, scan assemblies, or execute `Configure`.

## Consumer Setup

Application projects reference only the primary package:

```bash
dotnet add package Injectlynx
```

No separate generator package or implementation attributes are required. The package provides the DSL types for IntelliSense and the source generator for build-time registration output.

## Supported DSL

Selectors:

- `FromNamespace("Shop.Application.Services")`
- `WhereNameStartsWith("Default")`
- `WhereNameEndsWith("Service")`
- `WhereInterfaceNameStartsWith("IRead")`
- `WhereInterfaceNameEndsWith("Handler")`
- `AssignableToOpenGeneric(typeof(IRequestHandler<>))`

Registration strategies:

- `AsMatchingInterface()`
- `AsImplementedInterfaces()`
- `AsSelf()`
- `AsMatchingInterfaceAndSelf()`

`AsMatchingInterface()` requires a matching `I{ClassName}` contract. For example, `OrderService` must implement `IOrderService`. If the interface is removed, rebuild reports `INJ001` as an error. Use `AsSelf()` for concrete-only services.

`AsImplementedInterfaces()` requires at least one implemented interface after interface-name filters are applied. If no interface is available, rebuild reports `INJ004` as an error. Use `AsSelf()` when concrete registration is intended.

Exclusions:

- `ExcludeNamespace("Shop.Application.Services.Internal")`
- `ExcludeType<LegacyOrderService>()`

Lifetimes:

- `WithSingletonLifetime()`
- `WithScopedLifetime()`
- `WithTransientLifetime()`

Member injection:

- `For<TImplementation>()`
- `InjectProperty(static service => service.Clock)`
- `InjectOptionalProperty(static service => service.Logger)`
- `InjectMethod("Initialize")`
- `WithConstantArgument("name", "orders")`
- `WithServiceArgument<IClock>("clock")`

Explicit, keyed, and decorator registration:

- `Register<IPaymentGateway, StripePaymentGateway>()`
- `WithKey("stripe")`
- `Decorate<IOrderService, LoggingOrderDecorator>()`

Dependency diagnostics and architecture:

- `External<IPaymentGateway>()`
- `FrameworkProvided<IHostEnvironment>().FromProvider("Microsoft.Extensions.Hosting")`
- `ForbidDependency().FromNamespace("Shop.Application").ToNamespace("Shop.Infrastructure").AsError("message")`
- `Diagnostic("INJ401").AsWarning()`

Generated extension customization:

- `ModuleName("Shop")`
- `GeneratedMethod("AddShopServices")`
- `GeneratedNamespace("Shop.DependencyInjection")`

String arguments must be non-empty compile-time constants. Open generic filters must use `typeof(SomeGeneric<>)`. Invalid declarations produce `INJ504` diagnostics during build.

## Module Naming

A module groups generated registrations into one extension method. By default, Injectlynx generates:

```csharp
builder.Services.AddInjectlynxServices();
```

The generated extension methods live in `Microsoft.Extensions.DependencyInjection`.

## Generated Method Naming

The fixed default method is `AddInjectlynxServices()`. Developers do not write this method manually; the source generator emits it during build.

```csharp
public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .FromNamespace("InjectLynxApp.Services")
            .WhereNameEndsWith("Service")
            .AsMatchingInterface()
            .WithScopedLifetime();
    }
}
```

Then call the generated method from `Program.cs`:

```csharp
builder.Services.AddInjectlynxServices();
```

Use `GeneratedMethod(...)` when the startup app references multiple libraries with Injectlynx conventions, or when the team wants a project-specific name:

```csharp
services.GeneratedMethod("AddInfrastructureServices");
```

Then call the override:

```csharp
builder.Services.AddInfrastructureServices();
```

If `GeneratedNamespace("MyApp.DependencyInjection")` is also used, add the matching namespace import in startup:

```csharp
using MyApp.DependencyInjection;
```

Generated methods are emitted during build. They are not manually implemented in the convention class.

## Development Experience Rules

- Keep conventions close to the module they configure, such as `ApplicationServiceConventions`.
- Prefer one convention class per generated extension method.
- Prefer constructor injection. Use member injection only when the dependency is optional, framework-created, or needs post-construction initialization.
- Use constants and type-safe generic APIs where possible so rename refactoring works.
- Treat `INJ504` as a design-time error: the DSL is too dynamic for compile-time generation.

## Performance Rules

Injectlynx keeps builds fast by:

- Using syntax-first filtering before semantic analysis.
- Reading only recognized fluent DSL chains rooted in `IServiceConventionBuilder`.
- Avoiding runtime execution, reflection scanning, file probing, and dynamic configuration loading.
- Producing deterministic generated source so incremental builds can skip unchanged work.
- Emitting direct Microsoft DI calls unless a factory is required for property or method injection.

## Example: Open Generic Handlers

```csharp
public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .FromNamespace("Shop.Application.Handlers")
            .AssignableToOpenGeneric(typeof(IRequestHandler<>))
            .AsImplementedInterfaces()
            .WithTransientLifetime();
    }
}
```

This registers handler implementations against their implemented request-handler interfaces without runtime scanning.

## Registration Examples

### Concrete Class Only

Use `AsSelf()` when a class has no interface, or callers intentionally resolve the concrete type.

```csharp
services
    .FromNamespace("Shop.Infrastructure.Clients")
    .WhereNameEndsWith("Client")
    .AsSelf()
    .WithSingletonLifetime();
```

Example output:

```csharp
services.AddSingleton<MetricsClient, MetricsClient>();
```

### Register All Implemented Interfaces

Use `AsImplementedInterfaces()` when one class should be available through every interface it implements.

```csharp
services
    .FromNamespace("Shop.Application.Processors")
    .WhereNameEndsWith("Processor")
    .AsImplementedInterfaces()
    .WithScopedLifetime();
```

If `OrderProcessor : IOrderProcessor, IDisposableProcessor`, both interfaces are registered.

### Register By Interface Name Prefix Or Suffix

Use interface-name filters when the class implements multiple interfaces but only a naming group should be registered.

```csharp
services
    .FromNamespace("Shop.Application.Stores")
    .WhereInterfaceNameStartsWith("IRead")
    .AsImplementedInterfaces()
    .WithTransientLifetime();
```

If `OrderStore : IReadOrderStore, IWriteOrderStore`, only `IReadOrderStore` is registered by this convention.

Suffix example:

```csharp
services
    .FromNamespace("Shop.Application.Handlers")
    .WhereInterfaceNameEndsWith("Handler")
    .AsImplementedInterfaces()
    .WithTransientLifetime();
```

### Register One Specific Interface

Use explicit registration when one implementation has several interfaces and you want exactly one contract.

```csharp
services
    .Register<IWriteOrderStore, OrderStore>()
    .WithScopedLifetime();
```

This registers `OrderStore` only as `IWriteOrderStore`. Add more `Register<TService, TImplementation>()` calls for additional specific contracts.
