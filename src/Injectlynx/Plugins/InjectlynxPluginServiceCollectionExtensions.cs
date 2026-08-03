using System;
using Microsoft.Extensions.DependencyInjection;

namespace Injectlynx.Plugins;

public static class InjectlynxPluginServiceCollectionExtensions
{
    public static InjectlynxPluginLoadResult AddInjectlynxPlugins(
        this IServiceCollection services,
        Action<InjectlynxPluginLoadOptions> configure)
    {
        var options = new InjectlynxPluginLoadOptions();
        configure(options);
        return InjectlynxPluginLoader.Load(services, options);
    }
}
