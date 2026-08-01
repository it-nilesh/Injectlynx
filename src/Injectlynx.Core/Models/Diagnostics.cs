using System.Collections.Immutable;

namespace Injectlynx.Core.Models;

public sealed record DiagnosticState(
    string Id,
    string Title,
    string Message,
    DiagnosticSeverityModel Severity,
    SourceReference Source,
    ImmutableArray<SourceReference> RelatedSources,
    string? DocumentationUrl,
    bool IsUnsafeToSuppress)
{
    public static DiagnosticState None { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        DiagnosticSeverityModel.Hidden,
        SourceReference.None,
        ImmutableArray<SourceReference>.Empty,
        null,
        false);
}
