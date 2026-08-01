# Web API Sample

The Web API sample demonstrates generated service registrations in a conventional controller-based ASP.NET Core application, including the advanced C# DSL scenarios commonly used in production apps.

```bash
dotnet build samples/WebApi/WebApi.csproj
```

The sample includes:

- `ApplicationServiceConventions.Configure(IServiceConventionBuilder)` with customized generated namespace and method names.
- `OrderService : IOrderService` registered as scoped through namespace and suffix conventions.
- `LegacyOrderService` and `Services.Internal` excluded from the convention.
- `IOrderFormatter` explicitly registered with `CompactOrderFormatter`.
- `IPaymentGateway` registered as a keyed service with key `"stripe"`.
- `IOrderService` decorated by `LoggingOrderDecorator`.
- `IAuditSink` declared as external and registered manually in `Program.cs`.
- `IWebHostEnvironment` declared as framework-provided.
- An architecture guardrail forbidding controller dependencies on infrastructure.
- A diagnostic severity override example for `INJ401`.
- `OrdersController` resolved by ASP.NET Core MVC and consuming generated services.

The generated extension method is customized inside `ApplicationServiceConventions.Configure`:

```csharp
public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .ModuleName("WebApi")
            .GeneratedMethod("AddWebApiServices")
            .GeneratedNamespace("WebApi.DependencyInjection");

        services
            .FromNamespace("WebApi.Services")
            .WhereNameEndsWith("Service")
            .AsMatchingInterface()
            .WithScopedLifetime();
    }
}
```

Because the sample also customizes the generated namespace, `Program.cs` imports that namespace before calling the generated method:

```csharp
using WebApi.DependencyInjection;

builder.Services.AddWebApiServices();
```

The method is generated during build. Do not create `AddWebApiServices()` by hand.

The keyed registration can be consumed from MVC:

```csharp
public ActionResult<string> GetGateway([FromKeyedServices("stripe")] IPaymentGateway gateway) =>
    Ok(gateway.GetName());
```

During source-tree development the sample references the generator output as analyzer assets and the primary `Injectlynx` project for the strongly typed DSL interfaces. Packaged consumers should use the primary `Injectlynx` NuGet package instead.
