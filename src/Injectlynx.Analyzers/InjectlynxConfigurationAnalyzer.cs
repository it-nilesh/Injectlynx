using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Injectlynx.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InjectlynxConfigurationAnalyzer : DiagnosticAnalyzer
{
    public const string UnsupportedDslArgumentId = "INJA001";
    public const string InvalidConventionSignatureId = "INJA002";
    public const string ConstructabilityIssueId = "INJA003";

    private static readonly DiagnosticDescriptor UnsupportedDslArgument = new(
        UnsupportedDslArgumentId,
        "Injectlynx DSL argument must be compile-time readable",
        "{0} should use a string literal, nameof expression, or constant string so Injectlynx can read it during source generation",
        "Injectlynx.Analyzers.Configuration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Injectlynx convention DSL declarations are read by Roslyn without executing user code.");

    private static readonly DiagnosticDescriptor InvalidConventionSignature = new(
        InvalidConventionSignatureId,
        "Invalid Injectlynx convention signature",
        "Injectlynx convention method should be public static void Configure(IServiceConventionBuilder services)",
        "Injectlynx.Analyzers.Configuration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Convention methods must use the shape consumed by the Injectlynx source generator.");

    private static readonly DiagnosticDescriptor ConstructabilityIssue = new(
        ConstructabilityIssueId,
        "Injectlynx service may not be constructable",
        "{0} matches the matching-interface service shape but has {1}. Keep exactly one public constructor for predictable Microsoft DI activation.",
        "Injectlynx.Analyzers.Services",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Generated Microsoft DI registrations need a predictable public constructor.");

    private static readonly ImmutableHashSet<string> StringDslMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "FromNamespace",
        "WhereNameStartsWith",
        "WhereNameEndsWith",
        "WhereInterfaceNameStartsWith",
        "WhereInterfaceNameEndsWith",
        "ExcludeNamespace",
        "GeneratedMethod",
        "GeneratedNamespace",
        "ModuleName",
        "WithKey",
        "FromProvider");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(UnsupportedDslArgument, InvalidConventionSignature, ConstructabilityIssue);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var expression = invocation.Expression;
        if (expression is not MemberAccessExpressionSyntax memberAccess ||
            !StringDslMethods.Contains(memberAccess.Name.Identifier.ValueText) ||
            invocation.ArgumentList.Arguments.Count == 0)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol is null || !IsInjectlynxDslType(symbol.ContainingType))
        {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0];
        if (IsCompileTimeReadableString(context.SemanticModel, argument.Expression, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            UnsupportedDslArgument,
            argument.GetLocation(),
            memberAccess.Name.Identifier.ValueText));
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!string.Equals(method.Name, "Configure", StringComparison.Ordinal) ||
            !method.Parameters.Any(static parameter => IsInjectlynxServiceConventionBuilder(parameter.Type)))
        {
            return;
        }

        if (method.DeclaredAccessibility == Accessibility.Public &&
            method.IsStatic &&
            method.ReturnsVoid &&
            method.Parameters.Length == 1 &&
            IsInjectlynxServiceConventionBuilder(method.Parameters[0].Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            InvalidConventionSignature,
            method.Locations.FirstOrDefault()));
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class ||
            type.IsAbstract ||
            type.IsStatic ||
            !type.Name.EndsWith("Service", StringComparison.Ordinal) ||
            !type.AllInterfaces.Any(item => string.Equals(item.Name, "I" + type.Name, StringComparison.Ordinal)))
        {
            return;
        }

        var publicConstructors = type.InstanceConstructors
            .Where(static constructor => constructor.DeclaredAccessibility == Accessibility.Public)
            .ToArray();
        if (publicConstructors.Length == 1)
        {
            return;
        }

        var issue = publicConstructors.Length == 0
            ? "no public constructors"
            : "multiple public constructors";

        context.ReportDiagnostic(Diagnostic.Create(
            ConstructabilityIssue,
            type.Locations.FirstOrDefault(),
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            issue));
    }

    private static bool IsCompileTimeReadableString(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken cancellationToken)
    {
        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is IdentifierNameSyntax identifier &&
            string.Equals(identifier.Identifier.ValueText, "nameof", StringComparison.Ordinal))
        {
            return true;
        }

        var constantValue = semanticModel.GetConstantValue(expression, cancellationToken);
        return constantValue.HasValue && constantValue.Value is string;
    }

    private static bool IsInjectlynxDslType(INamedTypeSymbol? type)
    {
        while (type is not null)
        {
            if (type.ContainingNamespace.ToDisplayString().Equals("Injectlynx", StringComparison.Ordinal) &&
                type.Name.StartsWith("IService", StringComparison.Ordinal))
            {
                return true;
            }

            type = type.BaseType;
        }

        return false;
    }

    private static bool IsInjectlynxServiceConventionBuilder(ITypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString().Equals("Injectlynx", StringComparison.Ordinal) &&
        type.Name.Equals("IServiceConventionBuilder", StringComparison.Ordinal);
}
