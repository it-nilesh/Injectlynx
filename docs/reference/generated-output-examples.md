# Generated Output Examples

These examples show how common Injectlynx conventions map to generated Microsoft DI registrations.

The exact generated source can include helper methods or factories when member injection is configured. The examples below show the registration behavior developers should expect.

## Matching Interface

Convention:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

Service:

```csharp
public sealed class OrderService : IOrderService
{
}
```

Registration behavior:

```csharp
services.AddScoped<IOrderService, OrderService>();
```

## Implemented Interfaces

Convention:

```csharp
services
    .FromNamespace("Shop.Application.Processors")
    .WhereNameEndsWith("Processor")
    .AsImplementedInterfaces()
    .WithTransientLifetime();
```

Service:

```csharp
public sealed class OrderProcessor : IOrderProcessor, IOrderValidator
{
}
```

Registration behavior:

```csharp
services.AddTransient<IOrderProcessor, OrderProcessor>();
services.AddTransient<IOrderValidator, OrderProcessor>();
```

## Self Registration

Convention:

```csharp
services
    .FromNamespace("Shop.Infrastructure.Clients")
    .WhereNameEndsWith("Client")
    .AsSelf()
    .WithSingletonLifetime();
```

Service:

```csharp
public sealed class MetricsClient
{
}
```

Registration behavior:

```csharp
services.AddSingleton<MetricsClient, MetricsClient>();
```

## Matching Interface And Self

Convention:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterfaceAndSelf()
    .WithScopedLifetime();
```

Service:

```csharp
public sealed class OrderService : IOrderService
{
}
```

Registration behavior:

```csharp
services.AddScoped<IOrderService, OrderService>();
services.AddScoped<OrderService, OrderService>();
```

## Open Generic Handlers

Convention:

```csharp
services
    .FromNamespace("Shop.Application.Handlers")
    .AssignableToOpenGeneric(typeof(IRequestHandler<>))
    .AsImplementedInterfaces()
    .WithTransientLifetime();
```

Service:

```csharp
public sealed class GetOrderQueryHandler : IRequestHandler<GetOrderQuery>
{
}
```

Registration behavior:

```csharp
services.AddTransient<IRequestHandler<GetOrderQuery>, GetOrderQueryHandler>();
```

## Explicit Registration

Convention:

```csharp
services
    .Register<IWriteOrderStore, OrderStore>()
    .WithScopedLifetime();
```

Registration behavior:

```csharp
services.AddScoped<IWriteOrderStore, OrderStore>();
```

## Keyed Registration

Convention:

```csharp
services
    .Register<IPaymentGateway, StripePaymentGateway>()
    .WithSingletonLifetime()
    .WithKey("stripe");
```

Registration behavior:

```csharp
services.AddKeyedSingleton<IPaymentGateway, StripePaymentGateway>("stripe");
```

## Decorated Registration

Convention:

```csharp
services.Decorate<IOrderService, LoggingOrderDecorator>();
```

Registration behavior:

```csharp
services.AddScoped<IOrderService>(serviceProvider =>
{
    var implementation = ActivatorUtilities.CreateInstance<OrderService>(serviceProvider);
    return ActivatorUtilities.CreateInstance<LoggingOrderDecorator>(serviceProvider, implementation);
});
```

## Member Injection

Convention:

```csharp
services
    .For<OrderService>()
    .InjectProperty(static service => service.Clock)
    .InjectMethod("Initialize")
    .WithConstantArgument("name", "orders")
    .WithServiceArgument<object>("state");
```

Registration behavior:

```csharp
services.AddScoped<IOrderService>(serviceProvider =>
{
    var implementation = ActivatorUtilities.CreateInstance<OrderService>(serviceProvider);
    implementation.Clock = serviceProvider.GetRequiredService<IClock>();
    implementation.Initialize("orders", serviceProvider.GetRequiredService<object>());
    return implementation;
});
```

## Custom Generated Method

Convention:

```csharp
services
    .GeneratedMethod("AddInfrastructureServices")
    .GeneratedNamespace("Shop.Infrastructure.DependencyInjection");
```

Startup:

```csharp
using Shop.Infrastructure.DependencyInjection;

builder.Services.AddInfrastructureServices();
```

Registration behavior:

```csharp
public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
{
    // Generated registrations.
    return services;
}
```
