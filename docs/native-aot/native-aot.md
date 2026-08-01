# Native AOT

The Native AOT sample verifies that Injectlynx can generate direct Microsoft DI registrations without runtime assembly scanning or reflection-based service discovery on supported modern .NET targets.

```bash
dotnet build samples/NativeAot/NativeAot.csproj --no-restore
bash eng/validation/validate-native-aot.sh
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
