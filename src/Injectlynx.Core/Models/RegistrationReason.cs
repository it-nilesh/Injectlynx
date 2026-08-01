using System.Collections.Immutable;

namespace Injectlynx.Core.Models;

public sealed record RegistrationReason(
    RegistrationReasonKind Kind,
    string Summary,
    ImmutableArray<string> Details)
{
    public static RegistrationReason Create(
        RegistrationReasonKind kind,
        string summary,
        ImmutableArray<string>? details = null) =>
        new(kind, summary, details ?? ImmutableArray<string>.Empty);
}
