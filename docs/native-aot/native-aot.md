# Native AOT

The Native AOT sample verifies that Injectlynx can generate direct Microsoft DI registrations without runtime assembly scanning or reflection-based service discovery on supported modern .NET targets.

```bash
dotnet build samples/NativeAot/NativeAot.csproj --no-restore
bash eng/validation/validate-native-aot.sh
bash eng/validation/validate-trimming.sh
```

## Configuration

The sample uses the same C# convention DSL as the web samples:

```csharp
public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .FromNamespace("NativeAot.Services")
            .WhereNameEndsWith("Service")
            .AsMatchingInterface()
            .WithSingletonLifetime();
    }
}
```

The sample is configured entirely through the C# convention DSL.

## Covered Scenarios

The Native AOT sample covers:

- Convention-based singleton services.
- Constructor dependencies between generated services.
- Direct Microsoft DI registration output without runtime assembly scanning.
- A published executable smoke run when the validation script can execute the produced binary.

## Trimming Validation

Web API and Worker Service samples are validated separately with trimming enabled:

```bash
bash eng/validation/validate-trimming.sh osx-arm64 net10.0
```

The trimming validation publishes `samples/WebApi` and `samples/WorkerService` as self-contained trimmed applications using `TrimMode=partial`. This catches obvious trim warnings in sample registration paths without making every development build pay the cost of publishing.

## Limitations

The default Injectlynx path is Native AOT-friendly because it generates explicit Microsoft DI registration calls. Some advanced patterns still have runtime tradeoffs:

- Decorators currently use `ActivatorUtilities.CreateInstance<T>()` in generated factories.
- Member injection uses generated factory code and should be kept for edge cases.
- Dynamic plugin loading is runtime assembly loading and is not part of the default Native AOT path.
- Runtime scanning libraries should be avoided when Native AOT and trimming are strict requirements.

Prefer constructor injection and direct convention or explicit registrations for Native AOT-sensitive code.

See [Dynamic Plugin Loading](../plugins/dynamic-plugin-loading.md) for the opt-in runtime model and its trimming tradeoffs.

## CI Guidance

Run normal build and test jobs first, then run Native AOT and trimming publishes on a dedicated job because they are slower and RID-specific.

Recommended CI sequence:

```bash
dotnet build Injectlynx.slnx --no-restore
dotnet test tests/Injectlynx.Generator.Tests/Injectlynx.Generator.Tests.csproj --no-restore -f net10.0
bash eng/validation/validate-native-aot.sh osx-arm64 net10.0
bash eng/validation/validate-trimming.sh osx-arm64 net10.0
```

Use the RID that matches the CI runner, such as `linux-x64` on Linux runners or `win-x64` on Windows runners.
