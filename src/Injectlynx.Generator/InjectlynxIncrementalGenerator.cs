using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Injectlynx.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Injectlynx.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class InjectlynxIncrementalGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor ConventionDslError = new(
        "INJ504",
        "Invalid Injectlynx convention DSL",
        "{0}",
        "Injectlynx.ConfigurationDsl",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Injectlynx could not statically read a C# convention DSL declaration.");

    private static readonly DiagnosticDescriptor MissingMatchingInterface = new(
        "INJ001",
        "Missing matching interface",
        "{0} matches an Injectlynx convention but does not implement {1}",
        "Injectlynx.Registration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A service matched a MatchingInterface convention but no matching service contract was found.");

    private static readonly DiagnosticDescriptor AmbiguousContract = new(
        "INJ002",
        "Ambiguous service contract",
        "{0} matches multiple interfaces named {1}; add an explicit override or narrow the convention",
        "Injectlynx.Registration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Injectlynx found more than one matching interface for a service.");

    private static readonly DiagnosticDescriptor DuplicateRegistration = new(
        "INJ003",
        "Duplicate service registration",
        "{0} is registered by multiple implementations in module {1}",
        "Injectlynx.Registration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Multiple implementations were discovered for the same service contract in one generated module.");

    private static readonly DiagnosticDescriptor MissingImplementedInterfaces = new(
        "INJ004",
        "Missing implemented interfaces",
        "{0} matches an Injectlynx convention but no implemented interfaces match the convention",
        "Injectlynx.Registration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A service matched an ImplementedInterfaces convention but no service contracts were found.");

    private static readonly DiagnosticDescriptor NoAccessibleConstructor = new(
        "INJ101",
        "No public constructor",
        "{0} is registered by Injectlynx but has no public constructor",
        "Injectlynx.Constructors",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Microsoft DI requires a usable constructor to activate the generated implementation.");

    private static readonly DiagnosticDescriptor AmbiguousConstructor = new(
        "INJ102",
        "Ambiguous constructors",
        "{0} has multiple public constructors; keep one public constructor or make the intended constructor explicit",
        "Injectlynx.Constructors",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Injectlynx does not silently choose the constructor with the most parameters.");

    private static readonly DiagnosticDescriptor MissingDependency = new(
        "INJ201",
        "Missing dependency",
        "{0} depends on {1}, but no Injectlynx registration is generated for that dependency",
        "Injectlynx.Dependencies",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A constructor dependency does not match a generated registration or a known framework-provided service.");

    private static readonly DiagnosticDescriptor CircularDependency = new(
        "INJ202",
        "Circular dependency",
        "Circular dependency detected: {0}",
        "Injectlynx.Dependencies",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Generated services contain a constructor dependency cycle.");

    private static readonly DiagnosticDescriptor SelfDependency = new(
        "INJ203",
        "Self dependency",
        "{0} depends on itself through {1}",
        "Injectlynx.Dependencies",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A service cannot directly depend on its own service contract.");

    private static readonly DiagnosticDescriptor CaptiveDependency = new(
        "INJ210",
        "Captive scoped dependency",
        "Singleton {0} depends on scoped service {1}",
        "Injectlynx.Lifetimes",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A singleton service must not depend on a scoped service.");

    private static readonly DiagnosticDescriptor MissingDecoratorTarget = new(
        "INJ301",
        "Decorator target is not generated",
        "Decorator {0} targets {1}, but no Injectlynx registration is generated for that service contract",
        "Injectlynx.Decorators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A configured decorator must target a generated service registration.");

    private static readonly DiagnosticDescriptor InvalidDecoratorContract = new(
        "INJ302",
        "Decorator does not implement service contract",
        "Decorator {0} does not implement configured service contract {1}",
        "Injectlynx.Decorators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A decorator must implement the same service contract it decorates.");

    private static readonly DiagnosticDescriptor ForbiddenDependency = new(
        "INJ401",
        "Forbidden architecture dependency",
        "{0}",
        "Injectlynx.Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A generated service depends on a namespace forbidden by Injectlynx architecture governance rules.");

    private static readonly DiagnosticDescriptor DevelopmentReport = new(
        "INJ900",
        "Injectlynx development registration report",
        "{0}",
        "Injectlynx.Development",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Opt-in development report describing generated dependency injection registrations.");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax declaration && MightBeServiceCandidate(declaration),
                static (syntaxContext, cancellationToken) => CreateCandidate(syntaxContext, cancellationToken))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!);

        var conventionDsl = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax declaration && MightBeConventionDslMethod(declaration),
                static (syntaxContext, cancellationToken) => CreateConventionDslModule(syntaxContext, cancellationToken))
            .Where(static module => module is not null)
            .Select(static (module, _) => module!);

        var options = context.AnalyzerConfigOptionsProvider
            .Select(static (optionsProvider, _) => CreateGeneratorOptions(optionsProvider));

        var combined = context.CompilationProvider
            .Combine(classes.Collect())
            .Combine(conventionDsl.Collect())
            .Combine(options);

        context.RegisterSourceOutput(combined, static (sourceContext, input) =>
        {
            var (((_, candidates), conventionModules), options) = input;
            Execute(sourceContext, conventionModules, candidates, options);
        });
    }

    private static GeneratorOptions CreateGeneratorOptions(AnalyzerConfigOptionsProvider optionsProvider)
    {
        var enabled = optionsProvider.GlobalOptions.TryGetValue("build_property.InjectlynxDevelopmentReport", out var value) &&
            (IsTruthy(value) || string.Equals(value, "info", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase));
        var severity = string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase)
            ? DiagnosticSeverityModel.Warning
            : DiagnosticSeverityModel.Info;

        return new GeneratorOptions(enabled, severity);
    }

    private static bool IsTruthy(string? value) =>
        value is not null &&
        (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase));

    private static bool MightBeConventionDslMethod(MethodDeclarationSyntax declaration)
    {
        if (!string.Equals(declaration.Identifier.ValueText, "Configure", StringComparison.Ordinal) ||
            declaration.ParameterList.Parameters.Count != 1)
        {
            return false;
        }

        var parameterType = declaration.ParameterList.Parameters[0].Type?.ToString();
        return parameterType is not null &&
            parameterType.EndsWith("IServiceConventionBuilder", StringComparison.Ordinal);
    }

    private static bool MightBeServiceCandidate(ClassDeclarationSyntax declaration)
    {
        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) ||
            declaration.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return false;
        }

        return declaration.Identifier.ValueText.Length > 0;
    }

    private static ServiceCandidate? CreateCandidate(GeneratorSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var declaration = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        if (symbol.TypeKind != TypeKind.Class ||
            symbol.IsAbstract ||
            symbol.IsStatic ||
            symbol.IsImplicitlyDeclared ||
            symbol.ContainingNamespace is null)
        {
            return null;
        }

        if (symbol.DeclaredAccessibility != Accessibility.Public &&
            symbol.DeclaredAccessibility != Accessibility.Internal)
        {
            return null;
        }

        var namespaceName = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);

        var interfaces = symbol.Interfaces
            .Select(item => new InterfaceCandidate(
                FormatImplementedInterfaceForGeneratedCode(item, symbol),
                FormatOpenGenericDefinitionForGeneratedCode(item),
                item.Name,
                item.TypeArguments.Length))
            .OrderBy(static item => item.FullyQualifiedName, StringComparer.Ordinal)
            .ToImmutableArray();

        var constructors = symbol.Constructors
            .Where(static item => !item.IsStatic)
            .Select(static item => new ConstructorCandidate(
                item.DeclaredAccessibility == Accessibility.Public,
                item.Parameters
                    .Select(static parameter => parameter.Type is INamedTypeSymbol namedType
                        ? FormatTypeForGeneratedCode(namedType)
                        : parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .OrderBy(static item => item, StringComparer.Ordinal)
                    .ToImmutableArray(),
                item.Locations.FirstOrDefault()))
            .ToImmutableArray();

        var properties = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static item =>
                item.DeclaredAccessibility == Accessibility.Public &&
                item.SetMethod is not null &&
                item.SetMethod.DeclaredAccessibility == Accessibility.Public)
            .Select(static item => new PropertyCandidate(
                item.Name,
                item.Type is INamedTypeSymbol namedType
                    ? FormatTypeForGeneratedCode(namedType)
                    : item.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                item.NullableAnnotation == NullableAnnotation.Annotated,
                item.Locations.FirstOrDefault()))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        var methods = symbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static item =>
                item.MethodKind == MethodKind.Ordinary &&
                item.DeclaredAccessibility == Accessibility.Public &&
                !item.IsStatic)
            .Select(static item => new MethodCandidate(
                item.Name,
                item.Parameters
                    .Select(static parameter => new ParameterCandidate(
                        parameter.Name,
                        parameter.Type is INamedTypeSymbol namedType
                            ? FormatTypeForGeneratedCode(namedType)
                            : parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        parameter.NullableAnnotation == NullableAnnotation.Annotated))
                    .ToImmutableArray(),
                item.Locations.FirstOrDefault()))
            .OrderBy(static item => item.Name, StringComparer.Ordinal)
            .ThenBy(static item => item.Parameters.Length)
            .ToImmutableArray();

        return new ServiceCandidate(
            namespaceName,
            symbol.Name,
            FormatTypeForGeneratedCode(symbol),
            symbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
            interfaces,
            symbol.TypeParameters.Length,
            constructors,
            properties,
            methods,
            symbol.Locations.FirstOrDefault());
    }

    private static DslModuleInput? CreateConventionDslModule(GeneratorSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Parent is not ClassDeclarationSyntax containingClass)
        {
            return null;
        }

        var inferredModuleName = containingClass.Identifier.ValueText;
        if (inferredModuleName.EndsWith("ServiceConventions", StringComparison.Ordinal))
        {
            inferredModuleName = inferredModuleName.Substring(0, inferredModuleName.Length - "ServiceConventions".Length);
        }
        else if (inferredModuleName.EndsWith("Conventions", StringComparison.Ordinal))
        {
            inferredModuleName = inferredModuleName.Substring(0, inferredModuleName.Length - "Conventions".Length);
        }

        if (string.IsNullOrWhiteSpace(inferredModuleName))
        {
            inferredModuleName = "Application";
        }

        var conventions = ImmutableArray.CreateBuilder<DslConventionInput>();
        var memberInjections = ImmutableArray.CreateBuilder<DslMemberInjectionInput>();
        var explicitRegistrations = ImmutableArray.CreateBuilder<DslExplicitRegistrationInput>();
        var externalServices = ImmutableArray.CreateBuilder<DslExternalServiceInput>();
        var frameworkServices = ImmutableArray.CreateBuilder<DslFrameworkServiceInput>();
        var decorators = ImmutableArray.CreateBuilder<DslDecoratorInput>();
        var architectureRules = ImmutableArray.CreateBuilder<DslArchitectureRuleInput>();
        var diagnosticOverrides = ImmutableArray.CreateBuilder<DslDiagnosticOverrideInput>();
        var errors = ImmutableArray.CreateBuilder<DslErrorInput>();
        var moduleName = inferredModuleName;
        string? generatedMethod = null;
        string? generatedNamespace = null;
        var decoratorOrder = 0;
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Parent is MemberAccessExpressionSyntax)
            {
                continue;
            }

            var calls = FlattenInvocationChain(invocation);
            if (calls.Length == 0)
            {
                continue;
            }

            if (string.Equals(calls[0].Name, "FromNamespace", StringComparison.Ordinal) &&
                calls.Any(static call => call.Name.StartsWith("With", StringComparison.Ordinal) && call.Name.EndsWith("Lifetime", StringComparison.Ordinal)))
            {
                var convention = CreateDslConvention(context.SemanticModel, moduleName, calls, invocation.GetLocation(), cancellationToken, errors);
                if (convention is not null)
                {
                    conventions.Add(convention);
                }

                continue;
            }

            if (CreateDslModuleOption(context.SemanticModel, calls, cancellationToken, errors, ref moduleName, ref generatedMethod, ref generatedNamespace))
            {
                continue;
            }

            if (string.Equals(calls[0].Name, "Register", StringComparison.Ordinal))
            {
                var explicitRegistration = CreateDslExplicitRegistration(context.SemanticModel, calls, invocation.GetLocation(), cancellationToken, errors);
                if (explicitRegistration is not null)
                {
                    explicitRegistrations.Add(explicitRegistration);
                }

                continue;
            }

            if (string.Equals(calls[0].Name, "External", StringComparison.Ordinal))
            {
                var externalService = CreateDslExternalService(context.SemanticModel, calls, invocation.GetLocation(), cancellationToken, errors);
                if (externalService is not null)
                {
                    externalServices.Add(externalService);
                }

                continue;
            }

            if (string.Equals(calls[0].Name, "FrameworkProvided", StringComparison.Ordinal))
            {
                var frameworkService = CreateDslFrameworkService(context.SemanticModel, calls, invocation.GetLocation(), cancellationToken, errors);
                if (frameworkService is not null)
                {
                    frameworkServices.Add(frameworkService);
                }

                continue;
            }

            if (string.Equals(calls[0].Name, "Decorate", StringComparison.Ordinal))
            {
                var decorator = CreateDslDecorator(context.SemanticModel, calls, decoratorOrder++, invocation.GetLocation(), cancellationToken, errors);
                if (decorator is not null)
                {
                    decorators.Add(decorator);
                }

                continue;
            }

            if (string.Equals(calls[0].Name, "ForbidDependency", StringComparison.Ordinal))
            {
                var rule = CreateDslArchitectureRule(context.SemanticModel, calls, invocation.GetLocation(), cancellationToken, errors);
                if (rule is not null)
                {
                    architectureRules.Add(rule);
                }

                continue;
            }

            if (string.Equals(calls[0].Name, "Diagnostic", StringComparison.Ordinal))
            {
                var diagnosticOverride = CreateDslDiagnosticOverride(context.SemanticModel, calls, invocation.GetLocation(), cancellationToken, errors);
                if (diagnosticOverride is not null)
                {
                    diagnosticOverrides.Add(diagnosticOverride);
                }

                continue;
            }

            if (string.Equals(calls[0].Name, "For", StringComparison.Ordinal))
            {
                var memberInjection = CreateDslMemberInjection(context.SemanticModel, calls, invocation.GetLocation(), cancellationToken, errors);
                if (memberInjection is not null)
                {
                    memberInjections.Add(memberInjection);
                }
            }
        }

        return conventions.Count == 0 &&
                memberInjections.Count == 0 &&
                explicitRegistrations.Count == 0 &&
                externalServices.Count == 0 &&
                frameworkServices.Count == 0 &&
                decorators.Count == 0 &&
                architectureRules.Count == 0 &&
                diagnosticOverrides.Count == 0 &&
                generatedMethod is null &&
                generatedNamespace is null &&
                string.Equals(moduleName, inferredModuleName, StringComparison.Ordinal) &&
                errors.Count == 0
            ? null
            : new DslModuleInput(
                moduleName,
                context.SemanticModel.Compilation.AssemblyName,
                generatedMethod,
                generatedNamespace,
                conventions.ToImmutable(),
                explicitRegistrations.ToImmutable(),
                decorators.ToImmutable(),
                memberInjections.ToImmutable(),
                externalServices.ToImmutable(),
                frameworkServices.ToImmutable(),
                architectureRules.ToImmutable(),
                diagnosticOverrides.ToImmutable(),
                errors.ToImmutable());
    }

    private static DslConventionInput? CreateDslConvention(
        SemanticModel semanticModel,
        string moduleName,
        ImmutableArray<DslCallInput> calls,
        Location? location,
        System.Threading.CancellationToken cancellationToken,
        ImmutableArray<DslErrorInput>.Builder errors)
    {
        string? namespaceName = null;
        string? classPrefix = null;
        string? classSuffix = null;
        string? interfacePrefix = null;
        string? interfaceSuffix = null;
        string? assignableToOpenGeneric = null;
        var excludedNamespaces = ImmutableArray.CreateBuilder<string>();
        var excludedTypes = ImmutableArray.CreateBuilder<string>();
        var lifetime = ServiceLifetimeModel.Scoped;
        var strategy = RegistrationStrategy.MatchingInterface;

        foreach (var call in calls)
        {
            switch (call.Name)
            {
                case "FromNamespace":
                    namespaceName = GetRequiredStringArgument(semanticModel, call, "FromNamespace", errors, cancellationToken);
                    break;
                case "WhereNameStartsWith":
                    classPrefix = GetRequiredStringArgument(semanticModel, call, "WhereNameStartsWith", errors, cancellationToken);
                    break;
                case "WhereNameEndsWith":
                    classSuffix = GetRequiredStringArgument(semanticModel, call, "WhereNameEndsWith", errors, cancellationToken);
                    break;
                case "WhereInterfaceNameStartsWith":
                    interfacePrefix = GetRequiredStringArgument(semanticModel, call, "WhereInterfaceNameStartsWith", errors, cancellationToken);
                    break;
                case "WhereInterfaceNameEndsWith":
                    interfaceSuffix = GetRequiredStringArgument(semanticModel, call, "WhereInterfaceNameEndsWith", errors, cancellationToken);
                    break;
                case "AssignableToOpenGeneric":
                    assignableToOpenGeneric = GetRequiredTypeOfArgument(semanticModel, call, errors, cancellationToken);
                    break;
                case "ExcludeNamespace":
                    if (GetRequiredStringArgument(semanticModel, call, "ExcludeNamespace", errors, cancellationToken) is { } excludedNamespace)
                    {
                        excludedNamespaces.Add(excludedNamespace);
                    }

                    break;
                case "ExcludeType":
                    if (call.TypeArguments.Length == 1)
                    {
                        if (GetTypeArgument(semanticModel, call.TypeArguments[0], call.Location, errors, cancellationToken) is { } excludedType)
                        {
                            excludedTypes.Add(excludedType);
                        }
                    }
                    else
                    {
                        errors.Add(new DslErrorInput("ExcludeType<TImplementation> requires exactly one type argument.", call.Location));
                    }

                    break;
                case "AsMatchingInterface":
                    strategy = RegistrationStrategy.MatchingInterface;
                    break;
                case "AsImplementedInterfaces":
                    strategy = RegistrationStrategy.ImplementedInterfaces;
                    break;
                case "AsSelf":
                    strategy = RegistrationStrategy.Self;
                    break;
                case "AsMatchingInterfaceAndSelf":
                    strategy = RegistrationStrategy.MatchingInterfaceAndSelf;
                    break;
                case "WithSingletonLifetime":
                    lifetime = ServiceLifetimeModel.Singleton;
                    break;
                case "WithScopedLifetime":
                    lifetime = ServiceLifetimeModel.Scoped;
                    break;
                case "WithTransientLifetime":
                    lifetime = ServiceLifetimeModel.Transient;
                    break;
            }
        }

        if (namespaceName is null)
        {
            return null;
        }

        if (classPrefix is null && classSuffix is null && interfacePrefix is null && interfaceSuffix is null && assignableToOpenGeneric is null)
        {
            errors.Add(new DslErrorInput("Convention DSL chains must include a class/interface name filter or AssignableToOpenGeneric.", location));
            return null;
        }

        return new DslConventionInput(
            moduleName,
            namespaceName,
            excludedNamespaces.ToImmutable(),
            excludedTypes.ToImmutable(),
            classPrefix,
            classSuffix,
            interfacePrefix,
            interfaceSuffix,
            assignableToOpenGeneric,
            lifetime,
            strategy,
            location);
    }

    private static bool CreateDslModuleOption(
        SemanticModel semanticModel,
        ImmutableArray<DslCallInput> calls,
        System.Threading.CancellationToken cancellationToken,
        ImmutableArray<DslErrorInput>.Builder errors,
        ref string moduleName,
        ref string? generatedMethod,
        ref string? generatedNamespace)
    {
        if (!calls.All(static call => call.Name is "ModuleName" or "GeneratedMethod" or "GeneratedNamespace"))
        {
            return false;
        }

        foreach (var call in calls)
        {
            switch (call.Name)
            {
                case "ModuleName":
                    if (GetRequiredStringArgument(semanticModel, call, "ModuleName", errors, cancellationToken) is { } value)
                    {
                        moduleName = value;
                    }

                    break;
                case "GeneratedMethod":
                    generatedMethod = GetRequiredStringArgument(semanticModel, call, "GeneratedMethod", errors, cancellationToken);
                    break;
                case "GeneratedNamespace":
                    generatedNamespace = GetRequiredStringArgument(semanticModel, call, "GeneratedNamespace", errors, cancellationToken);
                    break;
            }
        }

        return true;
    }

    private static DslExplicitRegistrationInput? CreateDslExplicitRegistration(
        SemanticModel semanticModel,
        ImmutableArray<DslCallInput> calls,
        Location? location,
        System.Threading.CancellationToken cancellationToken,
        ImmutableArray<DslErrorInput>.Builder errors)
    {
        if (calls[0].TypeArguments.Length != 2)
        {
            errors.Add(new DslErrorInput("Register<TService, TImplementation> requires service and implementation type arguments.", calls[0].Location));
            return null;
        }

        var contract = GetTypeArgument(semanticModel, calls[0].TypeArguments[0], calls[0].Location, errors, cancellationToken);
        var implementation = GetTypeArgument(semanticModel, calls[0].TypeArguments[1], calls[0].Location, errors, cancellationToken);
        if (contract is null || implementation is null)
        {
            return null;
        }

        var lifetime = ServiceLifetimeModel.Scoped;
        string? key = null;
        foreach (var call in calls.Skip(1))
        {
            switch (call.Name)
            {
                case "WithSingletonLifetime":
                    lifetime = ServiceLifetimeModel.Singleton;
                    break;
                case "WithScopedLifetime":
                    lifetime = ServiceLifetimeModel.Scoped;
                    break;
                case "WithTransientLifetime":
                    lifetime = ServiceLifetimeModel.Transient;
                    break;
                case "WithKey":
                    key = GetRequiredStringArgument(semanticModel, call, "WithKey", errors, cancellationToken);
                    break;
            }
        }

        return new DslExplicitRegistrationInput(contract, implementation, lifetime, key, location);
    }

    private static DslExternalServiceInput? CreateDslExternalService(
        SemanticModel semanticModel,
        ImmutableArray<DslCallInput> calls,
        Location? location,
        System.Threading.CancellationToken cancellationToken,
        ImmutableArray<DslErrorInput>.Builder errors)
    {
        if (calls[0].TypeArguments.Length != 1)
        {
            errors.Add(new DslErrorInput("External<TService> requires one service type argument.", calls[0].Location));
            return null;
        }

        var contract = GetTypeArgument(semanticModel, calls[0].TypeArguments[0], calls[0].Location, errors, cancellationToken);
        if (contract is null)
        {
            return null;
        }

        ServiceLifetimeModel? lifetime = null;
        string? key = null;
        foreach (var call in calls.Skip(1))
        {
            switch (call.Name)
            {
                case "WithSingletonLifetime":
                    lifetime = ServiceLifetimeModel.Singleton;
                    break;
                case "WithScopedLifetime":
                    lifetime = ServiceLifetimeModel.Scoped;
                    break;
                case "WithTransientLifetime":
                    lifetime = ServiceLifetimeModel.Transient;
                    break;
                case "WithKey":
                    key = GetRequiredStringArgument(semanticModel, call, "WithKey", errors, cancellationToken);
                    break;
            }
        }

        return new DslExternalServiceInput(contract, lifetime, key, location);
    }

    private static DslFrameworkServiceInput? CreateDslFrameworkService(
        SemanticModel semanticModel,
        ImmutableArray<DslCallInput> calls,
        Location? location,
        System.Threading.CancellationToken cancellationToken,
        ImmutableArray<DslErrorInput>.Builder errors)
    {
        if (calls[0].TypeArguments.Length != 1)
        {
            errors.Add(new DslErrorInput("FrameworkProvided<TService> requires one service type argument.", calls[0].Location));
            return null;
        }

        var contract = GetTypeArgument(semanticModel, calls[0].TypeArguments[0], calls[0].Location, errors, cancellationToken);
        if (contract is null)
        {
            return null;
        }

        var provider = "Framework";
        foreach (var call in calls.Skip(1))
        {
            if (string.Equals(call.Name, "FromProvider", StringComparison.Ordinal))
            {
                provider = GetRequiredStringArgument(semanticModel, call, "FromProvider", errors, cancellationToken) ?? provider;
            }
        }

        return new DslFrameworkServiceInput(contract, provider, location);
    }

    private static DslDecoratorInput? CreateDslDecorator(
        SemanticModel semanticModel,
        ImmutableArray<DslCallInput> calls,
        int defaultOrder,
        Location? location,
        System.Threading.CancellationToken cancellationToken,
        ImmutableArray<DslErrorInput>.Builder errors)
    {
        if (calls[0].TypeArguments.Length != 2)
        {
            errors.Add(new DslErrorInput("Decorate<TService, TDecorator> requires service and decorator type arguments.", calls[0].Location));
            return null;
        }

        var contract = GetTypeArgument(semanticModel, calls[0].TypeArguments[0], calls[0].Location, errors, cancellationToken);
        var decorator = GetTypeArgument(semanticModel, calls[0].TypeArguments[1], calls[0].Location, errors, cancellationToken);
        if (contract is null || decorator is null)
        {
            return null;
        }

        var order = defaultOrder;
        foreach (var call in calls.Skip(1))
        {
            if (string.Equals(call.Name, "WithOrder", StringComparison.Ordinal))
            {
                var constant = call.Arguments.Count == 1
                    ? semanticModel.GetConstantValue(call.Arguments[0].Expression, cancellationToken)
                    : default;
                if (!constant.HasValue || constant.Value is not int value)
                {
                    errors.Add(new DslErrorInput("WithOrder requires one constant integer argument.", call.Location));
                    continue;
                }

                order = value;
            }
        }

        return new DslDecoratorInput(contract, decorator, order, location);
    }

    private static DslArchitectureRuleInput? CreateDslArchitectureRule(
        SemanticModel semanticModel,
        ImmutableArray<DslCallInput> calls,
        Location? location,
        System.Threading.CancellationToken cancellationToken,
        ImmutableArray<DslErrorInput>.Builder errors)
    {
        string? fromNamespace = null;
        string? toNamespace = null;
        var severity = DiagnosticSeverityModel.Error;
        string? message = null;

        foreach (var call in calls.Skip(1))
        {
            switch (call.Name)
            {
                case "FromNamespace":
                    fromNamespace = GetRequiredStringArgument(semanticModel, call, "FromNamespace", errors, cancellationToken);
                    break;
                case "ToNamespace":
                    toNamespace = GetRequiredStringArgument(semanticModel, call, "ToNamespace", errors, cancellationToken);
                    break;
                case "AsWarning":
                    severity = DiagnosticSeverityModel.Warning;
                    message = call.Arguments.Count == 0 ? null : GetRequiredStringArgument(semanticModel, call, "AsWarning", errors, cancellationToken);
                    break;
                case "AsError":
                    severity = DiagnosticSeverityModel.Error;
                    message = call.Arguments.Count == 0 ? null : GetRequiredStringArgument(semanticModel, call, "AsError", errors, cancellationToken);
                    break;
            }
        }

        if (fromNamespace is null || toNamespace is null)
        {
            errors.Add(new DslErrorInput("ForbidDependency requires FromNamespace and ToNamespace constant string declarations.", location));
            return null;
        }

        return new DslArchitectureRuleInput(fromNamespace, toNamespace, severity, message, location);
    }

    private static DslDiagnosticOverrideInput? CreateDslDiagnosticOverride(
        SemanticModel semanticModel,
        ImmutableArray<DslCallInput> calls,
        Location? location,
        System.Threading.CancellationToken cancellationToken,
        ImmutableArray<DslErrorInput>.Builder errors)
    {
        var diagnosticId = GetRequiredStringArgument(semanticModel, calls[0], "Diagnostic", errors, cancellationToken);
        if (diagnosticId is null)
        {
            return null;
        }

        var severity = DiagnosticSeverityModel.Warning;
        foreach (var call in calls.Skip(1))
        {
            severity = call.Name switch
            {
                "AsHidden" => DiagnosticSeverityModel.Hidden,
                "AsInfo" => DiagnosticSeverityModel.Info,
                "AsWarning" => DiagnosticSeverityModel.Warning,
                "AsError" => DiagnosticSeverityModel.Error,
                _ => severity
            };
        }

        return new DslDiagnosticOverrideInput(diagnosticId, severity, location);
    }

    private static DslMemberInjectionInput? CreateDslMemberInjection(
        SemanticModel semanticModel,
        ImmutableArray<DslCallInput> calls,
        Location? location,
        System.Threading.CancellationToken cancellationToken,
        ImmutableArray<DslErrorInput>.Builder errors)
    {
        if (calls[0].TypeArguments.Length != 1)
        {
            errors.Add(new DslErrorInput("For<TImplementation> requires exactly one implementation type argument.", calls[0].Location));
            return null;
        }

        var implementation = GetTypeArgument(semanticModel, calls[0].TypeArguments[0], calls[0].Location, errors, cancellationToken);
        if (implementation is null)
        {
            return null;
        }

        var properties = ImmutableArray.CreateBuilder<DslPropertyInjectionInput>();
        var methods = ImmutableArray.CreateBuilder<DslMethodInjectionInput>();
        DslMethodInjectionInput? currentMethod = null;

        foreach (var call in calls.Skip(1))
        {
            switch (call.Name)
            {
                case "InjectProperty":
                case "InjectOptionalProperty":
                    var propertyName = GetRequiredMemberSelectorArgument(semanticModel, call, call.Name, errors, cancellationToken);
                    if (propertyName is not null)
                    {
                        properties.Add(new DslPropertyInjectionInput(
                            propertyName,
                            string.Equals(call.Name, "InjectOptionalProperty", StringComparison.Ordinal),
                            call.Location));
                    }

                    currentMethod = null;
                    break;

                case "InjectMethod":
                    var methodName = GetRequiredMethodName(semanticModel, call, errors, cancellationToken);
                    if (methodName is not null)
                    {
                        currentMethod = new DslMethodInjectionInput(methodName, ImmutableArray<DslMethodArgumentInput>.Empty, call.Location);
                        methods.Add(currentMethod);
                    }

                    break;

                case "WithConstantArgument":
                    if (currentMethod is null)
                    {
                        errors.Add(new DslErrorInput("WithConstantArgument must follow InjectMethod.", call.Location));
                        break;
                    }

                    if (CreateConstantArgument(semanticModel, call, errors, cancellationToken) is { } constantArgument)
                    {
                        currentMethod = currentMethod with { Arguments = currentMethod.Arguments.Add(constantArgument) };
                        methods[methods.Count - 1] = currentMethod;
                    }

                    break;

                case "WithServiceArgument":
                    if (currentMethod is null)
                    {
                        errors.Add(new DslErrorInput("WithServiceArgument must follow InjectMethod.", call.Location));
                        break;
                    }

                    if (CreateServiceArgument(semanticModel, call, errors, cancellationToken) is { } serviceArgument)
                    {
                        currentMethod = currentMethod with { Arguments = currentMethod.Arguments.Add(serviceArgument) };
                        methods[methods.Count - 1] = currentMethod;
                    }

                    break;
            }
        }

        if (properties.Count == 0 && methods.Count == 0)
        {
            errors.Add(new DslErrorInput("For<TImplementation> must configure at least one property or method injection.", location));
            return null;
        }

        return new DslMemberInjectionInput(implementation, properties.ToImmutable(), methods.ToImmutable());
    }

    private static DslMethodArgumentInput? CreateConstantArgument(
        SemanticModel semanticModel,
        DslCallInput call,
        ImmutableArray<DslErrorInput>.Builder errors,
        System.Threading.CancellationToken cancellationToken)
    {
        if (call.Arguments.Count != 2)
        {
            errors.Add(new DslErrorInput("WithConstantArgument requires a parameter name and constant value.", call.Location));
            return null;
        }

        var parameterName = GetConstantString(semanticModel, call.Arguments[0].Expression, cancellationToken);
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            errors.Add(new DslErrorInput("WithConstantArgument requires a non-empty constant parameter name.", call.Location));
            return null;
        }

        var value = semanticModel.GetConstantValue(call.Arguments[1].Expression, cancellationToken);
        if (!value.HasValue)
        {
            errors.Add(new DslErrorInput("WithConstantArgument requires a compile-time constant value.", call.Location));
            return null;
        }

        var valueExpression = ToConstantExpression(value.Value);
        if (valueExpression is null)
        {
                errors.Add(new DslErrorInput("WithConstantArgument supports string, numeric, bool, char, and null constants.", call.Location));
            return null;
        }

        return new DslMethodArgumentInput(
            parameterName!,
            MethodArgumentInjectionKind.Constant,
            valueExpression,
            null,
            call.Location);
    }

    private static DslMethodArgumentInput? CreateServiceArgument(
        SemanticModel semanticModel,
        DslCallInput call,
        ImmutableArray<DslErrorInput>.Builder errors,
        System.Threading.CancellationToken cancellationToken)
    {
        if (call.TypeArguments.Length != 1 || call.Arguments.Count != 1)
        {
            errors.Add(new DslErrorInput("WithServiceArgument<TService> requires a service type argument and parameter name.", call.Location));
            return null;
        }

        var parameterName = GetConstantString(semanticModel, call.Arguments[0].Expression, cancellationToken);
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            errors.Add(new DslErrorInput("WithServiceArgument requires a non-empty constant parameter name.", call.Location));
            return null;
        }

        var serviceType = GetTypeArgument(semanticModel, call.TypeArguments[0], call.Location, errors, cancellationToken);
        if (serviceType is null)
        {
            return null;
        }

        return new DslMethodArgumentInput(
            parameterName!,
            MethodArgumentInjectionKind.Service,
            null,
            serviceType,
            call.Location);
    }

    private static string? GetRequiredMethodName(
        SemanticModel semanticModel,
        DslCallInput call,
        ImmutableArray<DslErrorInput>.Builder errors,
        System.Threading.CancellationToken cancellationToken)
    {
        if (call.Arguments.Count != 1)
        {
            errors.Add(new DslErrorInput("InjectMethod requires exactly one method selector or constant method name.", call.Location));
            return null;
        }

        if (GetConstantString(semanticModel, call.Arguments[0].Expression, cancellationToken) is { Length: > 0 } methodName)
        {
            return methodName;
        }

        if (GetLambdaBody(call.Arguments[0].Expression) is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.ValueText;
        }

        errors.Add(new DslErrorInput("InjectMethod requires a non-empty constant method name or simple method selector.", call.Location));
        return null;
    }

    private static string? GetRequiredMemberSelectorArgument(
        SemanticModel semanticModel,
        DslCallInput call,
        string methodName,
        ImmutableArray<DslErrorInput>.Builder errors,
        System.Threading.CancellationToken cancellationToken)
    {
        if (call.Arguments.Count != 1)
        {
            errors.Add(new DslErrorInput(methodName + " requires exactly one property selector.", call.Location));
            return null;
        }

        if (GetConstantString(semanticModel, call.Arguments[0].Expression, cancellationToken) is { Length: > 0 } constantName)
        {
            return constantName;
        }

        var body = GetLambdaBody(call.Arguments[0].Expression);
        if (body is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.ValueText;
        }

        errors.Add(new DslErrorInput(methodName + " requires a simple property selector.", call.Location));
        return null;
    }

    private static ExpressionSyntax? GetLambdaBody(ExpressionSyntax expression) =>
        expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.Body as ExpressionSyntax,
            _ => null
        };

    private static string? GetTypeArgument(
        SemanticModel semanticModel,
        TypeSyntax typeSyntax,
        Location? location,
        ImmutableArray<DslErrorInput>.Builder errors,
        System.Threading.CancellationToken cancellationToken)
    {
        if (semanticModel.GetTypeInfo(typeSyntax, cancellationToken).Type is not INamedTypeSymbol type)
        {
            errors.Add(new DslErrorInput("DSL type argument could not be resolved.", location));
            return null;
        }

        return FormatTypeForGeneratedCode(type);
    }

    private static string? GetConstantString(SemanticModel semanticModel, ExpressionSyntax expression, System.Threading.CancellationToken cancellationToken)
    {
        var value = semanticModel.GetConstantValue(expression, cancellationToken);
        return value.HasValue && value.Value is string stringValue ? stringValue : null;
    }

    private static string? ToConstantExpression(object? value) =>
        value switch
        {
            null => "null",
            string stringValue => ToStringLiteral(stringValue),
            bool boolValue => boolValue ? "true" : "false",
            char charValue => "'" + (charValue == '\'' || charValue == '\\' ? "\\" : string.Empty) + charValue + "'",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            _ => null
        };

    private static string? GetRequiredStringArgument(
        SemanticModel semanticModel,
        DslCallInput call,
        string methodName,
        ImmutableArray<DslErrorInput>.Builder errors,
        System.Threading.CancellationToken cancellationToken)
    {
        if (call.Arguments.Count != 1)
        {
            errors.Add(new DslErrorInput(methodName + " requires exactly one constant string argument.", call.Location));
            return null;
        }

        var value = semanticModel.GetConstantValue(call.Arguments[0].Expression, cancellationToken);
        if (!value.HasValue || value.Value is not string stringValue || string.IsNullOrWhiteSpace(stringValue))
        {
            errors.Add(new DslErrorInput(methodName + " requires a non-empty constant string argument.", call.Location));
            return null;
        }

        return stringValue;
    }

    private static string? GetRequiredTypeOfArgument(
        SemanticModel semanticModel,
        DslCallInput call,
        ImmutableArray<DslErrorInput>.Builder errors,
        System.Threading.CancellationToken cancellationToken)
    {
        if (call.Arguments.Count != 1 || call.Arguments[0].Expression is not TypeOfExpressionSyntax typeOf)
        {
            errors.Add(new DslErrorInput("AssignableToOpenGeneric requires exactly one typeof(OpenGeneric<>) argument.", call.Location));
            return null;
        }

        if (semanticModel.GetTypeInfo(typeOf.Type, cancellationToken).Type is not INamedTypeSymbol type)
        {
            errors.Add(new DslErrorInput("AssignableToOpenGeneric could not resolve the supplied type.", call.Location));
            return null;
        }

        if (!type.IsUnboundGenericType && type.Arity == 0)
        {
            errors.Add(new DslErrorInput("AssignableToOpenGeneric requires an open generic type such as typeof(IRequestHandler<>).", call.Location));
            return null;
        }

        return FormatOpenGenericDefinitionForGeneratedCode(type);
    }

    private static ImmutableArray<DslCallInput> FlattenInvocationChain(InvocationExpressionSyntax invocation)
    {
        var calls = ImmutableArray.CreateBuilder<DslCallInput>();
        ExpressionSyntax? current = invocation;

        while (current is InvocationExpressionSyntax currentInvocation)
        {
            if (currentInvocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var typeArguments = memberAccess.Name is GenericNameSyntax genericName
                    ? genericName.TypeArgumentList.Arguments.ToImmutableArray()
                    : ImmutableArray<TypeSyntax>.Empty;
                calls.Add(new DslCallInput(memberAccess.Name.Identifier.ValueText, typeArguments, currentInvocation.ArgumentList.Arguments, currentInvocation.GetLocation()));
                current = memberAccess.Expression;
                continue;
            }

            break;
        }

        calls.Reverse();
        return calls.ToImmutable();
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<DslModuleInput> conventionModules,
        ImmutableArray<ServiceCandidate> candidates,
        GeneratorOptions options)
    {
        var configuration = CreateConfiguration(context, conventionModules);
        if (configuration is null)
        {
            return;
        }

        foreach (var module in configuration.Modules)
        {
            ReportDiscoveryDiagnostics(context, configuration, module, candidates, configuration.Modules);
            var registrations = DiscoverRegistrations(module, candidates, context.CancellationToken);
            ReportDecoratorDiagnostics(context, configuration, module, candidates, registrations);
            registrations = ApplyDecorators(module, registrations);
            registrations = ApplyMemberInjections(context, configuration, module, candidates, registrations);
            ReportConstructorDiagnostics(context, configuration, module, candidates, registrations);
            ReportArchitectureDiagnostics(context, configuration, candidates, registrations);
            ReportDevelopmentRegistrations(context, options, module, registrations);
            var source = RegistrationSourceWriter.Write(module, registrations);
            context.AddSource("Injectlynx." + Sanitize(module.Identity.Name) + ".g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static void ReportDevelopmentRegistrations(
        SourceProductionContext context,
        GeneratorOptions options,
        ModuleModel module,
        ImmutableArray<GeneratedRegistration> registrations)
    {
        if (!options.DevelopmentReport)
        {
            return;
        }

        foreach (var registration in registrations)
        {
            var message = "Module " +
                module.Identity.Name +
                ": " +
                registration.Implementation +
                " -> " +
                registration.Contract +
                " (" +
                registration.Lifetime +
                ")" +
                (registration.Key is null ? string.Empty : " key=" + registration.Key) +
                (registration.Decorators.Length == 0 ? string.Empty : " decorators=" + string.Join(" -> ", registration.Decorators)) +
                (registration.MemberInjection is null ? string.Empty : " member-injection=true") +
                ". " +
                registration.Reason.Summary;
            context.ReportDiagnostic(Diagnostic.Create(CreateDevelopmentReportDescriptor(options.DevelopmentReportSeverity), Location.None, message));
        }
    }

    private static DiagnosticDescriptor CreateDevelopmentReportDescriptor(DiagnosticSeverityModel severity) =>
        new(
            DevelopmentReport.Id,
            DevelopmentReport.Title.ToString(),
            DevelopmentReport.MessageFormat.ToString(),
            DevelopmentReport.Category,
            ToRoslynSeverity(severity),
            isEnabledByDefault: severity != DiagnosticSeverityModel.Hidden,
            description: DevelopmentReport.Description);

    private static InjectlynxConfiguration? CreateConfiguration(
        SourceProductionContext context,
        ImmutableArray<DslModuleInput> conventionModules)
    {
        foreach (var error in conventionModules.SelectMany(static module => module.Errors))
        {
            context.ReportDiagnostic(Diagnostic.Create(ConventionDslError, error.Location, error.Message));
        }

        if (conventionModules.Any(static module => module.Errors.Length > 0))
        {
            return null;
        }

        var modules = conventionModules
            .GroupBy(static module => module.Name, StringComparer.Ordinal)
            .Select(group =>
            {
                var identity = new ModuleIdentity(group.Key);
                var conventions = group
                    .SelectMany(static module => module.Conventions)
                    .Select(convention => new ConventionModel(
                        identity,
                        convention.Namespace,
                        convention.ExcludedNamespaces,
                        convention.ExcludedTypes,
                        convention.ClassPrefix,
                        convention.ClassSuffix,
                        convention.InterfacePrefix,
                        convention.InterfaceSuffix,
                        convention.AssignableToOpenGeneric,
                        ConfigurationDefaults.Accessibility,
                        convention.Lifetime,
                        convention.Strategy,
                        ConfigurationDefaults.ExistingRegistrationBehavior))
                    .ToImmutableArray();

                var explicitRegistrations = group
                    .SelectMany(static module => module.ExplicitRegistrations)
                    .Select(registration => new ExplicitRegistrationModel(
                        identity,
                        registration.Contract,
                        registration.Implementation,
                        registration.Lifetime,
                        registration.Key,
                        ConfigurationDefaults.ExistingRegistrationBehavior,
                        SourceReference.None))
                    .ToImmutableArray();

                var decorators = group
                    .SelectMany(static module => module.Decorators)
                    .Select(decorator => new DecoratorModel(
                        ToServiceTypeIdentity(decorator.Contract),
                        ToServiceTypeIdentity(decorator.Decorator),
                        decorator.Order,
                        SourceReference.None))
                    .ToImmutableArray();

                var memberInjections = group
                    .SelectMany(static module => module.MemberInjections)
                    .GroupBy(static item => item.Implementation, StringComparer.Ordinal)
                    .Select(memberGroup => new MemberInjectionModel(
                        memberGroup.Key,
                        memberGroup
                            .SelectMany(static item => item.Properties)
                            .Select(static item => new PropertyInjectionModel(
                                item.Name,
                                item.Optional,
                                SourceReference.None))
                            .ToImmutableArray(),
                        memberGroup
                            .SelectMany(static item => item.Methods)
                            .Select(static item => new MethodInjectionModel(
                                item.Name,
                                item.Arguments
                                    .Select(static argument => new MethodArgumentInjectionModel(
                                        argument.ParameterName,
                                        argument.Kind,
                                        argument.ValueExpression,
                                        argument.ServiceType))
                                    .ToImmutableArray(),
                                SourceReference.None))
                            .ToImmutableArray()))
                    .OrderBy(static item => item.Implementation, StringComparer.Ordinal)
                    .ToImmutableArray();

                return new ModuleModel(
                    identity,
                    group.Select(static module => module.GeneratedMethod).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ??
                        "AddInjectlynxServices",
                    group.Select(static module => module.GeneratedNamespace).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ??
                        ConfigurationDefaults.GeneratedNamespace,
                    conventions,
                    explicitRegistrations,
                    decorators,
                    memberInjections);
            })
            .OrderBy(static module => module.Identity.Name, StringComparer.Ordinal)
            .ToImmutableArray();

        var externalServices = conventionModules
            .SelectMany(static module => module.ExternalServices)
            .Select(service => new ExternalServiceModel(
                ToServiceTypeIdentity(service.Contract),
                service.Lifetime,
                service.Key is null ? null : new KeyModel(new KeyIdentity(service.Key), SourceReference.None),
                SourceReference.None))
            .ToImmutableArray();

        var frameworkServices = conventionModules
            .SelectMany(static module => module.FrameworkServices)
            .Select(service => new FrameworkProvidedServiceModel(
                ToServiceTypeIdentity(service.Contract),
                service.Provider,
                SourceReference.None))
            .ToImmutableArray();

        var architectureRules = conventionModules
            .SelectMany(static module => module.ArchitectureRules)
            .Select(rule => new ForbiddenDependencyRuleModel(
                rule.FromNamespace,
                rule.ToNamespace,
                rule.Severity,
                rule.Message,
                SourceReference.None))
            .ToImmutableArray();

        var diagnosticOverrides = conventionModules
            .SelectMany(static module => module.DiagnosticOverrides)
            .Select(diagnostic => new DiagnosticSeverityOverride(diagnostic.DiagnosticId, diagnostic.Severity))
            .ToImmutableArray();

        return modules.Length == 0
            ? null
            : new InjectlynxConfiguration(
                InjectlynxConfiguration.CurrentVersion,
                modules,
                externalServices,
                frameworkServices,
                architectureRules,
                diagnosticOverrides);
    }

    private static ServiceTypeIdentity ToServiceTypeIdentity(string generatedTypeName)
    {
        var normalized = generatedTypeName.StartsWith("global::", StringComparison.Ordinal)
            ? generatedTypeName.Substring("global::".Length)
            : generatedTypeName;
        var isGeneric = normalized.Contains("<", StringComparison.Ordinal);
        var arity = 0;
        var displayName = normalized;
        var metadataName = normalized;
        var genericMarker = normalized.IndexOf('<');
        if (genericMarker >= 0)
        {
            displayName = normalized.Substring(0, genericMarker);
            metadataName = displayName;
            var genericDefinition = normalized.Substring(genericMarker);
            arity = genericDefinition.Count(static character => character == ',') + 1;
        }

        var lastDot = metadataName.LastIndexOf('.');
        var ns = lastDot >= 0 ? metadataName.Substring(0, lastDot) : string.Empty;
        var name = lastDot >= 0 ? metadataName.Substring(lastDot + 1) : metadataName;
        return new ServiceTypeIdentity(ns, name, displayName, isGeneric && normalized.Contains("<>", StringComparison.Ordinal), arity);
    }

    private static void ReportConstructorDiagnostics(
        SourceProductionContext context,
        InjectlynxConfiguration configuration,
        ModuleModel module,
        ImmutableArray<ServiceCandidate> candidates,
        ImmutableArray<GeneratedRegistration> registrations)
    {
        var registeredImplementations = registrations
            .Select(static item => item.Implementation)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var registeredContracts = registrations
            .Select(static item => item.Contract)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var registrationByContract = registrations
            .GroupBy(static item => item.Contract, StringComparer.Ordinal)
            .ToDictionary(static item => item.Key, static item => item.First(), StringComparer.Ordinal);
        var registrationByImplementation = registrations
            .GroupBy(static item => item.Implementation, StringComparer.Ordinal)
            .ToDictionary(static item => item.Key, static item => item.First(), StringComparer.Ordinal);
        var candidateByImplementation = candidates
            .GroupBy(static item => item.FullyQualifiedMetadataName, StringComparer.Ordinal)
            .ToDictionary(static item => item.Key, static item => item.First(), StringComparer.Ordinal);
        var externallyProvidedContracts = configuration.ExternalServices
            .Select(static item => NormalizeConfiguredTypeName(item.Contract.FullName))
            .Concat(configuration.FrameworkProvidedServices.Select(static item => NormalizeConfiguredTypeName(item.Contract.FullName)))
            .ToImmutableHashSet(StringComparer.Ordinal);

        foreach (var candidate in candidates.OrderBy(static item => item.FullyQualifiedMetadataName, StringComparer.Ordinal))
        {
            if (!registeredImplementations.Contains(candidate.FullyQualifiedMetadataName))
            {
                continue;
            }

            var publicConstructors = candidate.Constructors
                .Where(static item => item.IsPublic)
                .ToImmutableArray();

            if (publicConstructors.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ApplyDiagnosticOverride(NoAccessibleConstructor, configuration),
                    candidate.Location,
                    candidate.FullyQualifiedMetadataName));
                continue;
            }

            if (publicConstructors.Length > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ApplyDiagnosticOverride(AmbiguousConstructor, configuration),
                    candidate.Location,
                    candidate.FullyQualifiedMetadataName));
                continue;
            }

            foreach (var dependency in publicConstructors[0].Dependencies)
            {
                if (IsFrameworkProvidedDependency(dependency) || externallyProvidedContracts.Contains(dependency))
                {
                    continue;
                }

                if (registrationByContract.TryGetValue(dependency, out var resolvedDependency))
                {
                    if (string.Equals(resolvedDependency.Implementation, candidate.FullyQualifiedMetadataName, StringComparison.Ordinal))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            ApplyDiagnosticOverride(SelfDependency, configuration),
                            publicConstructors[0].Location ?? candidate.Location,
                            candidate.FullyQualifiedMetadataName,
                            dependency));
                        continue;
                    }

                    if (registrationByImplementation.TryGetValue(candidate.FullyQualifiedMetadataName, out var consumerRegistration) &&
                        consumerRegistration.Lifetime == ServiceLifetimeModel.Singleton &&
                        resolvedDependency.Lifetime == ServiceLifetimeModel.Scoped)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            ApplyDiagnosticOverride(CaptiveDependency, configuration),
                            publicConstructors[0].Location ?? candidate.Location,
                            candidate.FullyQualifiedMetadataName,
                            dependency));
                    }

                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    ApplyDiagnosticOverride(MissingDependency, configuration),
                    publicConstructors[0].Location ?? candidate.Location,
                    ImmutableDictionary<string, string?>.Empty
                        .Add("Injectlynx.Consumer", candidate.FullyQualifiedMetadataName)
                        .Add("Injectlynx.Dependency", dependency),
                    candidate.FullyQualifiedMetadataName,
                    dependency));
            }
        }

        foreach (var cycle in FindCircularDependencies(registrations, candidateByImplementation, registrationByContract))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ApplyDiagnosticOverride(CircularDependency, configuration),
                cycle.Location,
                cycle.Path));
        }
    }

    private static void ReportArchitectureDiagnostics(
        SourceProductionContext context,
        InjectlynxConfiguration configuration,
        ImmutableArray<ServiceCandidate> candidates,
        ImmutableArray<GeneratedRegistration> registrations)
    {
        if (configuration.ForbiddenDependencyRules.Length == 0)
        {
            return;
        }

        var registeredImplementations = registrations
            .Select(static item => item.Implementation)
            .ToImmutableHashSet(StringComparer.Ordinal);

        foreach (var candidate in candidates.OrderBy(static item => item.FullyQualifiedMetadataName, StringComparer.Ordinal))
        {
            if (!registeredImplementations.Contains(candidate.FullyQualifiedMetadataName))
            {
                continue;
            }

            var constructor = GetSinglePublicConstructor(candidate);
            if (constructor is null)
            {
                continue;
            }

            foreach (var dependency in constructor.Dependencies)
            {
                var dependencyNamespace = GetNamespaceFromGeneratedTypeName(dependency);
                foreach (var rule in configuration.ForbiddenDependencyRules)
                {
                    if (!candidate.Namespace.StartsWith(rule.FromNamespace, StringComparison.Ordinal) ||
                        !dependencyNamespace.StartsWith(rule.ToNamespace, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(
                        ApplyDiagnosticOverride(CreateForbiddenDependencyDescriptor(rule.Severity), configuration),
                        constructor.Location ?? candidate.Location,
                        rule.Message ??
                        candidate.FullyQualifiedMetadataName + " depends on " + dependency +
                        ", which violates forbidden dependency rule " + rule.FromNamespace + " -> " + rule.ToNamespace + "."));
                }
            }
        }
    }

    private static DiagnosticDescriptor CreateForbiddenDependencyDescriptor(DiagnosticSeverityModel severity) =>
        new(
            ForbiddenDependency.Id,
            ForbiddenDependency.Title.ToString(),
            ForbiddenDependency.MessageFormat.ToString(),
            ForbiddenDependency.Category,
            ToRoslynSeverity(severity),
            isEnabledByDefault: severity != DiagnosticSeverityModel.Hidden,
            description: ForbiddenDependency.Description);

    private static DiagnosticSeverity ToRoslynSeverity(DiagnosticSeverityModel severity) =>
        severity switch
        {
            DiagnosticSeverityModel.Hidden => DiagnosticSeverity.Hidden,
            DiagnosticSeverityModel.Info => DiagnosticSeverity.Info,
            DiagnosticSeverityModel.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Error
        };

    private static DiagnosticDescriptor ApplyDiagnosticOverride(
        DiagnosticDescriptor descriptor,
        InjectlynxConfiguration configuration)
    {
        var overrideSeverity = configuration.DiagnosticSeverityOverrides
            .LastOrDefault(item => string.Equals(item.DiagnosticId, descriptor.Id, StringComparison.Ordinal));
        if (overrideSeverity is null)
        {
            return descriptor;
        }

        var severity = ToRoslynSeverity(overrideSeverity.Severity);
        return new DiagnosticDescriptor(
            descriptor.Id,
            descriptor.Title,
            descriptor.MessageFormat,
            descriptor.Category,
            severity,
            isEnabledByDefault: overrideSeverity.Severity != DiagnosticSeverityModel.Hidden,
            description: descriptor.Description,
            helpLinkUri: descriptor.HelpLinkUri,
            customTags: descriptor.CustomTags.ToArray());
    }

    private static ImmutableArray<DependencyCycle> FindCircularDependencies(
        ImmutableArray<GeneratedRegistration> registrations,
        Dictionary<string, ServiceCandidate> candidateByImplementation,
        Dictionary<string, GeneratedRegistration> registrationByContract)
    {
        var edges = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
        var locations = new Dictionary<string, Location?>(StringComparer.Ordinal);

        foreach (var registration in registrations.OrderBy(static item => item.Implementation, StringComparer.Ordinal))
        {
            if (!candidateByImplementation.TryGetValue(registration.Implementation, out var candidate))
            {
                continue;
            }

            var constructor = GetSinglePublicConstructor(candidate);
            if (constructor is null)
            {
                continue;
            }

            var dependencies = constructor.Dependencies
                .Where(registrationByContract.ContainsKey)
                .Select(dependency => registrationByContract[dependency].Implementation)
                .Where(implementation => !string.Equals(implementation, registration.Implementation, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static item => item, StringComparer.Ordinal)
                .ToImmutableArray();

            edges[registration.Implementation] = dependencies;
            locations[registration.Implementation] = constructor.Location ?? candidate.Location;
        }

        var cycles = ImmutableArray.CreateBuilder<DependencyCycle>();
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var implementation in edges.Keys.OrderBy(static item => item, StringComparer.Ordinal))
        {
            Visit(implementation, ImmutableArray<string>.Empty);
        }

        return cycles.ToImmutable();

        void Visit(string current, ImmutableArray<string> path)
        {
            var index = path.IndexOf(current);
            if (index >= 0)
            {
                var cyclePath = path.Skip(index).Concat(new[] { current }).ToImmutableArray();
                var key = CanonicalCycleKey(cyclePath);
                if (reported.Add(key))
                {
                    cycles.Add(new DependencyCycle(string.Join(" -> ", cyclePath), locations.ContainsKey(current) ? locations[current] : Location.None));
                }

                return;
            }

            if (!edges.TryGetValue(current, out var next))
            {
                return;
            }

            var newPath = path.Add(current);
            foreach (var dependency in next)
            {
                Visit(dependency, newPath);
            }
        }
    }

    private static ConstructorCandidate? GetSinglePublicConstructor(ServiceCandidate candidate)
    {
        var publicConstructors = candidate.Constructors
            .Where(static item => item.IsPublic)
            .ToImmutableArray();

        return publicConstructors.Length == 1 ? publicConstructors[0] : null;
    }

    private static string CanonicalCycleKey(ImmutableArray<string> cyclePath)
    {
        var nodes = cyclePath.Take(cyclePath.Length - 1).OrderBy(static item => item, StringComparer.Ordinal);
        return string.Join("|", nodes);
    }

    private static bool IsFrameworkProvidedDependency(string typeName) =>
        typeName.StartsWith("global::Microsoft.Extensions.Logging.ILogger<", StringComparison.Ordinal) ||
        typeName.StartsWith("global::Microsoft.Extensions.Options.IOptions<", StringComparison.Ordinal) ||
        typeName.StartsWith("global::Microsoft.Extensions.Options.IOptionsMonitor<", StringComparison.Ordinal) ||
        typeName.StartsWith("global::Microsoft.Extensions.Options.IOptionsSnapshot<", StringComparison.Ordinal) ||
        string.Equals(typeName, "global::Microsoft.Extensions.Configuration.IConfiguration", StringComparison.Ordinal) ||
        string.Equals(typeName, "global::Microsoft.Extensions.Hosting.IHostEnvironment", StringComparison.Ordinal) ||
        string.Equals(typeName, "global::Microsoft.AspNetCore.Hosting.IWebHostEnvironment", StringComparison.Ordinal);

    private static string GetNamespaceFromGeneratedTypeName(string typeName)
    {
        var normalized = typeName.StartsWith("global::", StringComparison.Ordinal)
            ? typeName.Substring("global::".Length)
            : typeName;
        var genericMarker = normalized.IndexOf('<');
        if (genericMarker >= 0)
        {
            normalized = normalized.Substring(0, genericMarker);
        }

        var lastDot = normalized.LastIndexOf('.');
        return lastDot >= 0 ? normalized.Substring(0, lastDot) : string.Empty;
    }

    private static void ReportDiscoveryDiagnostics(
        SourceProductionContext context,
        InjectlynxConfiguration configuration,
        ModuleModel module,
        ImmutableArray<ServiceCandidate> candidates,
        ImmutableArray<ModuleModel> modules)
    {
        foreach (var convention in module.Conventions)
        {
            if (convention.Strategy != RegistrationStrategy.MatchingInterface &&
                convention.Strategy != RegistrationStrategy.MatchingInterfaceAndSelf)
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                if (!MatchesConvention(candidate, convention))
                {
                    continue;
                }

                var expectedInterface = "I" + candidate.Name;
                var matchingInterfaces = candidate.Interfaces
                    .Where(item => string.Equals(item.Name, expectedInterface, StringComparison.Ordinal))
                    .ToImmutableArray();

                if (matchingInterfaces.Length == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ApplyDiagnosticOverride(MissingMatchingInterface, configuration),
                        candidate.Location,
                        ImmutableDictionary<string, string?>.Empty
                            .Add("Injectlynx.Module", module.Identity.Name)
                            .Add("Injectlynx.Convention.Namespace", convention.IncludedNamespace)
                            .Add("Injectlynx.Convention.ClassPrefix", convention.ClassPrefix ?? string.Empty)
                            .Add("Injectlynx.Convention.ClassSuffix", convention.ClassSuffix ?? string.Empty)
                            .Add("Injectlynx.Service", candidate.FullyQualifiedMetadataName),
                        candidate.FullyQualifiedMetadataName,
                        expectedInterface));
                }
                else if (matchingInterfaces.Length > 1)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ApplyDiagnosticOverride(AmbiguousContract, configuration),
                        candidate.Location,
                        ImmutableDictionary<string, string?>.Empty
                            .Add("Injectlynx.Module", module.Identity.Name)
                            .Add("Injectlynx.Convention.Namespace", convention.IncludedNamespace)
                            .Add("Injectlynx.Convention.ClassPrefix", convention.ClassPrefix ?? string.Empty)
                            .Add("Injectlynx.Convention.ClassSuffix", convention.ClassSuffix ?? string.Empty)
                            .Add("Injectlynx.Service", candidate.FullyQualifiedMetadataName)
                            .Add("Injectlynx.Lifetime", convention.Lifetime.ToString())
                            .Add("Injectlynx.AmbiguousContracts", string.Join("\n", matchingInterfaces.Select(static item => item.FullyQualifiedName))),
                        candidate.FullyQualifiedMetadataName,
                        expectedInterface));
                }
            }
        }

        foreach (var convention in module.Conventions)
        {
            if (convention.Strategy != RegistrationStrategy.ImplementedInterfaces)
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                var matchesConvention = MatchesConvention(candidate, convention);
                if (!matchesConvention &&
                    !ShouldReportImplementedInterfaceFilterMiss(candidate, convention))
                {
                    continue;
                }

                var matchingInterfaces = candidate.Interfaces
                    .Where(item => MatchesInterfaceNameFilter(item, convention))
                    .ToImmutableArray();

                if (matchingInterfaces.Length == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ApplyDiagnosticOverride(MissingImplementedInterfaces, configuration),
                        candidate.Location,
                        ImmutableDictionary<string, string?>.Empty
                            .Add("Injectlynx.Module", module.Identity.Name)
                            .Add("Injectlynx.Convention.Namespace", convention.IncludedNamespace)
                            .Add("Injectlynx.Convention.ClassPrefix", convention.ClassPrefix ?? string.Empty)
                            .Add("Injectlynx.Convention.ClassSuffix", convention.ClassSuffix ?? string.Empty)
                            .Add("Injectlynx.Convention.InterfacePrefix", convention.InterfacePrefix ?? string.Empty)
                            .Add("Injectlynx.Convention.InterfaceSuffix", convention.InterfaceSuffix ?? string.Empty)
                            .Add("Injectlynx.Service", candidate.FullyQualifiedMetadataName),
                        candidate.FullyQualifiedMetadataName));
                }
            }
        }

        foreach (var duplicate in FindDuplicateContracts(module, candidates))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ApplyDiagnosticOverride(DuplicateRegistration, configuration),
                duplicate.Location,
                duplicate.Contract,
                module.Identity.Name));
        }
    }

    private static ImmutableArray<GeneratedRegistration> DiscoverRegistrations(
        ModuleModel module,
        ImmutableArray<ServiceCandidate> candidates,
        System.Threading.CancellationToken cancellationToken)
    {
        var registrations = ImmutableArray.CreateBuilder<GeneratedRegistration>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var convention in module.Conventions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var candidate in candidates)
            {
                if (!MatchesConvention(candidate, convention))
                {
                    continue;
                }

                foreach (var contract in ResolveContracts(candidate, convention))
                {
                    if (IsExcludedType(contract, convention.ExcludedTypes))
                    {
                        continue;
                    }

                    var key = contract + "|" + candidate.FullyQualifiedMetadataName + "|" + convention.Lifetime;
                    if (seen.Add(key))
                    {
                        registrations.Add(new GeneratedRegistration(
                            contract,
                            candidate.FullyQualifiedMetadataName,
                            convention.Lifetime,
                            null,
                            ImmutableArray<string>.Empty,
                            null,
                            CreateRegistrationReason(candidate, convention, contract)));
                    }
                }
            }
        }

        foreach (var explicitRegistration in module.ExplicitRegistrations)
        {
            var contract = NormalizeConfiguredTypeName(explicitRegistration.Contract);
            var implementation = NormalizeConfiguredTypeName(explicitRegistration.Implementation);
            var key = contract + "|" + implementation + "|" + explicitRegistration.Lifetime + "|" + explicitRegistration.Key;
            if (seen.Add(key))
            {
                registrations.Add(new GeneratedRegistration(
                    contract,
                    implementation,
                    explicitRegistration.Lifetime,
                    explicitRegistration.Key,
                    ImmutableArray<string>.Empty,
                    null,
                    CreateExplicitRegistrationReason(explicitRegistration, contract, implementation)));
            }
        }

        return registrations
            .OrderBy(static item => item.Contract, StringComparer.Ordinal)
            .ThenBy(static item => item.Implementation, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static void ReportDecoratorDiagnostics(
        SourceProductionContext context,
        InjectlynxConfiguration configuration,
        ModuleModel module,
        ImmutableArray<ServiceCandidate> candidates,
        ImmutableArray<GeneratedRegistration> registrations)
    {
        var contracts = registrations
            .Select(static item => item.Contract)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var candidateByImplementation = candidates
            .GroupBy(static item => item.FullyQualifiedMetadataName, StringComparer.Ordinal)
            .ToDictionary(static item => item.Key, static item => item.First(), StringComparer.Ordinal);

        foreach (var decorator in module.Decorators)
        {
            var contract = NormalizeConfiguredTypeName(decorator.Contract.FullName);
            var decoratorType = NormalizeConfiguredTypeName(decorator.Decorator.FullName);
            if (!contracts.Contains(contract))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ApplyDiagnosticOverride(MissingDecoratorTarget, configuration),
                    Location.None,
                    decoratorType,
                    contract));
                continue;
            }

            if (candidateByImplementation.TryGetValue(decoratorType, out var decoratorCandidate) &&
                !decoratorCandidate.Interfaces.Any(item => string.Equals(item.FullyQualifiedName, contract, StringComparison.Ordinal)))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ApplyDiagnosticOverride(InvalidDecoratorContract, configuration),
                    decoratorCandidate.Location,
                    decoratorType,
                    contract));
            }
        }
    }

    private static ImmutableArray<GeneratedRegistration> ApplyDecorators(
        ModuleModel module,
        ImmutableArray<GeneratedRegistration> registrations)
    {
        if (module.Decorators.Length == 0)
        {
            return registrations;
        }

        var decoratorsByContract = module.Decorators
            .GroupBy(static item => NormalizeConfiguredTypeName(item.Contract.FullName), StringComparer.Ordinal)
            .ToDictionary(
                static item => item.Key,
                static item => item
                    .OrderBy(static decorator => decorator.Order)
                    .ThenBy(static decorator => decorator.Decorator, ServiceTypeIdentity.FullNameComparer)
                    .Select(static decorator => NormalizeConfiguredTypeName(decorator.Decorator.FullName))
                    .ToImmutableArray(),
                StringComparer.Ordinal);

        return registrations
            .Select(registration => registration.Key is null &&
                    !IsOpenGenericName(registration.Contract) &&
                    !IsOpenGenericName(registration.Implementation) &&
                    decoratorsByContract.TryGetValue(registration.Contract, out var decorators)
                ? registration with { Decorators = decorators, Reason = AddDecoratorReason(registration.Reason, decorators) }
                : registration)
            .ToImmutableArray();
    }

    private static ImmutableArray<GeneratedRegistration> ApplyMemberInjections(
        SourceProductionContext context,
        InjectlynxConfiguration configuration,
        ModuleModel module,
        ImmutableArray<ServiceCandidate> candidates,
        ImmutableArray<GeneratedRegistration> registrations)
    {
        if (module.MemberInjections.Length == 0)
        {
            return registrations;
        }

        var candidatesByImplementation = candidates
            .GroupBy(static item => item.FullyQualifiedMetadataName, StringComparer.Ordinal)
            .ToDictionary(static item => item.Key, static item => item.First(), StringComparer.Ordinal);
        var memberInjections = module.MemberInjections
            .GroupBy(static item => NormalizeConfiguredTypeName(item.Implementation), StringComparer.Ordinal)
            .ToDictionary(static item => item.Key, static item => item.First(), StringComparer.Ordinal);
        var registrationsByImplementation = registrations
            .GroupBy(static item => item.Implementation, StringComparer.Ordinal)
            .ToDictionary(static item => item.Key, static item => item.ToImmutableArray(), StringComparer.Ordinal);

        foreach (var memberInjection in memberInjections)
        {
            if (!registrationsByImplementation.ContainsKey(memberInjection.Key))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ApplyDiagnosticOverride(ConventionDslError, configuration),
                    Location.None,
                    "Member injection target " + memberInjection.Key + " is not generated by any Injectlynx registration."));
            }
        }

        return registrations
            .Select(registration =>
            {
                if (!memberInjections.TryGetValue(registration.Implementation, out var memberInjection) ||
                    !candidatesByImplementation.TryGetValue(registration.Implementation, out var candidate))
                {
                    return registration;
                }

                var plan = CreateMemberInjectionPlan(context, configuration, candidate, memberInjection);
                return plan is null
                    ? registration
                    : registration with
                    {
                        MemberInjection = plan,
                        Reason = AddMemberInjectionReason(registration.Reason, plan)
                    };
            })
            .ToImmutableArray();
    }

    private static MemberInjectionPlan? CreateMemberInjectionPlan(
        SourceProductionContext context,
        InjectlynxConfiguration configuration,
        ServiceCandidate candidate,
        MemberInjectionModel memberInjection)
    {
        var properties = ImmutableArray.CreateBuilder<PropertyInjectionPlan>();
        var methods = ImmutableArray.CreateBuilder<MethodInjectionPlan>();
        var hasErrors = false;
        var seenProperties = new HashSet<string>(StringComparer.Ordinal);
        var seenMethods = new HashSet<string>(StringComparer.Ordinal);

        foreach (var propertyInjection in memberInjection.Properties)
        {
            if (!seenProperties.Add(propertyInjection.Name))
            {
                ReportMemberInjectionError(context, configuration, candidate, "Property " + propertyInjection.Name + " is configured for injection more than once.");
                hasErrors = true;
                continue;
            }

            var property = candidate.Properties.FirstOrDefault(item => string.Equals(item.Name, propertyInjection.Name, StringComparison.Ordinal));
            if (property is null)
            {
                ReportMemberInjectionError(context, configuration, candidate, "Property " + propertyInjection.Name + " was not found or has no accessible setter on " + candidate.FullyQualifiedMetadataName + ".");
                hasErrors = true;
                continue;
            }

            if (propertyInjection.Optional && !property.IsNullable)
            {
                ReportMemberInjectionError(context, configuration, candidate, "Optional property " + propertyInjection.Name + " must be nullable.");
                hasErrors = true;
                continue;
            }

            properties.Add(new PropertyInjectionPlan(property.Name, property.Type, propertyInjection.Optional));
        }

        foreach (var methodInjection in memberInjection.Methods)
        {
            var methodKey = methodInjection.Name + "|" + string.Join(",", methodInjection.Arguments.Select(static item => item.ParameterName));
            if (!seenMethods.Add(methodKey))
            {
                ReportMemberInjectionError(context, configuration, candidate, "Method " + methodInjection.Name + " is configured for injection more than once with the same arguments.");
                hasErrors = true;
                continue;
            }

            var matchingMethods = candidate.Methods
                .Where(item => string.Equals(item.Name, methodInjection.Name, StringComparison.Ordinal))
                .ToImmutableArray();
            if (matchingMethods.Length == 0)
            {
                ReportMemberInjectionError(context, configuration, candidate, "Method " + methodInjection.Name + " was not found on " + candidate.FullyQualifiedMetadataName + ".");
                hasErrors = true;
                continue;
            }

            var method = matchingMethods.Length == 1
                ? matchingMethods[0]
                : matchingMethods.FirstOrDefault(methodCandidate => MethodCanUseArguments(methodCandidate, methodInjection.Arguments));

            if (method is null)
            {
                ReportMemberInjectionError(context, configuration, candidate, "Method " + methodInjection.Name + " is ambiguous; configure arguments for one overload.");
                hasErrors = true;
                continue;
            }

            var argumentPlans = ImmutableArray.CreateBuilder<MethodArgumentPlan>();
            foreach (var parameter in method.Parameters)
            {
                var configured = methodInjection.Arguments.FirstOrDefault(item => string.Equals(item.ParameterName, parameter.Name, StringComparison.Ordinal));
                if (configured is not null)
                {
                    if (configured.Kind == MethodArgumentInjectionKind.Constant)
                    {
                        argumentPlans.Add(new MethodArgumentPlan(parameter.Name, parameter.Type, false, configured.ValueExpression));
                    }
                    else
                    {
                        var serviceType = configured.ServiceType ?? parameter.Type;
                        if (!string.Equals(serviceType, parameter.Type, StringComparison.Ordinal))
                        {
                            ReportMemberInjectionError(context, configuration, candidate, "Method argument " + parameter.Name + " expects " + parameter.Type + " but was configured with service " + serviceType + ".");
                            hasErrors = true;
                            continue;
                        }

                        argumentPlans.Add(new MethodArgumentPlan(parameter.Name, parameter.Type, false, null));
                    }

                    continue;
                }

                if (IsPrimitiveLike(parameter.Type) || string.Equals(parameter.Type, "object", StringComparison.Ordinal) || string.Equals(parameter.Type, "global::System.Object", StringComparison.Ordinal))
                {
                    ReportMemberInjectionError(context, configuration, candidate, "Method argument " + parameter.Name + " on " + method.Name + " requires an explicit constant or service argument.");
                    hasErrors = true;
                    continue;
                }

                argumentPlans.Add(new MethodArgumentPlan(parameter.Name, parameter.Type, false, null));
            }

            methods.Add(new MethodInjectionPlan(method.Name, argumentPlans.ToImmutable()));
        }

        return hasErrors
            ? null
            : new MemberInjectionPlan(properties.ToImmutable(), methods.ToImmutable());
    }

    private static bool MethodCanUseArguments(MethodCandidate method, ImmutableArray<MethodArgumentInjectionModel> arguments) =>
        arguments.All(argument => method.Parameters.Any(parameter => string.Equals(parameter.Name, argument.ParameterName, StringComparison.Ordinal)));

    private static bool IsPrimitiveLike(string typeName) =>
        typeName is "string" or "bool" or "char" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double" or "decimal" ||
        typeName is "global::System.String" or "global::System.Boolean" or "global::System.Char" or "global::System.Byte" or "global::System.SByte" ||
        typeName is "global::System.Int16" or "global::System.UInt16" or "global::System.Int32" or "global::System.UInt32" ||
        typeName is "global::System.Int64" or "global::System.UInt64" or "global::System.Single" or "global::System.Double" or "global::System.Decimal";

    private static void ReportMemberInjectionError(
        SourceProductionContext context,
        InjectlynxConfiguration configuration,
        ServiceCandidate candidate,
        string message) =>
        context.ReportDiagnostic(Diagnostic.Create(ApplyDiagnosticOverride(ConventionDslError, configuration), candidate.Location, message));

    private static GeneratedRegistrationReason AddMemberInjectionReason(
        GeneratedRegistrationReason reason,
        MemberInjectionPlan plan)
    {
        var details = reason.Details.Items
            .Concat(plan.Properties.Select(static property => "Property injection: " + property.Name + " from " + property.Type + (property.Optional ? " (optional)." : ".")))
            .Concat(plan.Methods.Select(static method => "Method injection: " + method.Name + "."));

        return reason with
        {
            Summary = reason.Summary + " Member injection is applied by generated factory.",
            Details = new EquatableArray<string>(details)
        };
    }

    private static GeneratedRegistrationReason AddDecoratorReason(
        GeneratedRegistrationReason reason,
        ImmutableArray<string> decorators)
    {
        var details = reason.Details.Items
            .Concat(decorators.Select(static decorator => "Decorator applied: " + decorator + "."))
            .ToArray();

        return reason with
        {
            Kind = RegistrationReasonKind.Decorator,
            Summary = reason.Summary + " Decorators are applied in configured order.",
            Details = new EquatableArray<string>(details)
        };
    }

    private static GeneratedRegistrationReason CreateExplicitRegistrationReason(
        ExplicitRegistrationModel registration,
        string contract,
        string implementation)
    {
        return new GeneratedRegistrationReason(
            RegistrationReasonKind.ExplicitOverride,
            implementation + " registered as " + contract + " because it is declared explicitly in the " + registration.Module.Name + " module.",
            new EquatableArray<string>(new[]
            {
                "Registration strategy is Explicit.",
                "Service contract configured as " + contract + ".",
                "Implementation configured as " + implementation + ".",
                "Lifetime is " + registration.Lifetime + ".",
                registration.Key is null ? "Registration is not keyed." : "Key is " + registration.Key + ".",
                "Existing-registration behavior is " + registration.ExistingRegistrationBehavior + ".",
                "Module is " + registration.Module.Name + "."
            }));
    }

    private static string NormalizeConfiguredTypeName(string typeName)
    {
        var trimmed = typeName.Trim();
        return trimmed.StartsWith("global::", StringComparison.Ordinal)
            ? trimmed
            : "global::" + trimmed;
    }

    private static bool IsOpenGenericName(string typeName) =>
        typeName.Contains("<>") ||
        typeName.Contains("<,>") ||
        typeName.Contains("<,,>") ||
        typeName.Contains("<,,,>");

    private static GeneratedRegistrationReason CreateRegistrationReason(
        ServiceCandidate candidate,
        ConventionModel convention,
        string contract)
    {
        var kind = convention.Strategy switch
        {
            RegistrationStrategy.ImplementedInterfaces => RegistrationReasonKind.ImplementedInterfaces,
            RegistrationStrategy.Self => RegistrationReasonKind.SelfRegistration,
            RegistrationStrategy.MatchingInterfaceAndSelf when string.Equals(contract, candidate.FullyQualifiedMetadataName, StringComparison.Ordinal) => RegistrationReasonKind.SelfRegistration,
            _ => RegistrationReasonKind.MatchingInterface
        };

        var details = ImmutableArray.CreateBuilder<string>();
        details.Add(candidate.FullyQualifiedMetadataName + " is a concrete " + candidate.Accessibility + " class.");
        details.Add("Namespace matched convention namespace " + convention.IncludedNamespace + ".");

        if (convention.ClassPrefix is not null)
        {
            details.Add("Class name starts with " + convention.ClassPrefix + ".");
        }

        if (convention.ClassSuffix is not null)
        {
            details.Add("Class name ends with " + convention.ClassSuffix + ".");
        }

        if (convention.AssignableToOpenGenericType is not null)
        {
            details.Add("Type is assignable to open generic " + convention.AssignableToOpenGenericType + ".");
        }

        if (convention.InterfacePrefix is not null)
        {
            details.Add("Interface name starts with " + convention.InterfacePrefix + ".");
        }

        if (convention.InterfaceSuffix is not null)
        {
            details.Add("Interface name ends with " + convention.InterfaceSuffix + ".");
        }

        details.Add("Registration strategy is " + convention.Strategy + ".");
        details.Add("Service contract resolved as " + contract + ".");
        details.Add("Lifetime is " + convention.Lifetime + ".");
        details.Add("Module is " + convention.Module.Name + ".");

        return new GeneratedRegistrationReason(
            kind,
            candidate.FullyQualifiedMetadataName + " registered as " + contract + " because it matched the " + convention.Module.Name + " convention.",
            new EquatableArray<string>(details));
    }

    private static bool MatchesConvention(ServiceCandidate candidate, ConventionModel convention)
    {
        if (!string.Equals(candidate.Accessibility, convention.Accessibility, StringComparison.Ordinal))
        {
            return false;
        }

        if (!candidate.Namespace.StartsWith(convention.IncludedNamespace, StringComparison.Ordinal))
        {
            return false;
        }

        if (convention.ExcludedNamespaces.Any(excluded => candidate.Namespace.StartsWith(excluded, StringComparison.Ordinal)))
        {
            return false;
        }

        if (IsExcludedType(candidate.FullyQualifiedMetadataName, convention.ExcludedTypes) ||
            IsExcludedType(candidate.Name, convention.ExcludedTypes))
        {
            return false;
        }

        if (convention.AssignableToOpenGenericType is not null &&
            !candidate.Interfaces.Any(item => IsSameOpenGenericType(item.OpenGenericDefinition, convention.AssignableToOpenGenericType)))
        {
            return false;
        }

        if (convention.ClassPrefix is not null && !candidate.Name.StartsWith(convention.ClassPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (convention.ClassSuffix is not null && !candidate.Name.EndsWith(convention.ClassSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        if ((convention.InterfacePrefix is not null || convention.InterfaceSuffix is not null) &&
            !candidate.Interfaces.Any(item => MatchesInterfaceNameFilter(item, convention)))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldReportImplementedInterfaceFilterMiss(ServiceCandidate candidate, ConventionModel convention)
    {
        if (convention.InterfacePrefix is null && convention.InterfaceSuffix is null)
        {
            return false;
        }

        if (convention.ClassPrefix is null &&
            convention.ClassSuffix is null &&
            convention.AssignableToOpenGenericType is null)
        {
            return false;
        }

        if (!MatchesConventionIgnoringInterfaceNameFilter(candidate, convention))
        {
            return false;
        }

        return !candidate.Interfaces.Any(item => MatchesInterfaceNameFilter(item, convention));
    }

    private static bool MatchesConventionIgnoringInterfaceNameFilter(ServiceCandidate candidate, ConventionModel convention)
    {
        if (!candidate.Namespace.StartsWith(convention.IncludedNamespace, StringComparison.Ordinal))
        {
            return false;
        }

        if (convention.ExcludedNamespaces.Any(excluded => candidate.Namespace.StartsWith(excluded, StringComparison.Ordinal)))
        {
            return false;
        }

        if (IsExcludedType(candidate.FullyQualifiedMetadataName, convention.ExcludedTypes) ||
            IsExcludedType(candidate.Name, convention.ExcludedTypes))
        {
            return false;
        }

        if (convention.AssignableToOpenGenericType is not null &&
            !candidate.Interfaces.Any(item => IsSameOpenGenericType(item.OpenGenericDefinition, convention.AssignableToOpenGenericType)))
        {
            return false;
        }

        if (convention.ClassPrefix is not null && !candidate.Name.StartsWith(convention.ClassPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (convention.ClassSuffix is not null && !candidate.Name.EndsWith(convention.ClassSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsExcludedType(string typeName, ImmutableArray<string> excludedTypes)
    {
        if (excludedTypes.Length == 0)
        {
            return false;
        }

        var normalized = typeName.StartsWith("global::", StringComparison.Ordinal)
            ? typeName.Substring("global::".Length)
            : typeName;
        var simpleName = normalized;
        var genericMarker = simpleName.IndexOf('<');
        if (genericMarker >= 0)
        {
            simpleName = simpleName.Substring(0, genericMarker);
        }

        var lastDot = simpleName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            simpleName = simpleName.Substring(lastDot + 1);
        }

        foreach (var excludedType in excludedTypes)
        {
            var normalizedExclusion = excludedType.StartsWith("global::", StringComparison.Ordinal)
                ? excludedType.Substring("global::".Length)
                : excludedType;

            if (string.Equals(normalized, normalizedExclusion, StringComparison.Ordinal) ||
                string.Equals(simpleName, normalizedExclusion, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOpenGenericType(string candidateType, string configuredType)
    {
        if (string.Equals(candidateType, configuredType, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(GetGenericTypeDefinitionName(candidateType), GetGenericTypeDefinitionName(configuredType), StringComparison.Ordinal);
    }

    private static string GetGenericTypeDefinitionName(string typeName)
    {
        var marker = typeName.IndexOf('<');
        return marker < 0 ? typeName : typeName.Substring(0, marker);
    }

    private static IEnumerable<string> ResolveContracts(ServiceCandidate candidate, ConventionModel convention)
    {
        if (convention.Strategy == RegistrationStrategy.Self)
        {
            yield return candidate.FullyQualifiedMetadataName;
            yield break;
        }

        var matchingInterfaceName = "I" + candidate.Name;
        if (convention.Strategy == RegistrationStrategy.MatchingInterface ||
            convention.Strategy == RegistrationStrategy.MatchingInterfaceAndSelf)
        {
            var matches = candidate.Interfaces
                .Where(item => string.Equals(item.Name, matchingInterfaceName, StringComparison.Ordinal))
                .ToImmutableArray();

            if (matches.Length == 1)
            {
                yield return matches[0].FullyQualifiedName;
            }
        }

        if (convention.Strategy == RegistrationStrategy.ImplementedInterfaces)
        {
            foreach (var contract in candidate.Interfaces.Where(item => MatchesInterfaceNameFilter(item, convention)))
            {
                yield return contract.FullyQualifiedName;
            }
        }

        if (convention.Strategy == RegistrationStrategy.MatchingInterfaceAndSelf)
        {
            yield return candidate.FullyQualifiedMetadataName;
        }
    }

    private static bool MatchesInterfaceNameFilter(InterfaceCandidate interfaceCandidate, ConventionModel convention)
    {
        if (convention.InterfacePrefix is not null && !interfaceCandidate.Name.StartsWith(convention.InterfacePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (convention.InterfaceSuffix is not null && !interfaceCandidate.Name.EndsWith(convention.InterfaceSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static ImmutableArray<DuplicateContract> FindDuplicateContracts(ModuleModel module, ImmutableArray<ServiceCandidate> candidates)
    {
        var byContract = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var locations = new Dictionary<string, Location?>(StringComparer.Ordinal);

        foreach (var convention in module.Conventions)
        {
            foreach (var candidate in candidates)
            {
                if (!MatchesConvention(candidate, convention))
                {
                    continue;
                }

                foreach (var contract in ResolveContracts(candidate, convention))
                {
                    if (!byContract.TryGetValue(contract, out var implementations))
                    {
                        implementations = new HashSet<string>(StringComparer.Ordinal);
                        byContract.Add(contract, implementations);
                        locations.Add(contract, candidate.Location);
                    }

                    implementations.Add(candidate.FullyQualifiedMetadataName);
                }
            }
        }

        return byContract
            .Where(static item => item.Value.Count > 1)
            .Select(item => new DuplicateContract(item.Key, locations[item.Key]))
            .OrderBy(static item => item.Contract, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static string FormatTypeForGeneratedCode(INamedTypeSymbol symbol)
    {
        if (symbol.Arity == 0 || symbol.TypeArguments.Length == 0)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        if (IsOpenGenericDefinition(symbol))
        {
            return FormatOpenGenericDefinitionForGeneratedCode(symbol);
        }

        var namespacePrefix = symbol.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace
            ? "global::"
            : "global::" + symbol.ContainingNamespace.ToDisplayString() + ".";

        return namespacePrefix +
            symbol.Name +
            "<" +
            string.Join(", ", symbol.TypeArguments.Select(static argument => argument is INamedTypeSymbol namedType
                ? FormatTypeForGeneratedCode(namedType)
                : argument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) +
            ">";
    }

    private static bool IsOpenGenericDefinition(INamedTypeSymbol symbol)
    {
        if (symbol.TypeArguments.Length != symbol.TypeParameters.Length)
        {
            return false;
        }

        for (var index = 0; index < symbol.TypeArguments.Length; index++)
        {
            if (!SymbolEqualityComparer.Default.Equals(symbol.TypeArguments[index], symbol.TypeParameters[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatOpenGenericDefinitionForGeneratedCode(INamedTypeSymbol symbol)
    {
        var definition = symbol.OriginalDefinition;
        if (definition.Arity == 0)
        {
            return definition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        var namespacePrefix = definition.ContainingNamespace is null || definition.ContainingNamespace.IsGlobalNamespace
            ? "global::"
            : "global::" + definition.ContainingNamespace.ToDisplayString() + ".";

        return namespacePrefix + definition.Name + "<" + new string(',', definition.Arity - 1) + ">";
    }

    private static string FormatImplementedInterfaceForGeneratedCode(INamedTypeSymbol interfaceSymbol, INamedTypeSymbol implementationSymbol) =>
        implementationSymbol.TypeParameters.Length == 0 && interfaceSymbol.TypeArguments.Length > 0
            ? interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : FormatTypeForGeneratedCode(interfaceSymbol);

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    private static string ToStringLiteral(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            if (character == '\\' || character == '"')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        builder.Append('"');
        return builder.ToString();
    }

    private sealed record ServiceCandidate(
        string Namespace,
        string Name,
        string FullyQualifiedMetadataName,
        string Accessibility,
        ImmutableArray<InterfaceCandidate> Interfaces,
        int GenericArity,
        ImmutableArray<ConstructorCandidate> Constructors,
        ImmutableArray<PropertyCandidate> Properties,
        ImmutableArray<MethodCandidate> Methods,
        Location? Location);

    private sealed record InterfaceCandidate(
        string FullyQualifiedName,
        string OpenGenericDefinition,
        string Name,
        int GenericArity);

    private sealed record DuplicateContract(
        string Contract,
        Location? Location);

    private sealed record ConstructorCandidate(
        bool IsPublic,
        ImmutableArray<string> Dependencies,
        Location? Location);

    private sealed record PropertyCandidate(
        string Name,
        string Type,
        bool IsNullable,
        Location? Location);

    private sealed record MethodCandidate(
        string Name,
        ImmutableArray<ParameterCandidate> Parameters,
        Location? Location);

    private sealed record ParameterCandidate(
        string Name,
        string Type,
        bool IsNullable);

    private sealed record DependencyCycle(
        string Path,
        Location? Location);

    private sealed record GeneratorOptions(
        bool DevelopmentReport,
        DiagnosticSeverityModel DevelopmentReportSeverity);

    private sealed record DslModuleInput(
        string Name,
        string? ProjectName,
        string? GeneratedMethod,
        string? GeneratedNamespace,
        ImmutableArray<DslConventionInput> Conventions,
        ImmutableArray<DslExplicitRegistrationInput> ExplicitRegistrations,
        ImmutableArray<DslDecoratorInput> Decorators,
        ImmutableArray<DslMemberInjectionInput> MemberInjections,
        ImmutableArray<DslExternalServiceInput> ExternalServices,
        ImmutableArray<DslFrameworkServiceInput> FrameworkServices,
        ImmutableArray<DslArchitectureRuleInput> ArchitectureRules,
        ImmutableArray<DslDiagnosticOverrideInput> DiagnosticOverrides,
        ImmutableArray<DslErrorInput> Errors);

    private sealed record DslConventionInput(
        string ModuleName,
        string Namespace,
        ImmutableArray<string> ExcludedNamespaces,
        ImmutableArray<string> ExcludedTypes,
        string? ClassPrefix,
        string? ClassSuffix,
        string? InterfacePrefix,
        string? InterfaceSuffix,
        string? AssignableToOpenGeneric,
        ServiceLifetimeModel Lifetime,
        RegistrationStrategy Strategy,
        Location? Location);

    private sealed record DslExplicitRegistrationInput(
        string Contract,
        string Implementation,
        ServiceLifetimeModel Lifetime,
        string? Key,
        Location? Location);

    private sealed record DslExternalServiceInput(
        string Contract,
        ServiceLifetimeModel? Lifetime,
        string? Key,
        Location? Location);

    private sealed record DslFrameworkServiceInput(
        string Contract,
        string Provider,
        Location? Location);

    private sealed record DslDecoratorInput(
        string Contract,
        string Decorator,
        int Order,
        Location? Location);

    private sealed record DslArchitectureRuleInput(
        string FromNamespace,
        string ToNamespace,
        DiagnosticSeverityModel Severity,
        string? Message,
        Location? Location);

    private sealed record DslDiagnosticOverrideInput(
        string DiagnosticId,
        DiagnosticSeverityModel Severity,
        Location? Location);

    private sealed record DslMemberInjectionInput(
        string Implementation,
        ImmutableArray<DslPropertyInjectionInput> Properties,
        ImmutableArray<DslMethodInjectionInput> Methods);

    private sealed record DslPropertyInjectionInput(
        string Name,
        bool Optional,
        Location? Location);

    private sealed record DslMethodInjectionInput(
        string Name,
        ImmutableArray<DslMethodArgumentInput> Arguments,
        Location? Location);

    private sealed record DslMethodArgumentInput(
        string ParameterName,
        MethodArgumentInjectionKind Kind,
        string? ValueExpression,
        string? ServiceType,
        Location? Location);

    private sealed record DslCallInput(
        string Name,
        ImmutableArray<TypeSyntax> TypeArguments,
        SeparatedSyntaxList<ArgumentSyntax> Arguments,
        Location? Location);

    private sealed record DslErrorInput(
        string Message,
        Location? Location);
}
