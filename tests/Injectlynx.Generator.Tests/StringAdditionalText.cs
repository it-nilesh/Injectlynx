using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Injectlynx.Generator.Tests;

internal sealed class StringAdditionalText(string path, string text) : AdditionalText
{
    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default) =>
        SourceText.From(text);
}
