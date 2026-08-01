# Diagnostics

Injectlynx diagnostics are emitted by the source generator, so they appear in IDEs, `dotnet build`, and CI.

## Implemented Diagnostics

- `INJ001`: missing matching interface. Error by default.
- `INJ002`: ambiguous matching interface.
- `INJ003`: duplicate registration.
- `INJ004`: missing implemented interfaces. Error by default.
- `INJ101`: no public constructor.
- `INJ102`: ambiguous constructors.
- `INJ201`: missing dependency.
- `INJ202`: circular dependency.
- `INJ203`: self dependency.
- `INJ210`: singleton depends on scoped service.
- `INJ504`: invalid C# convention DSL declaration.
- `INJ900`: opt-in development registration report.

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
