using System;
using System.Linq.Expressions;

namespace Injectlynx;

public interface IServiceConventionBuilder
{
    /// <summary>
    /// Selects concrete service types declared in the specified namespace.
    /// </summary>
    IServiceConventionRuleBuilder FromNamespace(string namespaceName);

    /// <summary>
    /// Configures property or method injection for a specific implementation type.
    /// </summary>
    IServiceTypeInjectionBuilder<TImplementation> For<TImplementation>();

    /// <summary>
    /// Registers one explicit service contract and implementation pair.
    /// </summary>
    IServiceExplicitRegistrationBuilder Register<TService, TImplementation>()
        where TImplementation : TService;

    /// <summary>
    /// Declares a service that is registered outside Injectlynx but should satisfy dependency diagnostics.
    /// </summary>
    IServiceExternalServiceBuilder External<TService>();

    /// <summary>
    /// Declares a framework-provided service that should satisfy dependency diagnostics.
    /// </summary>
    IServiceFrameworkServiceBuilder FrameworkProvided<TService>();

    /// <summary>
    /// Registers a decorator for an existing service contract.
    /// </summary>
    IServiceDecoratorBuilder Decorate<TService, TDecorator>()
        where TDecorator : TService;

    /// <summary>
    /// Starts an architecture dependency rule between namespaces.
    /// </summary>
    IServiceArchitectureRuleBuilder ForbidDependency();

    /// <summary>
    /// Overrides the severity of an Injectlynx diagnostic.
    /// </summary>
    IServiceDiagnosticBuilder Diagnostic(string diagnosticId);

    /// <summary>
    /// Sets the logical module name used to group generated registrations.
    /// </summary>
    IServiceConventionBuilder ModuleName(string moduleName);

    /// <summary>
    /// Overrides the generated DI extension method name. By default, Injectlynx generates AddInjectlynxServices().
    /// For example, use GeneratedMethod("AddWebApiServices") and then call builder.Services.AddWebApiServices().
    /// </summary>
    IServiceConventionBuilder GeneratedMethod(string methodName);

    /// <summary>
    /// Overrides the namespace that contains the generated DI extension method.
    /// Add a matching using directive in Program.cs before calling a method from a custom namespace.
    /// </summary>
    IServiceConventionBuilder GeneratedNamespace(string namespaceName);
}

public interface IServiceConventionRuleBuilder
{
    IServiceConventionRuleBuilder WhereNameStartsWith(string prefix);

    IServiceConventionRuleBuilder WhereNameEndsWith(string suffix);

    IServiceConventionRuleBuilder WhereInterfaceNameStartsWith(string prefix);

    IServiceConventionRuleBuilder WhereInterfaceNameEndsWith(string suffix);

    IServiceConventionRuleBuilder AssignableToOpenGeneric(System.Type openGenericType);

    IServiceConventionRuleBuilder ExcludeNamespace(string namespaceName);

    IServiceConventionRuleBuilder ExcludeType<TImplementation>();

    IServiceConventionRuleBuilder AsMatchingInterface();

    IServiceConventionRuleBuilder AsImplementedInterfaces();

    IServiceConventionRuleBuilder AsSelf();

    IServiceConventionRuleBuilder AsMatchingInterfaceAndSelf();

    IServiceConventionRuleBuilder WithSingletonLifetime();

    IServiceConventionRuleBuilder WithScopedLifetime();

    IServiceConventionRuleBuilder WithTransientLifetime();
}

public interface IServiceExplicitRegistrationBuilder
{
    IServiceExplicitRegistrationBuilder WithSingletonLifetime();

    IServiceExplicitRegistrationBuilder WithScopedLifetime();

    IServiceExplicitRegistrationBuilder WithTransientLifetime();

    IServiceExplicitRegistrationBuilder WithKey(string key);
}

public interface IServiceExternalServiceBuilder
{
    IServiceExternalServiceBuilder WithSingletonLifetime();

    IServiceExternalServiceBuilder WithScopedLifetime();

    IServiceExternalServiceBuilder WithTransientLifetime();

    IServiceExternalServiceBuilder WithKey(string key);
}

public interface IServiceFrameworkServiceBuilder
{
    IServiceFrameworkServiceBuilder FromProvider(string providerName);
}

public interface IServiceDecoratorBuilder
{
    IServiceDecoratorBuilder WithOrder(int order);
}

public interface IServiceArchitectureRuleBuilder
{
    IServiceArchitectureRuleBuilder FromNamespace(string namespaceName);

    IServiceArchitectureRuleBuilder ToNamespace(string namespaceName);

    IServiceArchitectureRuleBuilder AsWarning(string? message = null);

    IServiceArchitectureRuleBuilder AsError(string? message = null);
}

public interface IServiceDiagnosticBuilder
{
    IServiceConventionBuilder AsHidden();

    IServiceConventionBuilder AsInfo();

    IServiceConventionBuilder AsWarning();

    IServiceConventionBuilder AsError();
}

public interface IServiceTypeInjectionBuilder<TImplementation>
{
    IServiceMethodInjectionBuilder<TImplementation> InjectMethod(string methodName);

    IServiceMethodInjectionBuilder<TImplementation> InjectMethod(Expression<Action<TImplementation>> method);

    IServiceTypeInjectionBuilder<TImplementation> InjectProperty<TProperty>(
        Expression<Func<TImplementation, TProperty>> property);

    IServiceTypeInjectionBuilder<TImplementation> InjectOptionalProperty<TProperty>(
        Expression<Func<TImplementation, TProperty?>> property);
}

public interface IServiceMethodInjectionBuilder<TImplementation> : IServiceTypeInjectionBuilder<TImplementation>
{
    IServiceMethodInjectionBuilder<TImplementation> WithConstantArgument<TValue>(
        string parameterName,
        TValue value);

    IServiceMethodInjectionBuilder<TImplementation> WithServiceArgument<TService>(
        string parameterName);
}
