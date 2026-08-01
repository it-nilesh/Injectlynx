using Injectlynx;
using Microsoft.AspNetCore.Hosting;
using WebApi.Infrastructure;
using WebApi.Services;

namespace WebApi;

public static class ApplicationServiceConventions
{
    public static void Configure(IServiceConventionBuilder services)
    {
        services
            .ModuleName("WebApi")
            .GeneratedMethod("AddWebApiServices")
            .GeneratedNamespace("WebApi.DependencyInjection");

        services
            .External<IAuditSink>()
            .WithSingletonLifetime();

        services
            .FrameworkProvided<IWebHostEnvironment>()
            .FromProvider("ASP.NET Core");

        services
            .Diagnostic("INJ401")
            .AsWarning();

        services
            .ForbidDependency()
            .FromNamespace("WebApi.Controllers")
            .ToNamespace("WebApi.Infrastructure")
            .AsError("Controllers should depend on application services, not infrastructure.");

        services
            .FromNamespace("WebApi.Services")
            .WhereNameEndsWith("Service")
            .ExcludeNamespace("WebApi.Services.Internal")
            .ExcludeType<LegacyOrderService>()
            .AsMatchingInterface()
            .WithScopedLifetime();

        services
            .Register<IOrderFormatter, CompactOrderFormatter>()
            .WithTransientLifetime();

        services
            .Register<IPaymentGateway, StripePaymentGateway>()
            .WithSingletonLifetime()
            .WithKey("stripe");

        services
            .Decorate<IOrderService, LoggingOrderDecorator>();
    }
}
