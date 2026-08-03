using Microsoft.Extensions.DependencyInjection;

namespace Injectlynx.Plugins;

public interface IInjectlynxPlugin
{
    string Name { get; }

    string Description { get; }

    int Order { get; }

    void ConfigureServices(IServiceCollection services);
}
