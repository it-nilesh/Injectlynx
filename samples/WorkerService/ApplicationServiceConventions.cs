using Injectlynx;

namespace WorkerService;

public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .FromNamespace("WorkerService.Services")
            .WhereNameEndsWith("Service")
            .AsMatchingInterface()
            .WithSingletonLifetime();
    }
}
