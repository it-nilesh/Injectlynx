using Microsoft.Extensions.DependencyInjection;
using NativeAot.Services;

var services = new ServiceCollection()
    .AddInjectlynxServices()
    .BuildServiceProvider(validateScopes: true);

var startupMessage = services.GetRequiredService<IStartupMessageService>();
Console.WriteLine(startupMessage.CreateMessage());
