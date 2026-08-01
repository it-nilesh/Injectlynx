# Worker Service Sample

The Worker Service sample demonstrates generated service registration in a hosted background worker.

```bash
dotnet build samples/WorkerService/WorkerService.csproj --no-restore
```

The sample includes:

- `ApplicationServiceConventions.Configure(IServiceConventionBuilder)` with an `Application` module for `WorkerService.Services`.
- `HeartbeatService : IHeartbeatService` registered as singleton through `AsMatchingInterface().WithSingletonLifetime()`.
- `Worker` registered with `AddHostedService<Worker>()` and consuming `IHeartbeatService` through constructor injection.
- A call to the generated `builder.Services.AddInjectlynxServices()` extension method.

During source-tree development the sample references the generator output as analyzer assets and the primary `Injectlynx` project for the strongly typed DSL interfaces. Packaged consumers should use the primary `Injectlynx` NuGet package instead.
