# Common Architecture Styles

Injectlynx works best when convention modules follow the ownership boundaries already present in the solution.

## Modular Monolith

Use one convention module per feature or bounded context:

```csharp
services
    .ModuleName("Orders")
    .GeneratedMethod("AddOrdersServices")
    .GeneratedNamespace("Shop.Orders.DependencyInjection");
```

Each module can expose its own generated extension method, keeping startup code explicit while avoiding long registration lists.

## Clean Architecture

Register application services by convention and infrastructure services explicitly:

```csharp
services
    .FromNamespace("Shop.Application.Services")
    .WhereNameEndsWith("Service")
    .AsMatchingInterface()
    .WithScopedLifetime();

services
    .Register<IOrderStore, SqlOrderStore>()
    .WithScopedLifetime();
```

Use architecture rules to guard dependency direction:

```csharp
services
    .ForbidDependency()
    .FromNamespace("Shop.Application")
    .ToNamespace("Shop.Infrastructure")
    .AsError("Application must not depend on infrastructure.");
```

## Web API And Worker Pair

Shared application services can be generated once and used by both HTTP endpoints and background workers. Keep the generated extension method in the shared application assembly, then call it from each host.

## Runtime Extension Host

Use compile-time registration for the host services that are known at build time. Add dynamic plugin loading only for separately deployed integrations that cannot be known during compilation.
