namespace Injectlynx.Core.Models;

public enum ServiceLifetimeModel
{
    Singleton,
    Scoped,
    Transient
}

public enum RegistrationStrategy
{
    MatchingInterface,
    ImplementedInterfaces,
    Self,
    MatchingInterfaceAndSelf,
    Explicit
}

public enum ExistingRegistrationBehavior
{
    Append,
    TryAdd,
    TryAddEnumerable,
    Replace,
    Error
}

public enum RegistrationStatus
{
    Valid,
    Warning,
    Invalid,
    External,
    FrameworkProvided
}

public enum DependencyConfidence
{
    Certain,
    Probable,
    UnknownExternal,
    FrameworkProvided
}

public enum DiagnosticSeverityModel
{
    Hidden,
    Info,
    Warning,
    Error
}

public enum RegistrationReasonKind
{
    MatchingInterface,
    ImplementedInterfaces,
    SelfRegistration,
    ExplicitOverride,
    OpenGeneric,
    KeyedService,
    FrameworkConvention,
    Decorator,
    ConventionMatch
}
