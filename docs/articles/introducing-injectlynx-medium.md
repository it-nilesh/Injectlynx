# Introducing Injectlynx: Attribute-Free Compile-Time Dependency Injection for .NET

Dependency injection is one of the most common patterns in modern .NET applications. Most teams use `Microsoft.Extensions.DependencyInjection`, and for good reason: it is simple, built into the platform, and works well across ASP.NET Core, worker services, minimal APIs, libraries, and hosted applications.

The uncomfortable part usually starts when an application grows.

You either write registrations by hand:

```csharp
services.AddScoped<IOrderService, OrderService>();
services.AddScoped<IPaymentGateway, StripePaymentGateway>();
services.AddTransient<IRequestHandler<GetOrderQuery>, GetOrderQueryHandler>();
```

Or you introduce runtime scanning:

```csharp
services.Scan(...);
```

Manual registration is explicit, but repetitive. Runtime scanning is convenient, but it depends on reflection and can become harder to validate, trim, or reason about in Native AOT applications.

Injectlynx takes a third path: strongly typed C# conventions that are read at compile time.

## What Is Injectlynx?

Injectlynx is an attribute-free, convention-based compile-time dependency injection toolkit for .NET. It uses a Roslyn incremental source generator to emit normal `IServiceCollection` registrations during build.

There are no attributes on every service implementation class. There is no runtime assembly scanning. There is no custom container. The generated output is plain Microsoft DI registration code.

Install it with:

```bash
dotnet add package Injectlynx
```

Injectlynx supports `netstandard2.0`, `.NET 8`, `.NET 9`, and `.NET 10`.

## The Primary Developer Experience

Instead of decorating services with attributes, you create a convention module:

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

For this service:

```csharp
public interface IOrderService
{
    OrderSummary GetOrder(Guid id);
}

public sealed class OrderService : IOrderService
{
    public OrderSummary GetOrder(Guid id) => new(id, "Created");
}
```

Injectlynx generates the equivalent of:

```csharp
services.AddScoped<IOrderService, OrderService>();
```

At startup, call the generated extension method:

```csharp
builder.Services.AddInjectlynxServices();
```

The method is generated during build. You do not create it by hand.

## Why a C# DSL?

Source generators cannot safely execute arbitrary application code during compilation. That means a normal fluent API with runtime behavior is not enough.

Injectlynx uses a restricted, compile-time-readable C# DSL. The configuration still feels natural in the IDE, but the generator reads deterministic syntax and semantic information instead of running user code.

This gives developers a useful balance:

- IntelliSense while writing configuration.
- Refactoring safety for type-based registrations.
- Deterministic parsing during compilation.
- Compiler diagnostics for invalid conventions.
- Fast incremental builds.
- Good support in Visual Studio, VS Code, Rider, and CI.

## Common Registration Scenarios

Register a service by matching interface:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

Register concrete classes directly:

```csharp
services
    .FromNamespace("Shop.Infrastructure.Clients")
    .WhereNameEndsWith("Client")
    .AsSelf()
    .WithSingletonLifetime();
```

Register open generic handlers:

```csharp
services
    .FromNamespace("Shop.Application.Handlers")
    .AssignableToOpenGeneric(typeof(IRequestHandler<>))
    .AsImplementedInterfaces()
    .WithTransientLifetime();
```

Register one specific interface when a class implements several:

```csharp
services
    .Register<IWriteOrderStore, OrderStore>()
    .WithScopedLifetime();
```

Add exclusions:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .ExcludeType<LegacyOrderService>()
    .ExcludeNamespace("Shop.Application.Services.Internal")
    .AsMatchingInterface()
    .WithScopedLifetime();
```

## Custom Startup Method Names

The default generated method is:

```csharp
builder.Services.AddInjectlynxServices();
```

For a multi-project solution, you may want each library to expose a clearer method:

```csharp
services
    .ModuleName("Infrastructure")
    .GeneratedMethod("AddInfrastructureServices")
    .GeneratedNamespace("Shop.Infrastructure.DependencyInjection");
```

Then the application can call:

```csharp
using Shop.Infrastructure.DependencyInjection;

builder.Services.AddInfrastructureServices();
```

This works well when you have separate application, infrastructure, and feature libraries that each own their own registration conventions.

## Member Injection When You Need It

Constructor injection should be the default for new code. It is simple, testable, and explicit.

Injectlynx also supports property and method injection for practical edge cases: legacy classes, framework-created instances, optional dependencies, or initialization methods.

```csharp
services
    .For<OrderService>()
    .InjectProperty(static service => service.Clock)
    .InjectOptionalProperty(static service => service.Logger)
    .InjectMethod("Initialize")
    .WithConstantArgument("sampleName", "minimal-api")
    .WithServiceArgument<object>("state");
```

If a method has parameters such as `string sampleName, object state`, Injectlynx passes the configured constant for `sampleName` and resolves `object` from the service provider for `state`.

## Diagnostics You Can See During Development

When investigating what will be registered, enable the development report:

```bash
dotnet build -p:InjectlynxDevelopmentReport=true
```

To show the report directly in console output:

```bash
dotnet build -p:InjectlynxDevelopmentReport=warning
```

Example output:

```text
OrderService -> IOrderService (Scoped)
```

This is useful when reviewing conventions, checking which interfaces matched, or confirming that exclusions worked.

## Native AOT and Trimming

Native AOT changes the cost model for runtime reflection. Code that scans assemblies at startup can become harder to trim and validate.

Injectlynx avoids runtime scanning by generating registrations at compile time. The application starts with explicit registration code already compiled into the assembly.

That makes it a good fit for:

- ASP.NET Core APIs that want deterministic startup.
- Worker services.
- Minimal APIs.
- Native AOT applications.
- Libraries that want to expose project-specific registration methods.

## When Should You Use Injectlynx?

Use Injectlynx when your project follows predictable naming and namespace conventions, and you want generated Microsoft DI registrations without runtime scanning.

It is especially useful when:

- Your team wants clean service classes without DI attributes.
- You want build-time validation for missing interfaces or invalid conventions.
- You have multiple libraries and want each one to own its registrations.
- You are preparing for trimming or Native AOT.
- You want generated code that can be inspected and reasoned about.

Manual registrations are still fine for very small projects. Injectlynx becomes more valuable as the number of services grows and registration consistency matters.

## Final Thoughts

Injectlynx is built around a simple idea: dependency injection configuration should be easy for developers to write, but deterministic for the compiler to understand.

The result is a C# convention DSL that keeps service classes clean, generates fast Microsoft DI registrations, supports modern .NET targets, and gives teams useful diagnostics before the application runs.

Repository: https://github.com/it-nilesh/Injectlynx

NuGet: https://www.nuget.org/packages/Injectlynx
