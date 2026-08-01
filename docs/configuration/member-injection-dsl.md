# Method And Property Injection DSL

Injectlynx supports method and property injection with the same module-level C# DSL, as long as the configuration remains declarative and compile-time-readable. Constructor injection should remain the default because it is explicit, immutable, and easiest for Microsoft DI and Native AOT.

## Recommendation

Use constructor injection first:

```csharp
public sealed class OrderService(IClock clock) : IOrderService
{
}
```

Use method or property injection only for scenarios where constructor injection is awkward, such as optional dependencies, framework-created objects, plugin hooks, or initialization methods that must run after construction.

## Combined Setup Model

Constructor, property, and method injection can be configured in separate DSL chains. Injectlynx merges them into one activation plan per implementation type.

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();

services
    .For<OrderService>()
    .InjectProperty(static service => service.Clock)
    .InjectMethod("Initialize");
```

Execution order:

1. Construct `OrderService` using constructor injection.
2. Assign configured required properties.
3. Assign configured optional properties.
4. Invoke configured methods in declaration order.
5. Return the instance as the registered service contract.

If member injection is configured, the generator emits a factory registration. Without member injection, it keeps the faster direct registration path.

## Method Injection

Use `For<TImplementation>()` to opt in one implementation type:

```csharp
services
    .For<OrderService>()
    .InjectMethod("Initialize");
```

Typed form for refactoring safety:

```csharp
services
    .For<OrderService>()
    .InjectMethod(static service => service.Initialize(default!, default!));
```

Example target:

```csharp
public sealed class OrderService : IOrderService
{
    public void Initialize(IClock clock, ILogger<OrderService> logger)
    {
    }
}
```

Generated code resolves method parameters from `IServiceProvider`, constructs the service, calls the method once, then returns the instance.

### Method Arguments With Values

Method parameters that are not services must be configured explicitly. Injectlynx should not guess values for `string`, primitive types, enums, or `object`.

Example target:

```csharp
public void Initialize(string name, object state, IClock clock)
{
}
```

DSL:

```csharp
services
    .For<OrderService>()
    .InjectMethod("Initialize")
    .WithConstantArgument("name", "orders")
    .WithServiceArgument<object>("state")
    .WithServiceArgument<IClock>("clock");
```

Generated behavior:

```csharp
implementation.Initialize(
    "orders",
    provider.GetRequiredService<object>(),
    provider.GetRequiredService<IClock>());
```

Rules:

- `string`, numeric, `bool`, `char`, and `null` values use `WithConstantArgument(...)`.
- Complex objects should usually use `WithServiceArgument<T>()`.
- `object` must be explicit because it is too ambiguous.
- Arguments are matched by parameter name.
- Missing configured values for primitive-like parameters report `INJ504`.

## Property Injection

DSL:

```csharp
services
    .For<OrderService>()
    .InjectProperty(static service => service.Clock)
    .InjectProperty(static service => service.Logger);
```

Example target:

```csharp
public sealed class OrderService : IOrderService
{
    public IClock Clock { get; set; } = null!;

    public ILogger<OrderService>? Logger { get; set; }
}
```

Generated code creates the implementation and assigns configured public settable properties from `IServiceProvider`. Property injection is service-based; constants belong in constructors or explicit method arguments.

## Optional Dependencies

Optional member injection should be explicit:

```csharp
services
    .For<OrderService>()
    .InjectOptionalProperty(static service => service.Logger);
```

Generated code uses `GetService<T>()` for optional properties and `GetRequiredService<T>()` for required properties. Optional properties must be nullable.

## Multiple Members

Method and property injection can be combined:

```csharp
services
    .For<OrderService>()
    .InjectProperty(static service => service.Clock)
    .InjectMethod("Initialize");
```

Ordering rule:

1. Construct the implementation.
2. Assign required properties.
3. Assign optional properties.
4. Invoke configured methods in declaration order.

## Parser Rules

Allowed:

- Constant method names such as `InjectMethod("Initialize")`.
- Typed member selectors such as `InjectProperty(static x => x.Clock)`.
- Generic target selectors such as `For<OrderService>()`.
- Method/property member symbols resolved by the semantic model.
- Constant argument values for primitive-like parameters.
- Explicit service argument declarations for ambiguous types.

Rejected:

- Invoking arbitrary helper methods.
- Dynamic property names.
- Runtime values from environment variables or configuration files.
- Non-public members unless explicitly supported later.
- Multiple matching methods with the same name and incompatible signatures.
- Implicit `string`, primitive, enum, or `object` argument resolution.

## Diagnostics

Injectlynx reports `INJ504` when the DSL cannot be read deterministically or when a readable member injection setup is invalid.

Injectlynx reports diagnostics when member-injection configuration is readable but invalid, including:

- Method does not exist.
- Property does not exist.
- Property has no accessible setter.
- Member dependency cannot be resolved.
- Member injection creates a dependency cycle.
- Optional property injection targets a non-nullable property.
- Required method argument value is missing for a primitive-like or `object` parameter.
- `object` argument is not explicitly configured.
- Duplicate property or method injection is configured for the same member.
- Constructor/member lifetime rules are violated.

## Generated Code Shape

The generator emits a factory registration when member injection is configured:

```csharp
services.AddScoped<IOrderService>(provider =>
{
    var implementation = ActivatorUtilities.CreateInstance<OrderService>(provider);
    implementation.Clock = provider.GetRequiredService<IClock>();
    implementation.Initialize(provider.GetRequiredService<ILogger<OrderService>>());
    return implementation;
});
```

For Native AOT friendliness, generated code avoids runtime discovery and keeps activation explicit.

Example with constructor, property, method, and constant argument:

```csharp
services.AddScoped<IOrderService>(provider =>
{
    var implementation = new OrderService(
        provider.GetRequiredService<IRepository>());

    implementation.Clock = provider.GetRequiredService<IClock>();
    implementation.Initialize("orders", provider.GetRequiredService<object>());

    return implementation;
});
```

Generated reason comments list constructor dependencies, property injections, method injections, and configured constant values.

## Performance Guidance

- Keep direct registration generation for services without member injection.
- Use factory registrations only when a property or method must be injected.
- Resolve each dependency once per activation plan when the same service is needed multiple times.
- Keep factory bodies simple and explicit; current factory registrations use `ActivatorUtilities.CreateInstance<T>()`.
- Avoid reflection, expression compilation, dynamic invocation, or cached runtime delegates.
- Validate member metadata at compile time and emit simple generated C#.

## Limitations

- Constructor injection remains the primary pattern.
- Member injection should be opt-in per type or convention.
- Property injection can make required dependencies less obvious, so generated reason comments list every injected member.
- Method injection must be invoked exactly once by generated registration code.
- Disposal ownership must remain with Microsoft DI; injected dependencies should not be manually disposed by generated code.

## Public API Shape

```csharp
services
    .For<OrderService>()
    .InjectProperty(static service => service.Clock)
    .InjectOptionalProperty(static service => service.Logger)
    .InjectMethod("Initialize")
    .WithConstantArgument("sampleName", "minimal-api")
    .WithServiceArgument<object>("state");
```

Expression-based APIs are IDE-friendly. The generator parses expression syntax and semantic model information; it does not compile or execute expressions.
