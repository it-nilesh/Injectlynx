# Injectlynx Positioning

Injectlynx is a compile-time registration generator for `Microsoft.Extensions.DependencyInjection`.

It does not replace Microsoft DI, introduce a custom container, or scan assemblies at runtime. Instead, developers describe registration conventions in C#, and the source generator emits normal `IServiceCollection` registration code during build.

## What Injectlynx Optimizes For

- Clean service classes with no Injectlynx attributes.
- Deterministic Microsoft DI registrations generated at build time.
- Startup code that stays small as the application grows.
- Native AOT and trimming-friendly defaults by avoiding runtime assembly scanning.
- Diagnostics that fail during build instead of surfacing as runtime registration surprises.

## What Injectlynx Is Not

- It is not a runtime service scanner.
- It is not a custom dependency injection container.
- It is not a full object graph composer.
- It does not execute convention code during compilation.
- It does not make dynamic plugin loading part of the default compile-time path.

## Default Mental Model

Manual Microsoft DI registration:

```csharp
services.AddScoped<IOrderService, OrderService>();
services.AddScoped<IPaymentGateway, StripePaymentGateway>();
services.AddTransient<IRequestHandler<GetOrderQuery>, GetOrderQueryHandler>();
```

Injectlynx registration:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

Generated result:

```csharp
services.AddScoped<IOrderService, OrderService>();
services.AddScoped<IPaymentGateway, StripePaymentGateway>();
```

The value is not magic resolution. The value is predictable, generated Microsoft DI setup from readable project conventions.

## Best Fit

Injectlynx is a good fit when a project:

- Already uses ASP.NET Core, Minimal API, Worker Service, or another Microsoft DI-based host.
- Wants to avoid long manual registration lists.
- Wants convention-based registration without runtime scanning.
- Needs generated registration source for Native AOT-friendly applications.
- Wants build-time diagnostics for convention mistakes.

## Less Suitable Fit

Injectlynx is less suitable when a project:

- Needs runtime discovery of unknown assemblies as a primary feature.
- Wants a custom service container.
- Wants arbitrary dynamic DSL logic during compilation.
- Needs runtime plugin loading as the default registration mechanism.

