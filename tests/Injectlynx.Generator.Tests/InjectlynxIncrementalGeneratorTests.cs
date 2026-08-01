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

    private static GeneratorDriverRunResult RunGenerator(string source, bool developmentReport = false, string assemblyName = "Tests")
    {
        var compilation = CreateCompilation(source, assemblyName);
        var generator = new InjectlynxIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.Single().Options,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(developmentReport));

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

    private sealed class TestAnalyzerConfigOptionsProvider(bool developmentReport) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestAnalyzerConfigOptions(developmentReport);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyAnalyzerConfigOptions.Instance;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyAnalyzerConfigOptions.Instance;
    }

    private sealed class TestAnalyzerConfigOptions(bool developmentReport) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (developmentReport && string.Equals(key, "build_property.InjectlynxDevelopmentReport", StringComparison.Ordinal))
            {
                value = "true";
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
