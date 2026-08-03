using System;
using System.Collections.Generic;

namespace Injectlynx.Plugins;

public sealed record InjectlynxPluginManifest(
    string Name,
    string Version,
    string Description,
    string EntryAssembly,
    string TypeName,
    string? TargetFramework,
    string? Sha256,
    IReadOnlyList<string> Dependencies,
    bool Enabled,
    int Order)
{
    public static InjectlynxPluginManifest Empty { get; } = new(
    string.Empty,
    string.Empty,
    string.Empty,
    string.Empty,
    string.Empty,
    null,
    null,
    Array.Empty<string>(),
        true,
        0);
}
