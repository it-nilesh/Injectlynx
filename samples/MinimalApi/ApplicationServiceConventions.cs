using Injectlynx;
using MinimalApi.Handlers;
using MinimalApi.Services;

namespace MinimalApi;

public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .FromNamespace("MinimalApi.Services")
            .WhereNameEndsWith("Service")
            .AsImplementedInterfaces()
            .WithScopedLifetime();
            
        services
            .FromNamespace("MinimalApi.Handlers")
            .AssignableToOpenGeneric(typeof(IRequestHandler<>))
            .AsImplementedInterfaces()
            .WithTransientLifetime();

        services
            .For<OrderService>()
            .InjectProperty(static service => service.Clock)
            .InjectOptionalProperty(static service => service.Logger)
            .InjectMethod("Initialize")
            .WithConstantArgument("sampleName", "minimal-api")
            .WithServiceArgument<object>("state");
    }
}