using System;
using System.Collections.Generic;

namespace Injectlynx.Plugins;

public sealed class InjectlynxPluginLoadOptions
{
    public IList<string> PluginDirectories { get; } = new List<string>();

    public IList<string> ManifestFiles { get; } = new List<string>();

    public IList<string> PluginAssemblies { get; } = new List<string>();

    public IList<string> ConfigurationFiles { get; } = new List<string>();

    public ISet<string> DisabledPlugins { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool ThrowOnError { get; set; }

    public bool DiscoverUnmanifestedAssemblies { get; set; } = true;

    public bool UseCollectibleLoadContext { get; set; } = true;

    public void AddDirectory(string path) => PluginDirectories.Add(path);

    public void AddManifest(string path) => ManifestFiles.Add(path);

    public void AddAssembly(string path) => PluginAssemblies.Add(path);

    public void AddConfiguration(string path) => ConfigurationFiles.Add(path);

    public void DisablePlugin(string name) => DisabledPlugins.Add(name);
}
