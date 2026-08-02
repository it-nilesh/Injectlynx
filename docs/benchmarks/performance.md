# Performance And Benchmarks

Injectlynx should be measured as generated Microsoft DI registration code. The runtime path is intentionally simple: the generator writes normal `IServiceCollection` extension methods, and the app calls those methods during startup.

## What To Compare

Use three comparisons when evaluating performance:

- Manual Microsoft DI registrations: the baseline for generated output.
- Injectlynx-generated Microsoft DI registrations: should behave like the same hand-written registrations.
- Runtime scanning libraries: useful for startup comparison because they inspect assemblies when the application starts.

## Runtime Registration Cost

Generated registrations should be reviewed against equivalent manual code:

```csharp
builder.Services.AddScoped<IOrderService, OrderService>();
```

Injectlynx generates the same kind of direct registration call inside the generated startup extension:

```csharp
builder.Services.AddInjectlynxServices();
```

The generated method contains deterministic `AddScoped`, `AddSingleton`, `AddTransient`, and keyed-registration calls. There is no runtime convention scan on the default path.

## Startup Behavior

For application startup benchmarks, measure:

- Cold process startup.
- Service provider construction.
- First request or first background-service activation.
- Runtime scanning setup time, when comparing against scanning libraries.

Keep the service graph identical between variants. If a runtime-scanning variant also adds decorators, keyed services, or open generics, the Injectlynx and manual variants should include the same registrations.

## Source Generator Performance

Source-generator cost belongs to build time, not runtime startup. Use the large-consumer smoke script to create a temporary project with many convention-matched services:

```bash
SERVICE_COUNT=500 TARGET_FRAMEWORK=net10.0 bash eng/validation/measure-generator-performance.sh
```

Increase `SERVICE_COUNT` for larger solution simulations:

```bash
SERVICE_COUNT=2000 TARGET_FRAMEWORK=net10.0 bash eng/validation/measure-generator-performance.sh
```

Record the machine, SDK version, service count, target framework, and whether the build was warm or cold. Compare against a manual-registration project with the same number of services if you need a strict baseline.

## Package Smoke Validation

Before publishing, validate fresh consumers from the packed NuGet package:

```bash
dotnet pack src/Injectlynx/Injectlynx.csproj -c Debug -o artifacts/packages
bash eng/validation/validate-local-package.sh
```

The smoke validation builds multiple fresh Web SDK consumers, including default and custom generated method/namespace usage, across supported modern target frameworks.
