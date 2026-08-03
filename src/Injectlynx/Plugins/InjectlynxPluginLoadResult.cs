using System;
using System.Collections.Generic;
using System.Linq;

namespace Injectlynx.Plugins;

public sealed record InjectlynxLoadedPlugin(
    InjectlynxPluginManifest Manifest,
    string ManifestPath,
    string AssemblyPath,
    IInjectlynxPlugin Instance,
    Action? Unload);

public sealed record InjectlynxPluginLoadResult(
    IReadOnlyList<InjectlynxLoadedPlugin> Plugins,
    IReadOnlyList<InjectlynxPluginDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(static item => item.Severity == InjectlynxPluginDiagnosticSeverity.Error);
}
