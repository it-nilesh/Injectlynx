using System.Collections.Immutable;

namespace Injectlynx.Core.Models;

public sealed record ConstructorModel(
    ServiceTypeIdentity DeclaringType,
    ImmutableArray<DependencyModel> Dependencies,
    SourceReference Source,
    bool IsAccessible,
    bool IsSelected);

public sealed record DependencyModel(
    ServiceTypeIdentity Contract,
    ServiceLifetimeModel? RequiredLifetime,
    KeyModel? Key,
    SourceReference Source,
    DependencyConfidence Confidence);

public sealed record DecoratorModel(
    ServiceTypeIdentity Contract,
    ServiceTypeIdentity Decorator,
    int Order,
    SourceReference Source);

public sealed record ExternalServiceModel(
    ServiceTypeIdentity Contract,
    ServiceLifetimeModel? Lifetime,
    KeyModel? Key,
    SourceReference Source);

public sealed record FrameworkProvidedServiceModel(
    ServiceTypeIdentity Contract,
    string Provider,
    SourceReference Source);

public sealed record ServiceRegistrationModel(
    ServiceTypeIdentity Contract,
    ServiceTypeIdentity Implementation,
    ServiceLifetimeModel Lifetime,
    ModuleIdentity Module,
    RegistrationStrategy Strategy,
    RegistrationReason Reason,
    ImmutableArray<DependencyModel> Dependencies,
    ImmutableArray<DecoratorModel> Decorators,
    SourceReference Source,
    RegistrationStatus Status);
