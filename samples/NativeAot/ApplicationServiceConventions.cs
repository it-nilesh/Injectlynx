using Injectlynx;

namespace NativeAot;

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
