namespace Injectlynx.Core.Models;

public sealed record SourceReference(
    string FilePath,
    int Line,
    int Column)
{
    public static SourceReference None { get; } = new(string.Empty, 0, 0);
}
