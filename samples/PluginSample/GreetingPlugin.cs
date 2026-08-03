using Injectlynx.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace PluginSample;

public interface IPluginGreetingService
{
    string CreateGreeting();
}

public sealed class PluginGreetingService : IPluginGreetingService
{
    public string CreateGreeting() => "Hello from a runtime Injectlynx plugin.";
}

public sealed class GreetingPlugin : IInjectlynxPlugin
{
    public string Name => "GreetingPlugin";

    public string Description => "Registers a greeting service from a runtime plugin.";

    public int Order => 0;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPluginGreetingService, PluginGreetingService>();
    }
}
