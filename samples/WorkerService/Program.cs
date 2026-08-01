using WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInjectlynxServices();
builder.Services.AddHostedService<Worker>();

await builder.Build().RunAsync();
