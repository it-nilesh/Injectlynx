# Dynamic Plugin Loading

Injectlynx defaults to compile-time dependency injection. The generator reads your C# convention DSL during build and emits direct `IServiceCollection` calls. That path is deterministic, easy to inspect, and friendly to Native AOT.

Dynamic plugin loading is different. It is an opt-in runtime feature for applications that must discover functionality after deployment, such as modular platforms, tenant-specific extensions, marketplace integrations, or internal tools where teams ship independent feature assemblies. The API lives in the main `Injectlynx` package so plugin authors and host applications do not need to find a second package.

## When To Use It

Use dynamic plugins when the host cannot know every implementation at compile time. Examples include customer-specific integrations, optional workflow steps, independently deployed reporting modules, or feature packs that are enabled per environment.

Stay with compile-time Injectlynx conventions when services are known at build time. Compile-time registration is faster to validate, easier to trim, and better suited for Native AOT.

## Host Setup

The host chooses exactly where plugins may be loaded from:

```csharp
using Injectlynx.Plugins;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

var result = services.AddInjectlynxPlugins(options =>
{
    options.AddDirectory("plugins");
    options.AddConfiguration("injectlynx.plugins.json");
    options.DisablePlugin("LegacyReports");
    options.ThrowOnError = true;
});

foreach (var diagnostic in result.Diagnostics)
{
    Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
}
```

`AddInjectlynxPlugins` returns loaded plugin metadata and diagnostics. Hosts can fail startup with `ThrowOnError`, inspect warnings, or expose plugin status in their own health checks.

## Plugin Contract

A plugin implements `IInjectlynxPlugin` from the main package:

```csharp
using Injectlynx.Plugins;
using Microsoft.Extensions.DependencyInjection;

public sealed class GreetingPlugin : IInjectlynxPlugin
{
    public string Name => "GreetingPlugin";

    public string Description => "Registers greeting services for the host app.";

    public int Order => 0;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPluginGreetingService, PluginGreetingService>();
    }
}
```

The plugin contract includes metadata so a DLL can be self-describing without a JSON file:

- `Name`: stable plugin identity used for diagnostics and `DisablePlugin`.
- `Description`: human-friendly plugin purpose for status pages and tooling.
- `Order`: deterministic load order when multiple plugins are discovered.

The plugin contract intentionally receives `IServiceCollection`. That keeps runtime plugins compatible with Microsoft DI and lets teams reuse familiar lifetimes, options, hosted services, and factory registrations.

## Manifest Optional

The simplest plugin does not need JSON. If a configured directory contains a DLL with a public concrete type that implements `IInjectlynxPlugin`, Injectlynx can discover and load it:

```csharp
var result = services.AddInjectlynxPlugins(options =>
{
    options.AddDirectory("plugins/GreetingPlugin");
});
```

Use `options.AddAssembly("plugins/GreetingPlugin/PluginSample.dll")` when you want to point at one DLL directly.

Hosts can also load plugin settings from JSON configuration:

```json
{
  "pluginDirectories": ["plugins"],
  "manifestFiles": ["plugins/Reports/injectlynx.plugin.json"],
  "pluginAssemblies": ["plugins/GreetingPlugin/PluginSample.dll"],
  "disabledPlugins": ["LegacyReports"],
  "discoverUnmanifestedAssemblies": true,
  "useCollectibleLoadContext": true
}
```

```csharp
var result = services.AddInjectlynxPlugins(options =>
{
    options.AddConfiguration("injectlynx.plugins.json");
    options.ThrowOnError = true;
});
```

Configuration paths are resolved relative to the configuration file. Direct API calls and configuration files can be combined, which lets a host keep trusted plugin locations in app settings and add environment-specific overrides in code.

The loader still supports `injectlynx.plugin.json` when you need metadata, explicit type selection, disabled state, or ordering.

Example manifest:

```json
{
  "name": "GreetingPlugin",
  "version": "1.0.0",
  "description": "Registers greeting services for the host app.",
  "entryAssembly": "PluginSample.dll",
  "typeName": "PluginSample.GreetingPlugin",
  "targetFramework": "net8.0",
  "dependencies": [],
  "sha256": "optional-lower-or-upper-case-assembly-sha256",
  "enabled": true,
  "order": 0
}
```

The loader discovers manifests from configured directories and explicit manifest paths. Plugin assemblies are loaded from the manifest folder. Plugin-local dependencies are resolved from the plugin output folder, while the core `Injectlynx` and Microsoft DI assemblies are shared with the host.

When a manifest is present, its `name`, `description`, `dependencies`, and `order` are used for discovery and sorting. Without a manifest, Injectlynx reads `name`, `description`, and `order` from the `IInjectlynxPlugin` implementation.

## Ordering And Compatibility

Plugin load order is deterministic:

- Manifest dependencies load before plugins that depend on them.
- Independent plugins are ordered by `order`, then by name.
- Missing dependencies and dependency cycles are reported as diagnostics before registration continues.

The loader checks manifest target frameworks before loading. A plugin targeting a newer major .NET version than the current host is rejected with a diagnostic. Hosts can also add an optional `sha256` value to a manifest to verify that the assembly file matches the expected payload before it is loaded.

## CLI Commands

The CLI can inspect runtime plugins without writing a custom host:

```bash
dotnet run --project src/Injectlynx.Cli -f net10.0 -- plugins list plugins --config injectlynx.plugins.json
dotnet run --project src/Injectlynx.Cli -f net10.0 -- plugins validate plugins
dotnet run --project src/Injectlynx.Cli -f net10.0 -- plugins inspect plugins
```

Use `list` to see discovered plugin names, versions, order, and descriptions. Use `validate` to surface manifest and load diagnostics. Use `inspect` to print plugin types and the service registrations they add to `IServiceCollection`.

## Diagnostics

Plugin diagnostics use `INJP` codes:

- `INJP000`: plugin loaded successfully.
- `INJP001` and `INJP002`: configured manifest or plugin directory was not found.
- `INJP003` through `INJP006`: manifest JSON or required manifest fields are invalid.
- `INJP007` and `INJP008`: plugin was disabled by manifest or host options.
- `INJP009` through `INJP012`: assembly, type, contract, or load failure.
- `INJP013`: plugin registered a service contract that was already registered.
- `INJP014` and `INJP015`: explicit plugin assembly was not found or assembly scanning failed.
- `INJP016`: plugin targets a newer major .NET version than the host.
- `INJP017` and `INJP018`: manifest dependencies are missing or cyclic.
- `INJP019` and `INJP020`: plugin configuration file is missing or invalid.
- `INJP021`: plugin assembly SHA-256 does not match the manifest.

Duplicate registration is a warning because Microsoft DI permits multiple registrations. Hosts should decide whether that is expected, such as decorators or `IEnumerable<T>`, or a startup policy violation.

## Samples

Build the sample plugin and pass its output directory to the host:

```bash
dotnet build samples/PluginSample/PluginSample.csproj -f net10.0
dotnet run --project samples/PluginHost/PluginHost.csproj -f net10.0 -- samples/PluginSample/bin/Debug/net10.0
```

The sample demonstrates the simplest packaging shape: the plugin assembly implements `IInjectlynxPlugin`, and the host scans the output folder. Add a manifest only when you need metadata or ordering.

## Native AOT And Trimming

Dynamic plugin loading uses runtime assembly loading. That means it has different tradeoffs from the default Injectlynx source-generated path:

- It is not the recommended path for strict Native AOT applications.
- Trimming can remove types that are only activated from manifests unless the host and plugin preserve them.
- Plugin folders are a runtime trust boundary, so hosts should load only from directories they control.
- Long-running hosts should design a policy for version compatibility, disabled plugins, and duplicate service contracts.

For Native AOT-sensitive applications, prefer generated registrations and use runtime plugins only behind an explicit product decision.
