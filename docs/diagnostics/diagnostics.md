# Diagnostics

Injectlynx diagnostics are emitted by the source generator, so they appear in IDEs, `dotnet build`, and CI.

## Implemented Diagnostics

- `INJ001`: missing matching interface. Error by default.
- `INJ002`: ambiguous matching interface.
- `INJ003`: duplicate registration.
- `INJ004`: missing implemented interfaces. Error by default.
- `INJ005`: keyed registration target may be unsupported.
- `INJ101`: no public constructor.
- `INJ102`: ambiguous constructors.
- `INJ201`: missing dependency.
- `INJ202`: circular dependency.
- `INJ203`: self dependency.
- `INJ210`: singleton depends on scoped service.
- `INJ301`: decorator target is not generated.
- `INJ302`: decorator does not implement service contract.
- `INJ303`: decorator targets only keyed registrations.
- `INJ304`: decorator captures scoped dependency.
- `INJ401`: forbidden architecture dependency.
- `INJ504`: invalid C# convention DSL declaration.
- `INJ900`: opt-in development registration report.
- `INJA001`: analyzer warning for non-constant convention DSL string arguments.
- `INJA002`: analyzer warning for invalid convention method signatures.
- `INJA003`: analyzer warning for constructability issues in matching-interface service shapes.

## Matching Interface Diagnostics

`INJ001` is reported when `AsMatchingInterface()` selects an implementation but the expected interface does not exist on that type. For `OrderService`, Injectlynx expects `IOrderService`.

Fix by restoring the matching interface:

```csharp
public sealed class OrderService : IOrderService
{
}
```

Or use self registration when the concrete class is the intended service contract:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsSelf()
    .WithScopedLifetime();
```

`INJ004` is reported when `AsImplementedInterfaces()` selects an implementation but no implemented interface is available for registration. This also applies when interface-name filters remove every implemented interface.

Fix by implementing the intended interface:

```csharp
public sealed class OrderProcessor : IOrderProcessor
{
}
```

Or change the convention to `AsSelf()` if concrete-type resolution is intended.

`INJ002` is reported when `AsMatchingInterface()` finds more than one matching interface for the same implementation. This usually happens when multiple namespaces expose the same interface name.

Fix by narrowing the convention, excluding the unintended type, adding an interface filter, or registering the intended contract explicitly:

```csharp
services
    .Register<IOrderService, OrderService>()
    .WithScopedLifetime();
```

`INJ003` is reported when more than one implementation registers the same service contract in one generated module.

Fix by narrowing conventions, excluding extra implementations, adding keys for intentional multi-registration, or keeping one explicit registration.

## Keyed Registration Diagnostics

`INJ005` is reported when a keyed registration is generated for a target framework older than `net8.0`.

Microsoft DI keyed-service APIs are safest on .NET 8 or later. Fix by targeting `net8.0` or later, removing `WithKey()`, or ensuring the consumer references compatible Microsoft.Extensions.DependencyInjection APIs.

```csharp
services
    .Register<IOrderService, OrderService>()
    .WithScopedLifetime()
    .WithKey("orders");
```

## Constructor And Dependency Diagnostics

`INJ101` is reported when a generated implementation has no public constructor.

Fix by adding one public constructor or excluding the type from the convention.

`INJ102` is reported when a generated implementation has multiple public constructors.

Fix by keeping one public constructor or making extra constructors non-public.

`INJ201` is reported when a generated implementation depends on a service contract that Injectlynx does not generate and has not been declared external or framework-provided.

Fix by adding a convention or explicit registration for the dependency:

```csharp
services
    .Register<IPaymentGateway, StripePaymentGateway>()
    .WithScopedLifetime();
```

Or declare the dependency as externally provided:

```csharp
services.External<IPaymentGateway>();
```

`INJ202` is reported when generated services form a constructor dependency cycle. Break the cycle with a different abstraction, a factory boundary, or by moving orchestration into a separate service.

`INJ203` is reported when a generated service directly depends on its own contract. This is usually a decorator-like shape; use `Decorate<TService, TDecorator>()` instead of registering the decorator as the primary implementation.

`INJ210` is reported when a singleton generated service depends on a scoped generated service.

Fix by making the singleton scoped/transient, making the dependency singleton-safe, or moving scoped work behind a proper runtime scope boundary.

## Decorator Diagnostics

`INJ301` is reported when `Decorate<TService, TDecorator>()` targets a service contract that Injectlynx does not generate.

Fix by registering the target service, changing the decorator target, or removing the decorator.

`INJ302` is reported when the decorator type does not implement the configured service contract.

Fix by implementing the decorated contract:

```csharp
public sealed class LoggingOrderDecorator(IOrderService inner) : IOrderService
{
}
```

`INJ303` is reported when a decorator targets a service contract that only has keyed generated registrations. Injectlynx applies decorators only to unkeyed registrations.

Fix by adding an unkeyed registration for that contract or removing the decorator.

`INJ304` is reported when a decorator applied to a singleton service depends on a scoped service.

Fix by making the decorated service scoped/transient or removing the scoped dependency from the decorator.

## Architecture Diagnostics

`INJ401` is reported when a generated service violates a configured architecture dependency rule.

Fix by moving the dependency behind an allowed abstraction, changing the dependency direction, or adjusting the architecture rule if the dependency is intentional.

## DSL Diagnostics

`INJ504` is reported when the generator cannot statically read a convention declaration, or when a readable member-injection declaration is invalid. Use non-empty constant strings for namespace and name filters, use `typeof(OpenGeneric<>)` for open generic filters, and configure primitive-like method arguments explicitly.

Example invalid DSL:

```csharp
var namespaceName = "Shop.Application.Services";

services
    .FromNamespace(namespaceName)
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

Use a constant expression instead:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

Example member-injection fix:

```csharp
services
    .For<OrderService>()
    .InjectMethod("Initialize")
    .WithConstantArgument("name", "orders")
    .WithServiceArgument<object>("state");
```

## Development Registration Report

Enable `INJ900` when you want to inspect which implementation registered against which service contract during development.

Project file:

```xml
<PropertyGroup>
  <InjectlynxDevelopmentReport>true</InjectlynxDevelopmentReport>
</PropertyGroup>
```

Command line:

```bash
dotnet build -p:InjectlynxDevelopmentReport=true
```

For console-visible output in normal builds, use warning mode temporarily:

```bash
dotnet build -p:InjectlynxDevelopmentReport=warning
```

Warning mode is for local investigation only. Keep it off in CI and production builds.

## Analyzer Diagnostics

`INJA001`, `INJA002`, and `INJA003` are emitted by `Injectlynx.Analyzers` in the IDE and during builds that load analyzer assemblies from the package. They are early warnings for patterns the generator may later reject or that Microsoft DI may activate unpredictably.

See [Analyzers And Code Fixes](../analyzers/analyzers.md) for details.
