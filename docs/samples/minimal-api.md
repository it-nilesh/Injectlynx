# Minimal API Sample

The Minimal API sample demonstrates the full implemented C# convention DSL in an ASP.NET Core app.

```bash
dotnet build samples/MinimalApi/MinimalApi.csproj
```

The sample includes:

- `ApplicationServiceConventions.Configure(IServiceConventionBuilder)` with an `Application` module convention.
- `OrderService : IOrderService` registered with `AsMatchingInterface().WithScopedLifetime()`.
- `ClockService : IClockService` registered by the same namespace/name convention.
- `GetOrderQueryHandler : IRequestHandler<GetOrderQuery>` registered through `AssignableToOpenGeneric(typeof(IRequestHandler<>))`.
- Required property injection with `InjectProperty(static service => service.Clock)`.
- Optional property injection with `InjectOptionalProperty(static service => service.Logger)`.
- Method injection with `InjectMethod("Initialize")`.
- Constant method arguments through `WithConstantArgument("sampleName", "minimal-api")`.
- Service method arguments through `WithServiceArgument<object>("state")`.
- A call to the generated `builder.Services.AddInjectlynxServices()` extension method.

The endpoint resolves an open-generic handler contract:

```csharp
app.MapGet("/orders/{id:guid}", async (Guid id, IRequestHandler<GetOrderQuery> handler) =>
{
    var order = await handler.HandleAsync(new GetOrderQuery(id));
    return order is null ? Results.NotFound() : Results.Ok(order);
});
```

The generated registration for `OrderService` uses a factory because member injection is configured. Direct convention-only services keep the faster direct registration path.

During source-tree development the sample references the generator output as analyzer assets, including support assemblies. Packaged consumers should use the primary `Injectlynx` NuGet package instead.
