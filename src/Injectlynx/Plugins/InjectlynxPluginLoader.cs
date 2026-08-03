using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace Injectlynx.Plugins;

public static class InjectlynxPluginLoader
{
    public static InjectlynxPluginLoadResult Load(IServiceCollection services, InjectlynxPluginLoadOptions options)
    {
        var diagnostics = new List<InjectlynxPluginDiagnostic>();
        ApplyConfigurationFiles(options, diagnostics);
        var loadedPlugins = new List<InjectlynxLoadedPlugin>();
        var registeredServiceTypes = new HashSet<string>(
            services.Select(static descriptor => GetServiceKey(descriptor.ServiceType)),
            StringComparer.Ordinal);
        var candidates = OrderCandidates(DiscoverManifestFiles(options, diagnostics)
            .Select(path => LoadManifest(path, diagnostics))
            .Where(static item => item is not null)
            .Select(static item => item!)
            .Where(item => ShouldLoad(item.Manifest, options, diagnostics, item.Path))
            .Select(item => new PluginCandidate(
                item.Manifest,
                item.Path,
                Path.GetFullPath(Path.Combine(Path.GetDirectoryName(item.Path) ?? Directory.GetCurrentDirectory(), item.Manifest.EntryAssembly)),
                item.Manifest.TypeName))
            .Concat(DiscoverUnmanifestedPluginCandidates(options, diagnostics))
            .Where(item => ShouldLoad(item.Manifest, options, diagnostics, item.ManifestPath)), diagnostics)
            .ToArray();

        foreach (var item in candidates)
        {
            var loaded = LoadPlugin(item, services, options, diagnostics, registeredServiceTypes);
            if (loaded is not null)
            {
                loadedPlugins.Add(loaded);
            }
        }

        if (options.ThrowOnError && diagnostics.Any(static item => item.Severity == InjectlynxPluginDiagnosticSeverity.Error))
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics
                .Where(static item => item.Severity == InjectlynxPluginDiagnosticSeverity.Error)
                .Select(static item => item.Code + ": " + item.Message)));
        }

        return new InjectlynxPluginLoadResult(loadedPlugins, diagnostics);
    }

    public static void ApplyConfigurationFiles(InjectlynxPluginLoadOptions options, IList<InjectlynxPluginDiagnostic>? diagnostics = null)
    {
        foreach (var configurationFile in options.ConfigurationFiles.ToArray())
        {
            var path = Path.GetFullPath(configurationFile);
            if (!File.Exists(path))
            {
                diagnostics?.Add(new InjectlynxPluginDiagnostic(
                    InjectlynxPluginDiagnosticSeverity.Error,
                    "INJP019",
                    "Plugin configuration file was not found: " + path,
                    path));
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                AddStrings(root, "pluginDirectories", options.AddDirectory, path);
                AddStrings(root, "manifestFiles", options.AddManifest, path);
                AddStrings(root, "pluginAssemblies", options.AddAssembly, path);
                AddNames(root, "disabledPlugins", options.DisablePlugin);

                if (GetOptionalBoolean(root, "discoverUnmanifestedAssemblies") is { } discoverUnmanifestedAssemblies)
                {
                    options.DiscoverUnmanifestedAssemblies = discoverUnmanifestedAssemblies;
                }

                if (GetOptionalBoolean(root, "useCollectibleLoadContext") is { } useCollectibleLoadContext)
                {
                    options.UseCollectibleLoadContext = useCollectibleLoadContext;
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                diagnostics?.Add(new InjectlynxPluginDiagnostic(
                    InjectlynxPluginDiagnosticSeverity.Error,
                    "INJP020",
                    "Plugin configuration is invalid: " + ex.Message,
                    path));
            }
        }
    }

    public static IReadOnlyList<string> DiscoverManifestFiles(InjectlynxPluginLoadOptions options, IList<InjectlynxPluginDiagnostic>? diagnostics = null)
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestFile in options.ManifestFiles)
        {
            var path = Path.GetFullPath(manifestFile);
            if (File.Exists(path))
            {
                files.Add(path);
                continue;
            }

            diagnostics?.Add(new InjectlynxPluginDiagnostic(
                InjectlynxPluginDiagnosticSeverity.Error,
                "INJP001",
                "Plugin manifest file was not found: " + path,
                path));
        }

        foreach (var directory in options.PluginDirectories)
        {
            var path = Path.GetFullPath(directory);
            if (!Directory.Exists(path))
            {
                diagnostics?.Add(new InjectlynxPluginDiagnostic(
                    InjectlynxPluginDiagnosticSeverity.Error,
                    "INJP002",
                    "Plugin directory was not found: " + path));
                continue;
            }

            foreach (var manifest in Directory.EnumerateFiles(path, "injectlynx.plugin.json", SearchOption.AllDirectories))
            {
                files.Add(Path.GetFullPath(manifest));
            }
        }

        return files.ToArray();
    }

    public static IReadOnlyList<string> DiscoverAssemblyFiles(InjectlynxPluginLoadOptions options, IList<InjectlynxPluginDiagnostic>? diagnostics = null)
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assemblyFile in options.PluginAssemblies)
        {
            var path = Path.GetFullPath(assemblyFile);
            if (File.Exists(path))
            {
                files.Add(path);
                continue;
            }

            diagnostics?.Add(new InjectlynxPluginDiagnostic(
                InjectlynxPluginDiagnosticSeverity.Error,
                "INJP014",
                "Plugin assembly file was not found: " + path,
                path));
        }

        if (!options.DiscoverUnmanifestedAssemblies)
        {
            return files.ToArray();
        }

        foreach (var directory in options.PluginDirectories)
        {
            var path = Path.GetFullPath(directory);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var assembly in Directory.EnumerateFiles(path, "*.dll", SearchOption.AllDirectories))
            {
                var assemblyDirectory = Path.GetDirectoryName(assembly) ?? path;
                if (File.Exists(Path.Combine(assemblyDirectory, "injectlynx.plugin.json")))
                {
                    continue;
                }

                files.Add(Path.GetFullPath(assembly));
            }
        }

        return files.ToArray();
    }

    public static InjectlynxPluginManifest ReadManifest(string manifestPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var dependencies = root.TryGetProperty("dependencies", out var dependencyElement) &&
            dependencyElement.ValueKind == JsonValueKind.Array
            ? dependencyElement.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.String)
                .Select(static item => item.GetString()!)
                .ToArray()
            : Array.Empty<string>();

        return new InjectlynxPluginManifest(
            GetOptionalString(root, "name") ?? string.Empty,
            GetOptionalString(root, "version") ?? "0.0.0",
            GetOptionalString(root, "description") ?? string.Empty,
            GetOptionalString(root, "entryAssembly") ?? string.Empty,
            GetOptionalString(root, "typeName") ?? string.Empty,
            GetOptionalString(root, "targetFramework"),
            GetOptionalString(root, "sha256"),
            dependencies,
            GetOptionalBoolean(root, "enabled") ?? true,
            GetOptionalInt32(root, "order") ?? 0);
    }

    private static ManifestItem? LoadManifest(string path, IList<InjectlynxPluginDiagnostic> diagnostics)
    {
        try
        {
            var manifest = ReadManifest(path);
            var diagnosticCount = diagnostics.Count;
            ValidateManifest(manifest, path, diagnostics);
            return diagnostics.Count == diagnosticCount
                ? new ManifestItem(path, manifest)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException)
        {
            diagnostics.Add(new InjectlynxPluginDiagnostic(
                InjectlynxPluginDiagnosticSeverity.Error,
                "INJP003",
                "Plugin manifest is invalid: " + ex.Message,
                path));
            return null;
        }
    }

    private static void ValidateManifest(InjectlynxPluginManifest manifest, string path, IList<InjectlynxPluginDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Error, "INJP004", "Plugin manifest name is required.", path));
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Error, "INJP005", "Plugin manifest entryAssembly is required.", path, manifest.Name));
        }

        if (string.IsNullOrWhiteSpace(manifest.TypeName))
        {
            diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Error, "INJP006", "Plugin manifest typeName is required.", path, manifest.Name));
        }
    }

    private static bool ShouldLoad(
        InjectlynxPluginManifest manifest,
        InjectlynxPluginLoadOptions options,
        IList<InjectlynxPluginDiagnostic> diagnostics,
        string path)
    {
        if (!manifest.Enabled)
        {
            diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Info, "INJP007", "Plugin is disabled in its manifest.", path, manifest.Name));
            return false;
        }

        if (options.DisabledPlugins.Contains(manifest.Name))
        {
            diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Info, "INJP008", "Plugin is disabled by host options.", path, manifest.Name));
            return false;
        }

        return true;
    }

    private static IEnumerable<PluginCandidate> DiscoverUnmanifestedPluginCandidates(
        InjectlynxPluginLoadOptions options,
        IList<InjectlynxPluginDiagnostic> diagnostics)
    {
        var candidates = new List<PluginCandidate>();
        foreach (var assemblyPath in DiscoverAssemblyFiles(options, diagnostics))
        {
            LoadedAssembly? loaded = null;
            try
            {
                loaded = LoadAssembly(assemblyPath, options);
                foreach (var type in loaded.Assembly.GetTypes()
                    .Where(static type => typeof(IInjectlynxPlugin).IsAssignableFrom(type) &&
                        type is { IsAbstract: false, IsInterface: false } &&
                        type.GetConstructor(Type.EmptyTypes) is not null)
                    .OrderBy(static type => type.FullName, StringComparer.Ordinal))
                {
                    var plugin = (IInjectlynxPlugin)Activator.CreateInstance(type)!;
                    var manifest = CreateManifestFromPlugin(plugin, type, assemblyPath, loaded.Assembly.GetName().Version?.ToString() ?? "0.0.0");

                    candidates.Add(new PluginCandidate(manifest, assemblyPath, assemblyPath, type.FullName ?? type.Name));
                }
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException or ReflectionTypeLoadException)
            {
                diagnostics.Add(new InjectlynxPluginDiagnostic(
                    InjectlynxPluginDiagnosticSeverity.Error,
                    "INJP015",
                    "Plugin assembly scan failed: " + ex.Message,
                    assemblyPath));
            }
            finally
            {
                loaded?.Unload?.Invoke();
            }
        }

        return candidates;
    }

    private static InjectlynxLoadedPlugin? LoadPlugin(
        PluginCandidate candidate,
        IServiceCollection services,
        InjectlynxPluginLoadOptions options,
        IList<InjectlynxPluginDiagnostic> diagnostics,
        ISet<string> registeredServiceTypes)
    {
        var manifest = candidate.Manifest;
        var manifestPath = candidate.ManifestPath;
        var assemblyPath = candidate.AssemblyPath;
        if (!File.Exists(assemblyPath))
        {
            diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Error, "INJP009", "Plugin entry assembly was not found: " + assemblyPath, manifestPath, manifest.Name));
            return null;
        }

        try
        {
            if (!ValidateAssemblyHash(manifest, manifestPath, assemblyPath, diagnostics) ||
                !ValidateTargetFramework(manifest, manifestPath, diagnostics))
            {
                return null;
            }

            var loaded = LoadAssembly(assemblyPath, options);
            var type = loaded.Assembly.GetType(candidate.TypeName, throwOnError: false);
            if (type is null)
            {
                diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Error, "INJP010", "Plugin type was not found: " + candidate.TypeName, manifestPath, manifest.Name));
                loaded.Unload?.Invoke();
                return null;
            }

            if (!typeof(IInjectlynxPlugin).IsAssignableFrom(type))
            {
                diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Error, "INJP011", "Plugin type must implement IInjectlynxPlugin: " + candidate.TypeName, manifestPath, manifest.Name));
                loaded.Unload?.Invoke();
                return null;
            }

            var instance = (IInjectlynxPlugin)Activator.CreateInstance(type)!;
            var beforeCount = services.Count;
            instance.ConfigureServices(services);
            foreach (var descriptor in services.Skip(beforeCount))
            {
                if (!registeredServiceTypes.Add(GetServiceKey(descriptor.ServiceType)))
                {
                    diagnostics.Add(new InjectlynxPluginDiagnostic(
                        InjectlynxPluginDiagnosticSeverity.Warning,
                        "INJP013",
                        "Plugin registered a service contract that was already registered: " + descriptor.ServiceType.FullName,
                        manifestPath,
                        manifest.Name));
                }
            }

            diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Info, "INJP000", "Plugin loaded successfully.", manifestPath, manifest.Name));
            return new InjectlynxLoadedPlugin(manifest, manifestPath, assemblyPath, instance, loaded.Unload);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException or TargetInvocationException or TypeLoadException or MissingMethodException)
        {
            diagnostics.Add(new InjectlynxPluginDiagnostic(InjectlynxPluginDiagnosticSeverity.Error, "INJP012", "Plugin load failed: " + ex.Message, manifestPath, manifest.Name));
            return null;
        }
    }

    private static LoadedAssembly LoadAssembly(string assemblyPath, InjectlynxPluginLoadOptions options)
    {
#if NET8_0_OR_GREATER
        var context = new InjectlynxPluginLoadContext(assemblyPath, options.UseCollectibleLoadContext);
        return new LoadedAssembly(
            context.LoadFromAssemblyPath(assemblyPath),
            options.UseCollectibleLoadContext ? context.Unload : null);
#else
        return new LoadedAssembly(Assembly.LoadFrom(assemblyPath), null);
#endif
    }

    private static string? GetOptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool? GetOptionalBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;

    private static int? GetOptionalInt32(JsonElement root, string name) =>
        root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;

    private static InjectlynxPluginManifest CreateManifestFromPlugin(
        IInjectlynxPlugin plugin,
        Type pluginType,
        string assemblyPath,
        string version)
    {
        var typeName = pluginType.FullName ?? pluginType.Name;
        return new InjectlynxPluginManifest(
            string.IsNullOrWhiteSpace(plugin.Name) ? typeName : plugin.Name,
            version,
            plugin.Description,
            Path.GetFileName(assemblyPath),
            typeName,
            null,
            null,
            Array.Empty<string>(),
            true,
            plugin.Order);
    }

    private static bool ValidateAssemblyHash(
        InjectlynxPluginManifest manifest,
        string manifestPath,
        string assemblyPath,
        IList<InjectlynxPluginDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            return true;
        }

        using var stream = File.OpenRead(assemblyPath);
        using var sha256 = SHA256.Create();
        var hash = ToHex(sha256.ComputeHash(stream));
        if (string.Equals(hash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        diagnostics.Add(new InjectlynxPluginDiagnostic(
            InjectlynxPluginDiagnosticSeverity.Error,
            "INJP021",
            "Plugin assembly SHA-256 hash did not match the manifest.",
            manifestPath,
            manifest.Name));
        return false;
    }

    private static bool ValidateTargetFramework(
        InjectlynxPluginManifest manifest,
        string manifestPath,
        IList<InjectlynxPluginDiagnostic> diagnostics)
    {
        var targetFramework = manifest.TargetFramework ?? string.Empty;
        if (targetFramework.Length == 0 ||
            !targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var versionText = targetFramework.Substring("net".Length);
        var dotIndex = versionText.IndexOf('.');
        if (dotIndex >= 0)
        {
            versionText = versionText.Substring(0, dotIndex);
        }

        if (!int.TryParse(versionText, out var requiredMajor) ||
            requiredMajor <= Environment.Version.Major)
        {
            return true;
        }

        diagnostics.Add(new InjectlynxPluginDiagnostic(
            InjectlynxPluginDiagnosticSeverity.Error,
            "INJP016",
            "Plugin targets " + targetFramework + " but the host runtime is " + Environment.Version + ".",
            manifestPath,
            manifest.Name));
        return false;
    }

    private static IReadOnlyList<PluginCandidate> OrderCandidates(
        IEnumerable<PluginCandidate> candidates,
        IList<InjectlynxPluginDiagnostic> diagnostics)
    {
        var ordered = candidates
            .OrderBy(static item => item.Manifest.Order)
            .ThenBy(static item => item.Manifest.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var byName = ordered.ToDictionary(static item => item.Manifest.Name, StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PluginCandidate>();

        foreach (var candidate in ordered)
        {
            Visit(candidate);
        }

        return result;

        void Visit(PluginCandidate candidate)
        {
            if (visited.Contains(candidate.Manifest.Name))
            {
                return;
            }

            if (!visiting.Add(candidate.Manifest.Name))
            {
                diagnostics.Add(new InjectlynxPluginDiagnostic(
                    InjectlynxPluginDiagnosticSeverity.Error,
                    "INJP018",
                    "Plugin dependency cycle includes " + candidate.Manifest.Name + ".",
                    candidate.ManifestPath,
                    candidate.Manifest.Name));
                return;
            }

            foreach (var dependency in candidate.Manifest.Dependencies)
            {
                if (byName.TryGetValue(dependency, out var dependencyCandidate))
                {
                    Visit(dependencyCandidate);
                    continue;
                }

                diagnostics.Add(new InjectlynxPluginDiagnostic(
                    InjectlynxPluginDiagnosticSeverity.Error,
                    "INJP017",
                    "Plugin dependency was not discovered: " + dependency,
                    candidate.ManifestPath,
                    candidate.Manifest.Name));
            }

            visiting.Remove(candidate.Manifest.Name);
            visited.Add(candidate.Manifest.Name);
            result.Add(candidate);
        }
    }

    private static void AddStrings(JsonElement root, string name, Action<string> add, string configurationPath)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var baseDirectory = Path.GetDirectoryName(configurationPath) ?? Directory.GetCurrentDirectory();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                continue;
            }

            var value = item.GetString()!;
            add(Path.IsPathRooted(value) ? value : Path.GetFullPath(Path.Combine(baseDirectory, value)));
        }
    }

    private static void AddNames(JsonElement root, string name, Action<string> add)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                add(item.GetString()!);
            }
        }
    }

    private static string ToHex(byte[] bytes)
    {
        var chars = new char[bytes.Length * 2];
        const string alphabet = "0123456789abcdef";
        for (var index = 0; index < bytes.Length; index++)
        {
            chars[index * 2] = alphabet[bytes[index] >> 4];
            chars[index * 2 + 1] = alphabet[bytes[index] & 0xF];
        }

        return new string(chars);
    }

    private static string GetServiceKey(Type serviceType) =>
        serviceType.Assembly.GetName().Name + ":" + serviceType.FullName;

    private sealed record ManifestItem(string Path, InjectlynxPluginManifest Manifest);

    private sealed record PluginCandidate(
        InjectlynxPluginManifest Manifest,
        string ManifestPath,
        string AssemblyPath,
        string TypeName);

    private sealed record LoadedAssembly(Assembly Assembly, Action? Unload);

#if NET8_0_OR_GREATER
    private sealed class InjectlynxPluginLoadContext(string assemblyPath, bool isCollectible) : AssemblyLoadContext(isCollectible)
    {
        private readonly AssemblyDependencyResolver resolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (IsSharedAssembly(assemblyName.Name))
            {
                return null;
            }

            var path = resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        private static bool IsSharedAssembly(string? name) =>
            name is "Injectlynx" ||
            name is not null && (
                name.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) ||
                name.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal));
    }
#endif
}
