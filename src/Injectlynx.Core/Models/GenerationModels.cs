using System.Collections.Immutable;

namespace Injectlynx.Core.Models;

public sealed record GeneratedMethodModel(
    ModuleIdentity Module,
    string Namespace,
    string TypeName,
    string MethodName,
    SourceReference Source);

public sealed record GeneratedSourceFileModel(
    string HintName,
    GeneratedMethodModel Method,
    ImmutableArray<ServiceRegistrationModel> Registrations,
    ImmutableArray<DiagnosticState> Diagnostics);
