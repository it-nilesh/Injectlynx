namespace Injectlynx.Plugins;

public enum InjectlynxPluginDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record InjectlynxPluginDiagnostic(
    InjectlynxPluginDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? ManifestPath = null,
    string? PluginName = null);
