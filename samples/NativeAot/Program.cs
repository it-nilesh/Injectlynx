using Microsoft.Extensions.DependencyInjection;
using NativeAot.Services;

var services = new ServiceCollection()
    .AddInjectlynxServices()
    .BuildServiceProvider(validateScopes: true);

var clock = services.GetRequiredService<IClockService>();
Console.WriteLine(clock.GetMessage());
