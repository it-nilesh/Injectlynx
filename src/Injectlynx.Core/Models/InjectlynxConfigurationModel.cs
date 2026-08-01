using System.Collections.Immutable;

namespace Injectlynx.Core.Models;

public sealed record InjectlynxConfiguration(
    int Version,
    ImmutableArray<ModuleModel> Modules,
    ImmutableArray<ExternalServiceModel> ExternalServices,
    ImmutableArray<FrameworkProvidedServiceModel> FrameworkProvidedServices,
    ImmutableArray<ForbiddenDependencyRuleModel> ForbiddenDependencyRules,
    ImmutableArray<DiagnosticSeverityOverride> DiagnosticSeverityOverrides)
{
    public const int CurrentVersion = 1;
}

public sealed record DiagnosticSeverityOverride(
    string DiagnosticId,
    DiagnosticSeverityModel Severity);

public static class ConfigurationDefaults
{
    public const string GeneratedNamespace = "Microsoft.Extensions.DependencyInjection";
    public const string Accessibility = "public";

    public const ServiceLifetimeModel Lifetime = ServiceLifetimeModel.Scoped;
    public const RegistrationStrategy RegistrationStrategy = Core.Models.RegistrationStrategy.MatchingInterface;
    public const ExistingRegistrationBehavior ExistingRegistrationBehavior = Core.Models.ExistingRegistrationBehavior.Append;
}
