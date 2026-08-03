# Migrate From Manual Registration

This guide shows how to replace repetitive `IServiceCollection` registrations with Injectlynx conventions.

## 1. Install Injectlynx

```bash
dotnet add package Injectlynx
```

## 2. Group Existing Registrations

Start by grouping similar registrations:

```csharp
services.AddScoped<IOrderService, OrderService>();
services.AddScoped<IInvoiceService, InvoiceService>();
services.AddScoped<ICustomerService, CustomerService>();
```

These services share a pattern:

- Implementations are in the same module.
- Names end with `Service`.
- Interfaces match `I{ClassName}`.
- Lifetime is scoped.

## 3. Add A Convention Module

Create a convention class near the services it configures:

```csharp
using Injectlynx;

namespace Shop.Application;

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

## 4. Call The Generated Method

Replace the repeated registrations with:

```csharp
builder.Services.AddInjectlynxServices();
```

The method is generated during build.

## 5. Keep Special Registrations Explicit

Not every registration must become convention-based. Keep unusual registrations explicit until a clear convention appears:

```csharp
services.AddSingleton<ISystemClock>(SystemClock.Instance);
services.AddHttpClient<PaymentClient>();
```

Injectlynx works best when it removes repeated patterns, not intentional one-off code.

## 6. Use Explicit DSL For Exceptions

When a class implements multiple interfaces or does not follow naming conventions, use explicit registration:

```csharp
services
    .Register<IWriteOrderStore, OrderStore>()
    .WithScopedLifetime();
```

## 7. Build And Inspect

Build the project:

```bash
dotnet build
```

When investigating generated registrations, enable the development report:

```bash
dotnet build -p:InjectlynxDevelopmentReport=warning
```

Look for entries such as:

```text
OrderService -> IOrderService (Scoped)
```

## Migration Checklist

- [ ] Install the package.
- [ ] Identify repeated manual registration patterns.
- [ ] Create a convention module.
- [ ] Replace repeated startup registrations with the generated method call.
- [ ] Keep one-off registrations explicit.
- [ ] Build and fix diagnostics.
- [ ] Enable the development report when reviewing matches.

