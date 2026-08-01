using System.Collections.Immutable;

namespace Injectlynx.Core.Models;

public sealed record ConventionModel(
    ModuleIdentity Module,
    string IncludedNamespace,
    ImmutableArray<string> ExcludedNamespaces,
    ImmutableArray<string> ExcludedTypes,
    string? ClassPrefix,
    string? ClassSuffix,
    string? InterfacePrefix,
    string? InterfaceSuffix,
    string? AssignableToOpenGenericType,
    string Accessibility,
    ServiceLifetimeModel Lifetime,
    RegistrationStrategy Strategy,
    ExistingRegistrationBehavior ExistingRegistrationBehavior);

public sealed record ModuleModel(
    ModuleIdentity Identity,
    string GeneratedMethod,
    string GeneratedNamespace,
    ImmutableArray<ConventionModel> Conventions,
    ImmutableArray<ExplicitRegistrationModel> ExplicitRegistrations,
    ImmutableArray<DecoratorModel> Decorators,
    ImmutableArray<MemberInjectionModel> MemberInjections);

public sealed record ExplicitRegistrationModel(
    ModuleIdentity Module,
    string Contract,
    string Implementation,
    ServiceLifetimeModel Lifetime,
    string? Key,
    ExistingRegistrationBehavior ExistingRegistrationBehavior,
    SourceReference Source);

public sealed record KeyModel(
    KeyIdentity Identity,
    SourceReference Source);

public sealed record GenericMappingModel(
    ServiceTypeIdentity Contract,
    ServiceTypeIdentity Implementation,
    int Arity,
    SourceReference Source);

public sealed record ForbiddenDependencyRuleModel(
    string FromNamespace,
    string ToNamespace,
    DiagnosticSeverityModel Severity,
    string? Message,
    SourceReference Source);

public sealed record MemberInjectionModel(
    string Implementation,
    ImmutableArray<PropertyInjectionModel> Properties,
    ImmutableArray<MethodInjectionModel> Methods);

public sealed record PropertyInjectionModel(
    string Name,
    bool Optional,
    SourceReference Source);

public sealed record MethodInjectionModel(
    string Name,
    ImmutableArray<MethodArgumentInjectionModel> Arguments,
    SourceReference Source);

public sealed record MethodArgumentInjectionModel(
    string ParameterName,
    MethodArgumentInjectionKind Kind,
    string? ValueExpression,
    string? ServiceType);

public enum MethodArgumentInjectionKind
{
    Constant,
    Service
}
