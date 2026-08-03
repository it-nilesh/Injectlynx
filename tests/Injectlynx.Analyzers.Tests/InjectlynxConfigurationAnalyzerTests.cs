using System.Collections.Immutable;
using Injectlynx.Analyzers;
using Injectlynx.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Injectlynx.Analyzers.Tests;

public sealed class InjectlynxConfigurationAnalyzerTests
{
    [Fact]
    public async Task Analyzer_ReportsUnsupportedDynamicDslArgument()
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

        var diagnostic = Assert.Single(await RunAnalyzerAsync(source));

        Assert.Equal(InjectlynxConfigurationAnalyzer.UnsupportedDslArgumentId, diagnostic.Id);
        Assert.Contains("FromNamespace", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyzer_ReportsInvalidConventionSignature()
    {
        const string source = """
        using Injectlynx;

        namespace Shop.Application.Services;

        public static class ApplicationServiceConventions
        {
            public void Configure(IServiceConventionBuilder services)
            {
            }
        }
        """;

        var diagnostic = Assert.Single(await RunAnalyzerAsync(source));

        Assert.Equal(InjectlynxConfigurationAnalyzer.InvalidConventionSignatureId, diagnostic.Id);
    }

    [Fact]
    public async Task Analyzer_ReportsConstructabilityIssueForMatchingServiceShape()
    {
        const string source = """
        namespace Shop.Application.Services;

        public interface IOrderService { }

        public sealed class OrderService : IOrderService
        {
            private OrderService()
            {
            }
        }
        """;

        var diagnostic = Assert.Single(await RunAnalyzerAsync(source));

        Assert.Equal(InjectlynxConfigurationAnalyzer.ConstructabilityIssueId, diagnostic.Id);
        Assert.Contains("no public constructors", diagnostic.GetMessage());
    }

    [Fact]
    public void CodeFixProvider_AdvertisesInjectlynxFixes()
    {
        CodeFixProvider provider = new InjectlynxCodeFixProvider();

        Assert.Contains(InjectlynxConfigurationAnalyzer.UnsupportedDslArgumentId, provider.FixableDiagnosticIds);
        Assert.Contains("INJ001", provider.FixableDiagnosticIds);
        Assert.Contains("CS1061", provider.FixableDiagnosticIds);
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Injectlynx.IServiceConventionBuilder).Assembly.Location)
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new InjectlynxConfigurationAnalyzer());
        var diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToImmutableArray();
    }
}
