# Troubleshooting

Use this guide when a convention does not generate the registration you expected.

## Generated Method Is Missing

Expected:

```csharp
builder.Services.AddInjectlynxServices();
```

Check:

- The project references the `Injectlynx` package.
- The convention class is in the project being built.
- The convention class is `public static`.
- The method signature is `public static void Configure(IServiceConventionBuilder services)`.
- The project builds far enough for analyzers/source generators to run.

## No Services Were Registered

Check:

- The namespace string matches the service namespace.
- The name filter matches the implementation names.
- Exclusions are not filtering out the target types.
- The registration strategy matches the service shape.

Example:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

This expects types such as `OrderService : IOrderService`.

## `INJ001`: Missing Matching Interface

`AsMatchingInterface()` expects `I{ClassName}`.

For `OrderService`, add:

```csharp
public sealed class OrderService : IOrderService
{
}
```

Or use self registration when callers resolve the concrete type:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsSelf()
    .WithScopedLifetime();
```

## `INJ004`: Missing Implemented Interfaces

`AsImplementedInterfaces()` found a class but no usable interface.

Fix by implementing the intended interface or switching to `AsSelf()`.

## `INJ002`: Ambiguous Matching Interface

`AsMatchingInterface()` found more than one interface with the expected name.

Fix by narrowing the convention, adding an interface filter, excluding the unintended type, or using an explicit registration:

```csharp
services
    .Register<IOrderService, OrderService>()
    .WithScopedLifetime();
```

## `INJ201`: Missing Dependency

A generated implementation depends on a service that Injectlynx does not generate.

Fix by adding a convention or explicit registration for the dependency, or declare it as externally provided:

```csharp
services.External<IPaymentGateway>();
```

## `INJ005`: Keyed Registration Target May Be Unsupported

Keyed Microsoft DI registrations are safest on `net8.0` or later.

Fix by targeting `net8.0` or later, removing `WithKey()`, or ensuring the consumer references compatible Microsoft.Extensions.DependencyInjection APIs.

## Decorator Diagnostics

For `INJ301`, register the decorated service contract or remove the decorator.

For `INJ302`, make the decorator implement the service contract it decorates.

For `INJ303`, add an unkeyed registration because decorators are not applied to keyed-only registrations.

For `INJ304`, avoid scoped dependencies inside decorators applied to singleton services.

## `INJ504`: Invalid DSL Declaration

The generator reads a restricted, compile-time-readable DSL shape. Avoid dynamic local variables in convention chains:

```csharp
var namespaceName = "Shop.Application.Services";

services
    .FromNamespace(namespaceName)
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

Use a compile-time constant expression:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

## Singleton Depends On Scoped Service

If a singleton depends on a scoped service, review the lifetimes:

```csharp
services
    .FromNamespace("Shop.Infrastructure.Clients")
    .WhereNameEndsWith("Client")
    .AsSelf()
    .WithSingletonLifetime();
```

Use singleton only when every dependency is safe to capture for the application lifetime.

## Inspect Generated Registrations

Enable the development report:

```bash
dotnet build -p:InjectlynxDevelopmentReport=warning
```

Example output:

```text
OrderService -> IOrderService (Scoped)
```

Disable warning mode after investigation. It is intended for local review, not normal CI output.

You can also use the CLI to build with report source enabled and inspect the generated report:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- inspect . --build
```
