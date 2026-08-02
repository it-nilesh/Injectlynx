using Injectlynx.Plugins;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace Injectlynx.Plugins.Tests;

public sealed class InjectlynxPluginLoaderTests
{
    [Fact]
    public void ReadManifest_LoadsManifestDefaults()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.Write("injectlynx.plugin.json", """
        {
          "name": "Sample",
          "description": "Sample plugin",
          "entryAssembly": "Sample.dll",
          "typeName": "Sample.Plugin"
        }
        """);

        var manifest = InjectlynxPluginLoader.ReadManifest(manifestPath);

        Assert.Equal("Sample", manifest.Name);
        Assert.Equal("0.0.0", manifest.Version);
        Assert.Equal("Sample plugin", manifest.Description);
        Assert.True(manifest.Enabled);
        Assert.Equal(0, manifest.Order);
    }

    [Fact]
    public void DiscoverManifestFiles_ReadsPluginDirectoriesAndExplicitManifests()
    {
        using var workspace = new TemporaryWorkspace();
        var directoryManifest = workspace.Write(Path.Combine("plugins", "orders", "injectlynx.plugin.json"), "{}");
        var explicitManifest = workspace.Write("custom.json", "{}");
        var options = new InjectlynxPluginLoadOptions();
        options.AddDirectory(Path.Combine(workspace.Root, "plugins"));
        options.AddManifest(explicitManifest);

        var manifests = InjectlynxPluginLoader.DiscoverManifestFiles(options);

        Assert.Contains(directoryManifest, manifests);
        Assert.Contains(explicitManifest, manifests);
    }

    [Fact]
    public void DiscoverAssemblyFiles_ReadsPluginDirectoriesAndExplicitAssemblies()
    {
        using var workspace = new TemporaryWorkspace();
        var assemblyName = Path.GetFileName(typeof(SamplePlugin).Assembly.Location);
        var directoryAssembly = Path.Combine(workspace.Root, "plugins", assemblyName);
        Directory.CreateDirectory(Path.GetDirectoryName(directoryAssembly)!);
        File.Copy(typeof(SamplePlugin).Assembly.Location, directoryAssembly, overwrite: true);
        var explicitAssembly = Path.Combine(workspace.Root, "custom.dll");
        File.Copy(typeof(SamplePlugin).Assembly.Location, explicitAssembly, overwrite: true);
        var options = new InjectlynxPluginLoadOptions();
        options.AddDirectory(Path.Combine(workspace.Root, "plugins"));
        options.AddAssembly(explicitAssembly);

        var assemblies = InjectlynxPluginLoader.DiscoverAssemblyFiles(options);

        Assert.Contains(directoryAssembly, assemblies);
        Assert.Contains(explicitAssembly, assemblies);
    }

    [Fact]
    public void Load_RegistersServicesFromManifestPlugin()
    {
        using var workspace = new TemporaryWorkspace();
        var assemblyName = Path.GetFileName(typeof(SamplePlugin).Assembly.Location);
        var manifestPath = workspace.Write("injectlynx.plugin.json", $$"""
        {
          "name": "Sample",
          "version": "1.0.0",
          "entryAssembly": "{{assemblyName}}",
          "typeName": "{{typeof(SamplePlugin).FullName}}",
          "dependencies": [],
          "order": 10
        }
        """);
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(workspace.Root, assemblyName), overwrite: true);
        var services = new ServiceCollection();
        var options = new InjectlynxPluginLoadOptions();
        options.AddManifest(manifestPath);
        options.UseCollectibleLoadContext = false;

        var result = InjectlynxPluginLoader.Load(services, options);

        Assert.False(result.HasErrors);
        Assert.Single(result.Plugins);
        Assert.Contains(services, static descriptor => descriptor.ServiceType.FullName == typeof(ISamplePluginService).FullName);
    }

    [Fact]
    public void Load_RegistersServicesFromAssemblyPluginWithoutManifest()
    {
        using var workspace = new TemporaryWorkspace();
        var assemblyName = Path.GetFileName(typeof(SamplePlugin).Assembly.Location);
        var assemblyPath = Path.Combine(workspace.Root, assemblyName);
        File.Copy(typeof(SamplePlugin).Assembly.Location, assemblyPath, overwrite: true);
        var services = new ServiceCollection();
        var options = new InjectlynxPluginLoadOptions();
        options.AddAssembly(assemblyPath);
        options.UseCollectibleLoadContext = false;

        var result = InjectlynxPluginLoader.Load(services, options);

        Assert.False(result.HasErrors);
        Assert.Single(result.Plugins);
        Assert.Equal("SamplePlugin", result.Plugins[0].Manifest.Name);
        Assert.Equal("Registers sample test services.", result.Plugins[0].Manifest.Description);
        Assert.Equal(20, result.Plugins[0].Manifest.Order);
        Assert.Equal(typeof(SamplePlugin).FullName, result.Plugins[0].Manifest.TypeName);
        Assert.Contains(services, static descriptor => descriptor.ServiceType.FullName == typeof(ISamplePluginService).FullName);
    }

    [Fact]
    public void Load_ReportsMissingAssembly()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.Write("injectlynx.plugin.json", """
        {
          "name": "Missing",
          "entryAssembly": "Missing.dll",
          "typeName": "Missing.Plugin"
        }
        """);
        var services = new ServiceCollection();
        var options = new InjectlynxPluginLoadOptions();
        options.AddManifest(manifestPath);

        var result = InjectlynxPluginLoader.Load(services, options);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "INJP009");
    }

    [Fact]
    public void Load_ReportsManifestValidationFailures()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.Write("injectlynx.plugin.json", """
        {
          "name": "",
          "entryAssembly": "",
          "typeName": ""
        }
        """);
        var services = new ServiceCollection();
        var options = new InjectlynxPluginLoadOptions();
        options.AddManifest(manifestPath);

        var result = InjectlynxPluginLoader.Load(services, options);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "INJP004");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "INJP005");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "INJP006");
        Assert.Empty(result.Plugins);
    }

    [Fact]
    public void Load_ReportsDuplicateServiceRegistration()
    {
        using var workspace = new TemporaryWorkspace();
        var assemblyName = Path.GetFileName(typeof(SamplePlugin).Assembly.Location);
        var manifestPath = workspace.Write("injectlynx.plugin.json", $$"""
        {
          "name": "Sample",
          "entryAssembly": "{{assemblyName}}",
          "typeName": "{{typeof(SamplePlugin).FullName}}"
        }
        """);
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(workspace.Root, assemblyName), overwrite: true);
        var services = new ServiceCollection();
        services.AddSingleton<ISamplePluginService, SamplePluginService>();
        var options = new InjectlynxPluginLoadOptions();
        options.AddManifest(manifestPath);
        options.UseCollectibleLoadContext = false;

        var result = InjectlynxPluginLoader.Load(services, options);

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "INJP013");
    }

    [Fact]
    public void Load_ReadsPluginConfigurationFile()
    {
        using var workspace = new TemporaryWorkspace();
        var assemblyName = Path.GetFileName(typeof(SamplePlugin).Assembly.Location);
        var pluginDirectory = Path.Combine(workspace.Root, "plugins");
        Directory.CreateDirectory(pluginDirectory);
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(pluginDirectory, assemblyName), overwrite: true);
        var configPath = workspace.Write("injectlynx.plugins.json", """
        {
          "pluginDirectories": ["plugins"]
        }
        """);
        var services = new ServiceCollection();
        var options = new InjectlynxPluginLoadOptions();
        options.AddConfiguration(configPath);
        options.UseCollectibleLoadContext = false;

        var result = InjectlynxPluginLoader.Load(services, options);

        Assert.False(result.HasErrors);
        Assert.Single(result.Plugins);
    }

    [Fact]
    public void Load_OrdersPluginsAfterManifestDependencies()
    {
        using var workspace = new TemporaryWorkspace();
        var assemblyName = Path.GetFileName(typeof(SamplePlugin).Assembly.Location);
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(workspace.Root, assemblyName), overwrite: true);
        var firstManifest = workspace.Write("first.plugin.json", $$"""
        {
          "name": "First",
          "entryAssembly": "{{assemblyName}}",
          "typeName": "{{typeof(SamplePlugin).FullName}}",
          "order": 20
        }
        """);
        var secondManifest = workspace.Write("second.plugin.json", $$"""
        {
          "name": "Second",
          "entryAssembly": "{{assemblyName}}",
          "typeName": "{{typeof(SamplePlugin).FullName}}",
          "dependencies": ["First"],
          "order": 0
        }
        """);
        var services = new ServiceCollection();
        var options = new InjectlynxPluginLoadOptions();
        options.AddManifest(secondManifest);
        options.AddManifest(firstManifest);
        options.UseCollectibleLoadContext = false;

        var result = InjectlynxPluginLoader.Load(services, options);

        Assert.False(result.HasErrors);
        Assert.Equal(["First", "Second"], result.Plugins.Select(static plugin => plugin.Manifest.Name).ToArray());
    }

    [Fact]
    public void Load_ReportsFutureTargetFramework()
    {
        using var workspace = new TemporaryWorkspace();
        var assemblyName = Path.GetFileName(typeof(SamplePlugin).Assembly.Location);
        var manifestPath = workspace.Write("injectlynx.plugin.json", $$"""
        {
          "name": "Future",
          "entryAssembly": "{{assemblyName}}",
          "typeName": "{{typeof(SamplePlugin).FullName}}",
          "targetFramework": "net99.0"
        }
        """);
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(workspace.Root, assemblyName), overwrite: true);
        var options = new InjectlynxPluginLoadOptions();
        options.AddManifest(manifestPath);

        var result = InjectlynxPluginLoader.Load(new ServiceCollection(), options);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "INJP016");
    }

    [Fact]
    public void Load_ReportsHashMismatch()
    {
        using var workspace = new TemporaryWorkspace();
        var assemblyName = Path.GetFileName(typeof(SamplePlugin).Assembly.Location);
        var manifestPath = workspace.Write("injectlynx.plugin.json", $$"""
        {
          "name": "Hashed",
          "entryAssembly": "{{assemblyName}}",
          "typeName": "{{typeof(SamplePlugin).FullName}}",
          "sha256": "000000"
        }
        """);
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(workspace.Root, assemblyName), overwrite: true);
        var options = new InjectlynxPluginLoadOptions();
        options.AddManifest(manifestPath);

        var result = InjectlynxPluginLoader.Load(new ServiceCollection(), options);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "INJP021");
    }

    [Fact]
    public void Load_ExposesUnloadHandleForCollectibleContext()
    {
        using var workspace = new TemporaryWorkspace();
        var assemblyName = Path.GetFileName(typeof(SamplePlugin).Assembly.Location);
        var hash = ComputeSha256(typeof(SamplePlugin).Assembly.Location);
        var manifestPath = workspace.Write("injectlynx.plugin.json", $$"""
        {
          "name": "Collectible",
          "entryAssembly": "{{assemblyName}}",
          "typeName": "{{typeof(SamplePlugin).FullName}}",
          "sha256": "{{hash}}"
        }
        """);
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(workspace.Root, assemblyName), overwrite: true);
        var options = new InjectlynxPluginLoadOptions();
        options.AddManifest(manifestPath);

        var result = InjectlynxPluginLoader.Load(new ServiceCollection(), options);

        Assert.False(result.HasErrors);
        var plugin = Assert.Single(result.Plugins);
        Assert.NotNull(plugin.Unload);
        plugin.Unload?.Invoke();
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        return string.Concat(sha256.ComputeHash(stream).Select(static item => item.ToString("x2")));
    }

    public interface ISamplePluginService
    {
    }

    public sealed class SamplePluginService : ISamplePluginService
    {
    }

    public sealed class SamplePlugin : IInjectlynxPlugin
    {
        public string Name => "SamplePlugin";

        public string Description => "Registers sample test services.";

        public int Order => 20;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ISamplePluginService, SamplePluginService>();
        }
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "InjectlynxPluginTests_" + Guid.NewGuid().ToString("N"));

        public TemporaryWorkspace()
        {
            Directory.CreateDirectory(Root);
        }

        public string Write(string relativePath, string contents)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
