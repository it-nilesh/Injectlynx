using Injectlynx.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Injectlynx.Generator.Tests;

public sealed class InjectlynxIncrementalGeneratorTests
{
    [Fact]
    public void Generator_ReadsStronglyTypedConventionDsl()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public sealed class OrderService : IOrderService
        {
        }
        """;

        var result = RunGenerator(source);
        var generated = Assert.Single(result.GeneratedTrees);
        var text = generated.GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("public static class InjectlynxApplicationServiceCollectionExtensions", text);
        Assert.Contains("AddInjectlynxServices", text);
        Assert.Contains("// Injectlynx registration", text);
        Assert.Contains("// - Class name ends with Service.", text);
        Assert.Contains("// - Registration strategy is MatchingInterface.", text);
        Assert.Contains("AddScoped<global::Shop.Application.Services.IOrderService, global::Shop.Application.Services.OrderService>(services);", text);
    }

    [Fact]
    public void Generator_MatchesBasicRegistrationSnapshot()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public sealed class OrderService : IOrderService
        {
        }
        """;

        var result = RunGenerator(source);
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();
        var snapshot = File.ReadAllText(GetSnapshotPath("ApplicationBasic.g.cs.txt"));

        Assert.Equal(NormalizeLineEndings(snapshot), NormalizeLineEndings(text));
    }

    [Fact]
    public void Generator_ReadsOpenGenericAssignableConventionDsl()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Handlers;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Application.Handlers")
                    .AssignableToOpenGeneric(typeof(IRequestHandler<>))
                    .AsImplementedInterfaces()
                    .WithTransientLifetime();
            }
        }

        public interface IRequestHandler<TRequest> { }

        public sealed class SubmitOrder { }

        public sealed class SubmitOrderHandler : IRequestHandler<SubmitOrder>
        {
        }

        public sealed class IgnoredHandler
        {
        }
        """;

        var result = RunGenerator(source);
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddTransient<global::Shop.Application.Handlers.IRequestHandler<global::Shop.Application.Handlers.SubmitOrder>, global::Shop.Application.Handlers.SubmitOrderHandler>(services);", text);
        Assert.DoesNotContain("IgnoredHandler", text);
        Assert.Contains("// - Type is assignable to open generic global::Shop.Application.Handlers.IRequestHandler<>.", text);
    }

    [Fact]
    public void Generator_ReportsConventionDslNonConstantNamespace()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                var namespaceName = "Shop.Application.Services";

                services
                    .FromNamespace(namespaceName)
                    .WhereNameEndsWith("Service")
                    .AsMatchingInterface()
                    .WithScopedLifetime();
            }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ504", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("FromNamespace requires a non-empty constant string argument", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsMissingMatchingInterface()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public sealed class OrderService
        {
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Application", diagnostic.Properties["Injectlynx.Module"]);
        Assert.Equal("Shop.Application.Services", diagnostic.Properties["Injectlynx.Convention.Namespace"]);
        Assert.Equal("Service", diagnostic.Properties["Injectlynx.Convention.ClassSuffix"]);
    }

    [Fact]
    public void Generator_ReportsMissingImplementedInterfaces()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Processor")
                    .AsImplementedInterfaces()
                    .WithScopedLifetime();
            }
        }

        public sealed class OrderProcessor
        {
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Application", diagnostic.Properties["Injectlynx.Module"]);
        Assert.Equal("Shop.Application.Services", diagnostic.Properties["Injectlynx.Convention.Namespace"]);
        Assert.Equal("Processor", diagnostic.Properties["Injectlynx.Convention.ClassSuffix"]);
    }

    [Fact]
    public void Generator_ReportsMissingImplementedInterfacesAfterInterfaceFilter()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Store")
                    .WhereInterfaceNameStartsWith("IRead")
                    .AsImplementedInterfaces()
                    .WithTransientLifetime();
            }
        }

        public interface IWriteOrderStore { }

        public sealed class OrderStore : IWriteOrderStore
        {
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ004", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("IRead", diagnostic.Properties["Injectlynx.Convention.InterfacePrefix"]);
    }

    [Fact]
    public void Generator_ReportsDuplicateContractRegistrations()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Payments;

        public static class InfrastructureConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Payments")
                    .WhereNameEndsWith("Gateway")
                    .AsImplementedInterfaces()
                    .WithScopedLifetime();
            }
        }

        public interface IPaymentGateway { }

        public sealed class StripeGateway : IPaymentGateway
        {
        }

        public sealed class PaypalGateway : IPaymentGateway
        {
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Fix by narrowing conventions", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsAmbiguousMatchingInterfaceWithSuggestedFix()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services
        {
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

            public sealed class OrderService : Contracts.A.IOrderService, Contracts.B.IOrderService
            {
            }
        }

        namespace Shop.Application.Services.Contracts.A
        {
            public interface IOrderService { }
        }

        namespace Shop.Application.Services.Contracts.B
        {
            public interface IOrderService { }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Fix by narrowing the convention", diagnostic.GetMessage());
        Assert.Contains("global::Shop.Application.Services.Contracts.A.IOrderService", diagnostic.Properties["Injectlynx.AmbiguousContracts"]);
    }

    [Fact]
    public void Generator_ReportsKeyedRegistrationForUnsupportedTargetFramework()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .Register<IOrderService, OrderService>()
                    .WithScopedLifetime()
                    .WithKey("orders");
            }
        }

        public interface IOrderService { }

        public sealed class OrderService : IOrderService
        {
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source, targetFramework: "net7.0").Diagnostics);

        Assert.Equal("INJ005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Target net8.0 or later", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsAmbiguousConstructors()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public sealed class OrderService : IOrderService
        {
            public OrderService() { }

            public OrderService(object state) { }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ102", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Fix by keeping one public constructor", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsMissingConstructorDependencyWithSuggestedFix()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public interface IPaymentGateway { }

        public sealed class OrderService(IPaymentGateway gateway) : IOrderService
        {
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ201", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("External<TService>()", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsCircularDependency()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public interface IInvoiceService { }

        public sealed class OrderService(IInvoiceService invoices) : IOrderService { }

        public sealed class InvoiceService(IOrderService orders) : IInvoiceService { }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics.Where(static item => item.Id == "INJ202"));

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Fix by breaking the constructor dependency cycle", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsSelfDependency()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public sealed class OrderService(IOrderService inner) : IOrderService { }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics.Where(static item => item.Id == "INJ203"));

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("self-referential", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsCaptiveDependency()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services
        {
            public static class ApplicationServiceConventions
            {
                public static void Configure(IServiceConventionBuilder services)
                {
                    services
                        .FromNamespace("Shop.Application.Services")
                        .WhereNameEndsWith("Service")
                        .AsMatchingInterface()
                        .WithSingletonLifetime();

                    services
                        .FromNamespace("Shop.Application.Scoped")
                        .WhereNameEndsWith("Context")
                        .AsMatchingInterface()
                        .WithScopedLifetime();
                }
            }

            public interface IOrderService { }

            public sealed class OrderService(Shop.Application.Scoped.IRequestContext context) : IOrderService { }
        }

        namespace Shop.Application.Scoped
        {
            public interface IRequestContext { }

            public sealed class RequestContext : IRequestContext { }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics.Where(static item => item.Id == "INJ210"));

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Fix by making", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsMissingDecoratorTarget()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services.Decorate<IOrderService, LoggingOrderDecorator>();
            }
        }

        public interface IOrderService { }

        public sealed class LoggingOrderDecorator : IOrderService { }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ301", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Fix by registering", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsInvalidDecoratorContract()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Service")
                    .AsMatchingInterface()
                    .WithScopedLifetime();

                services.Decorate<IOrderService, LoggingOrderDecorator>();
            }
        }

        public interface IOrderService { }

        public sealed class OrderService : IOrderService { }

        public sealed class LoggingOrderDecorator { }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ302", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Fix by implementing", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsDecoratorTargetingOnlyKeyedRegistrations()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .Register<IOrderService, OrderService>()
                    .WithScopedLifetime()
                    .WithKey("orders");

                services.Decorate<IOrderService, LoggingOrderDecorator>();
            }
        }

        public interface IOrderService { }

        public sealed class OrderService : IOrderService { }

        public sealed class LoggingOrderDecorator : IOrderService { }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ303", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("only keyed registrations", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsDecoratorCaptiveDependency()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services
        {
            public static class ApplicationServiceConventions
            {
                public static void Configure(IServiceConventionBuilder services)
                {
                    services
                        .FromNamespace("Shop.Application.Services")
                        .WhereNameEndsWith("Service")
                        .AsMatchingInterface()
                        .WithSingletonLifetime();

                    services
                        .FromNamespace("Shop.Application.Scoped")
                        .WhereNameEndsWith("Context")
                        .AsMatchingInterface()
                        .WithScopedLifetime();

                    services.Decorate<IOrderService, LoggingOrderDecorator>();
                }
            }

            public interface IOrderService { }

            public sealed class OrderService : IOrderService { }

            public sealed class LoggingOrderDecorator(IOrderService inner, Shop.Application.Scoped.IRequestContext context) : IOrderService { }
        }

        namespace Shop.Application.Scoped
        {
            public interface IRequestContext { }

            public sealed class RequestContext : IRequestContext { }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics.Where(static item => item.Id == "INJ304"));

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("depends on scoped service", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsNoPublicConstructor()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public sealed class OrderService : IOrderService
        {
            private OrderService()
            {
            }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ101", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Generator_GeneratesFactoryForPropertyAndMethodInjection()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Service")
                    .AsMatchingInterface()
                    .WithScopedLifetime();

                services
                    .For<OrderService>()
                    .InjectProperty(static service => service.Clock)
                    .InjectMethod("Initialize")
                    .WithConstantArgument("name", "orders")
                    .WithServiceArgument<object>("state");
            }
        }

        public interface IClock { }

        public interface IOrderService { }

        public sealed class OrderService : IOrderService
        {
            public IClock Clock { get; set; } = null!;

            public void Initialize(string name, object state)
            {
            }
        }
        """;

        var result = RunGenerator(source);
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddScoped<global::Shop.Application.Services.IOrderService>(services, static serviceProvider =>", text);
        Assert.Contains("var implementation = global::Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<global::Shop.Application.Services.OrderService>(serviceProvider);", text);
        Assert.Contains("implementation.Clock = global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Shop.Application.Services.IClock>(serviceProvider);", text);
        Assert.Contains("implementation.Initialize(\"orders\", global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<object>(serviceProvider));", text);
        Assert.Contains("// - Property injection: Clock from global::Shop.Application.Services.IClock.", text);
        Assert.Contains("// - Method injection: Initialize.", text);
    }

    [Fact]
    public void Generator_ReportsMethodInjectionMissingPrimitiveArgument()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Service")
                    .AsMatchingInterface()
                    .WithScopedLifetime();

                services
                    .For<OrderService>()
                    .InjectMethod("Initialize");
            }
        }

        public interface IOrderService { }

        public sealed class OrderService : IOrderService
        {
            public void Initialize(string name)
            {
            }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ504", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Method argument name on Initialize requires an explicit constant or service argument", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReportsOptionalPropertyInjectionForNonNullableProperty()
    {
        const string source = """
        #nullable enable
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Service")
                    .AsMatchingInterface()
                    .WithScopedLifetime();

                services
                    .For<OrderService>()
                    .InjectOptionalProperty(static service => service.Clock);
            }
        }

        public interface IClock { }

        public interface IOrderService { }

        public sealed class OrderService : IOrderService
        {
            public IClock Clock { get; set; } = null!;
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ504", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Optional property Clock must be nullable", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_ReadsExclusionsExplicitKeyedDecoratorsAndModuleOptions()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .ModuleName("Shop")
                    .GeneratedMethod("AddShopServices")
                    .GeneratedNamespace("Shop.DependencyInjection");

                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Service")
                    .ExcludeNamespace("Shop.Application.Services.Internal")
                    .ExcludeType<LegacyOrderService>()
                    .ExcludeType<LoggingOrderDecorator>()
                    .AsMatchingInterface()
                    .WithScopedLifetime();

                services
                    .Register<IPaymentGateway, StripePaymentGateway>()
                    .WithSingletonLifetime()
                    .WithKey("stripe");

                services.Decorate<IOrderService, LoggingOrderDecorator>();
            }
        }

        public interface IOrderService { }

        public sealed class OrderService : IOrderService { }

        public sealed class LegacyOrderService : IOrderService { }

        public sealed class LoggingOrderDecorator(IOrderService inner) : IOrderService { }

        public interface IPaymentGateway { }

        public sealed class StripePaymentGateway : IPaymentGateway { }

        namespace Internal
        {
            public sealed class InternalOrderService : IOrderService { }
        }
        """;

        var result = RunGenerator(source);
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("namespace Shop.DependencyInjection;", text);
        Assert.Contains("AddShopServices", text);
        Assert.Contains("AddKeyedSingleton<global::Shop.Application.Services.IPaymentGateway, global::Shop.Application.Services.StripePaymentGateway>(services, \"stripe\");", text);
        Assert.Contains("AddScoped<global::Shop.Application.Services.IOrderService>(services, static serviceProvider =>", text);
        Assert.Contains("LoggingOrderDecorator", text);
        Assert.DoesNotContain("LegacyOrderService>(services", text);
        Assert.DoesNotContain("InternalOrderService", text);
    }

    [Theory]
    [InlineData("Shop")]
    [InlineData("Shop.WebApi")]
    [InlineData("WorkerService")]
    public void Generator_DefaultGeneratedMethodUsesFixedInjectlynxName(string assemblyName)
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public sealed class OrderService : IOrderService { }
        """;

        var result = RunGenerator(source, assemblyName: assemblyName);
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddInjectlynxServices", text);
    }

    [Fact]
    public void Generator_ReadsExternalAndFrameworkProvidedServices()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services.External<IPaymentGateway>().WithSingletonLifetime();
                services.FrameworkProvided<IHostEnvironment>().FromProvider("Microsoft.Extensions.Hosting");

                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Service")
                    .AsMatchingInterface()
                    .WithScopedLifetime();
            }
        }

        public interface IOrderService { }

        public interface IPaymentGateway { }

        public interface IHostEnvironment { }

        public sealed class OrderService(IPaymentGateway gateway, IHostEnvironment environment) : IOrderService { }
        """;

        var result = RunGenerator(source);
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddScoped<global::Shop.Application.Services.IOrderService, global::Shop.Application.Services.OrderService>(services);", text);
        Assert.DoesNotContain("IPaymentGateway, global::", text);
        Assert.DoesNotContain("IHostEnvironment, global::", text);
    }

    [Fact]
    public void Generator_ReadsArchitectureRulesAndDiagnosticOverrides()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services
        {
            public static class ApplicationServiceConventions
            {
                public static void Configure(IServiceConventionBuilder services)
                {
                    services
                        .Diagnostic("INJ401")
                        .AsWarning();

                    services
                        .ForbidDependency()
                        .FromNamespace("Shop.Application")
                        .ToNamespace("Shop.Infrastructure")
                        .AsError("Application cannot depend on infrastructure.");

                    services
                        .FromNamespace("Shop.Application.Services")
                        .WhereNameEndsWith("Service")
                        .AsMatchingInterface()
                        .WithScopedLifetime();

                    services
                        .FromNamespace("Shop.Infrastructure")
                        .WhereNameEndsWith("Repository")
                        .AsMatchingInterface()
                        .WithScopedLifetime();
                }
            }

            public interface IOrderService { }

            public sealed class OrderService(Shop.Infrastructure.IOrderRepository repository) : IOrderService { }
        }

        namespace Shop.Infrastructure
        {
            public interface IOrderRepository { }

            public sealed class OrderRepository : IOrderRepository { }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ401", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Application cannot depend on infrastructure", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_SupportsSelfImplementedInterfacesInterfaceFiltersAndSpecificRegistration()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Client")
                    .AsSelf()
                    .WithSingletonLifetime();

                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Processor")
                    .AsImplementedInterfaces()
                    .WithScopedLifetime();

                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereInterfaceNameStartsWith("IRead")
                    .AsImplementedInterfaces()
                    .WithTransientLifetime();

                services
                    .Register<IWriteOrderStore, OrderStore>()
                    .WithScopedLifetime();
            }
        }

        public sealed class MetricsClient { }

        public interface IOrderProcessor { }

        public interface IDisposableProcessor { }

        public sealed class OrderProcessor : IOrderProcessor, IDisposableProcessor { }

        public interface IReadOrderStore { }

        public interface IWriteOrderStore { }

        public sealed class OrderStore : IReadOrderStore, IWriteOrderStore { }
        """;

        var result = RunGenerator(source);
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddSingleton<global::Shop.Application.Services.MetricsClient, global::Shop.Application.Services.MetricsClient>(services);", text);
        Assert.Contains("AddScoped<global::Shop.Application.Services.IOrderProcessor, global::Shop.Application.Services.OrderProcessor>(services);", text);
        Assert.Contains("AddScoped<global::Shop.Application.Services.IDisposableProcessor, global::Shop.Application.Services.OrderProcessor>(services);", text);
        Assert.Contains("AddTransient<global::Shop.Application.Services.IReadOrderStore, global::Shop.Application.Services.OrderStore>(services);", text);
        Assert.Contains("AddScoped<global::Shop.Application.Services.IWriteOrderStore, global::Shop.Application.Services.OrderStore>(services);", text);
    }

    [Fact]
    public void Generator_ReportsDuplicateContractAcrossOverlappingConventions()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services
        {
            public static class ApplicationServiceConventions
            {
                public static void Configure(IServiceConventionBuilder services)
                {
                    services
                        .FromNamespace("Shop.Application.Services")
                        .WhereNameEndsWith("Service")
                        .AsMatchingInterface()
                        .WithScopedLifetime();

                    services
                        .FromNamespace("Shop.Application.Special")
                        .WhereNameEndsWith("Service")
                        .AsImplementedInterfaces()
                        .WithScopedLifetime();
                }
            }

            public interface IOrderService { }

            public sealed class OrderService : IOrderService { }
        }

        namespace Shop.Application.Special
        {
            public sealed class PriorityOrderService : Shop.Application.Services.IOrderService { }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("global::Shop.Application.Services.IOrderService", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_EmitsSeparateCustomMethodsForMultipleModules()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application
        {
            public static class ApplicationConventions
            {
                public static void Configure(IServiceConventionBuilder services)
                {
                    services
                        .ModuleName("Application")
                        .GeneratedMethod("AddApplicationServices")
                        .GeneratedNamespace("Shop.Application.DependencyInjection");

                    services
                        .FromNamespace("Shop.Application")
                        .WhereNameEndsWith("Service")
                        .AsMatchingInterface()
                        .WithScopedLifetime();
                }
            }

            public interface IOrderService { }

            public sealed class OrderService : IOrderService { }
        }

        namespace Shop.Infrastructure
        {
            public static class InfrastructureConventions
            {
                public static void Configure(IServiceConventionBuilder services)
                {
                    services
                        .ModuleName("Infrastructure")
                        .GeneratedMethod("AddInfrastructureServices")
                        .GeneratedNamespace("Shop.Infrastructure.DependencyInjection");

                    services
                        .FromNamespace("Shop.Infrastructure")
                        .WhereNameEndsWith("Repository")
                        .AsMatchingInterface()
                        .WithSingletonLifetime();
                }
            }

            public interface IOrderRepository { }

            public sealed class OrderRepository : IOrderRepository { }
        }
        """;

        var result = RunGenerator(source);
        var generatedTexts = result.GeneratedTrees
            .Select(static tree => tree.GetText().ToString())
            .ToArray();

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, generatedTexts.Length);
        Assert.Contains(generatedTexts, static text =>
            text.Contains("namespace Shop.Application.DependencyInjection;", StringComparison.Ordinal) &&
            text.Contains("AddApplicationServices", StringComparison.Ordinal) &&
            text.Contains("AddScoped<global::Shop.Application.IOrderService, global::Shop.Application.OrderService>(services);", StringComparison.Ordinal));
        Assert.Contains(generatedTexts, static text =>
            text.Contains("namespace Shop.Infrastructure.DependencyInjection;", StringComparison.Ordinal) &&
            text.Contains("AddInfrastructureServices", StringComparison.Ordinal) &&
            text.Contains("AddSingleton<global::Shop.Infrastructure.IOrderRepository, global::Shop.Infrastructure.OrderRepository>(services);", StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_EmitsKeyedRegistrationWithoutTargetWarningOnModernFramework()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .Register<IPaymentGateway, StripePaymentGateway>()
                    .WithSingletonLifetime()
                    .WithKey("stripe");
            }
        }

        public interface IPaymentGateway { }

        public sealed class StripePaymentGateway : IPaymentGateway { }
        """;

        var result = RunGenerator(source, targetFramework: "net8.0");
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("AddKeyedSingleton<global::Shop.Application.Services.IPaymentGateway, global::Shop.Application.Services.StripePaymentGateway>(services, \"stripe\");", text);
    }

    [Fact]
    public void Generator_AppliesDecoratorsInConfiguredOrder()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public static void Configure(IServiceConventionBuilder services)
            {
                services
                    .FromNamespace("Shop.Application.Services")
                    .WhereNameEndsWith("Service")
                    .AsMatchingInterface()
                    .WithScopedLifetime();

                services.Decorate<IOrderService, LoggingOrderDecorator>();
                services.Decorate<IOrderService, MetricsOrderDecorator>();
            }
        }

        public interface IOrderService { }

        public sealed class OrderService : IOrderService { }

        public sealed class LoggingOrderDecorator(IOrderService inner) : IOrderService { }

        public sealed class MetricsOrderDecorator(IOrderService inner) : IOrderService { }
        """;

        var result = RunGenerator(source);
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();
        var loggingIndex = text.IndexOf("LoggingOrderDecorator", StringComparison.Ordinal);
        var metricsIndex = text.IndexOf("MetricsOrderDecorator", StringComparison.Ordinal);

        Assert.Empty(result.Diagnostics);
        Assert.True(loggingIndex >= 0);
        Assert.True(metricsIndex > loggingIndex);
        Assert.Contains("ActivatorUtilities.CreateInstance<global::Shop.Application.Services.MetricsOrderDecorator>(serviceProvider, global::Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<global::Shop.Application.Services.LoggingOrderDecorator>", text);
    }

    [Fact]
    public void Generator_ReportsArchitectureRulesAsErrorsAndIgnoresAllowedDependencies()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services
        {
            public static class ApplicationServiceConventions
            {
                public static void Configure(IServiceConventionBuilder services)
                {
                    services
                        .ForbidDependency()
                        .FromNamespace("Shop.Application")
                        .ToNamespace("Shop.Infrastructure")
                        .AsError("Application cannot depend on infrastructure.");

                    services
                        .ForbidDependency()
                        .FromNamespace("Shop.Infrastructure")
                        .ToNamespace("Shop.Application")
                        .AsError("Infrastructure cannot depend on application.");

                    services
                        .FromNamespace("Shop.Application.Services")
                        .WhereNameEndsWith("Service")
                        .AsMatchingInterface()
                        .WithScopedLifetime();

                    services
                        .FromNamespace("Shop.Infrastructure")
                        .WhereNameEndsWith("Repository")
                        .AsMatchingInterface()
                        .WithScopedLifetime();
                }
            }

            public interface IOrderService { }

            public sealed class OrderService(Shop.Infrastructure.IOrderRepository repository) : IOrderService { }
        }

        namespace Shop.Infrastructure
        {
            public interface IOrderRepository { }

            public sealed class OrderRepository : IOrderRepository { }
        }
        """;

        var diagnostic = Assert.Single(RunGenerator(source).Diagnostics);

        Assert.Equal("INJ401", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Application cannot depend on infrastructure.", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_EmitsDevelopmentReportWhenEnabled()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public sealed class OrderService : IOrderService { }
        """;

        var diagnostics = RunGenerator(source, developmentReport: true).Diagnostics;
        var diagnostic = Assert.Single(diagnostics.Where(static item => item.Id == "INJ900"));

        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Contains("OrderService -> global::Shop.Application.Services.IOrderService", diagnostic.GetMessage());
        Assert.Contains("Scoped", diagnostic.GetMessage());
    }

    [Fact]
    public void Generator_EmitsDeterministicReportSourceWhenEnabled()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public sealed class OrderService : IOrderService { }
        """;

        var result = RunGenerator(source, reportSource: true);
        var reportTree = Assert.Single(result.GeneratedTrees.Where(static tree => tree.FilePath.EndsWith("Injectlynx.Application.Report.g.cs", StringComparison.Ordinal)));
        var text = reportTree.GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.Contains("internal static class InjectlynxApplicationRegistrationReport", text);
        Assert.Contains("Injectlynx registration report for module Application", text);
        Assert.Contains("Registration count: 1", text);
        Assert.Contains("global::Shop.Application.Services.IOrderService -> global::Shop.Application.Services.OrderService", text);
        Assert.Contains("flowchart LR", text);
        Assert.Contains("module[\"\"Application module\"\"]", text);
    }

    [Fact]
    public void Generator_CanDisableRegistrationComments()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

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

        public interface IOrderService { }

        public sealed class OrderService : IOrderService { }
        """;

        var result = RunGenerator(source, registrationComments: false);
        var text = Assert.Single(result.GeneratedTrees).GetText().ToString();

        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain("// Injectlynx registration", text);
        Assert.Contains("AddScoped<global::Shop.Application.Services.IOrderService, global::Shop.Application.Services.OrderService>(services);", text);
    }

    private static GeneratorDriverRunResult RunGenerator(
        string source,
        bool developmentReport = false,
        string assemblyName = "Tests",
        bool registrationComments = true,
        bool reportSource = false,
        string? targetFramework = null)
    {
        var compilation = CreateCompilation(source, assemblyName);
        var generator = new InjectlynxIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.Single().Options,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(developmentReport, registrationComments, reportSource, targetFramework));

        return driver.RunGenerators(compilation).GetRunResult();
    }

    private static Compilation CreateCompilation(string source, string assemblyName) =>
        CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Expressions.Expression).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Injectlynx.IServiceConventionBuilder).Assembly.Location)
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n");

    private static string GetSnapshotPath(string fileName, [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "") =>
        Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots", fileName);

    private sealed class TestAnalyzerConfigOptionsProvider(bool developmentReport, bool registrationComments, bool reportSource, string? targetFramework) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestAnalyzerConfigOptions(developmentReport, registrationComments, reportSource, targetFramework);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyAnalyzerConfigOptions.Instance;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyAnalyzerConfigOptions.Instance;
    }

    private sealed class TestAnalyzerConfigOptions(bool developmentReport, bool registrationComments, bool reportSource, string? targetFramework) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (developmentReport && string.Equals(key, "build_property.InjectlynxDevelopmentReport", StringComparison.Ordinal))
            {
                value = "true";
                return true;
            }

            if (!registrationComments && string.Equals(key, "build_property.InjectlynxRegistrationComments", StringComparison.Ordinal))
            {
                value = "false";
                return true;
            }

            if (reportSource && string.Equals(key, "build_property.InjectlynxReportSource", StringComparison.Ordinal))
            {
                value = "true";
                return true;
            }

            if (targetFramework is not null && string.Equals(key, "build_property.TargetFramework", StringComparison.Ordinal))
            {
                value = targetFramework;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

    private sealed class EmptyAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        public static EmptyAnalyzerConfigOptions Instance { get; } = new();

        public override bool TryGetValue(string key, out string value)
        {
            value = string.Empty;
            return false;
        }
    }
}
