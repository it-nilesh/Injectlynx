using Injectlynx.Plugins;
using Microsoft.Extensions.DependencyInjection;

var pluginDirectory = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "plugins");

var services = new ServiceCollection();
var result = services.AddInjectlynxPlugins(options =>
{
    options.AddDirectory(pluginDirectory);
    options.ThrowOnError = false;
});

foreach (var diagnostic in result.Diagnostics)
{
    Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
}

Console.WriteLine($"Loaded plugins: {result.Plugins.Count}");
using var provider = services.BuildServiceProvider();

foreach (var plugin in result.Plugins)
{
    Console.WriteLine($"- {plugin.Manifest.Name} {plugin.Manifest.Version}");
}
