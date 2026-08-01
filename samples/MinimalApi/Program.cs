using MinimalApi.Handlers;
using MinimalApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<object>(new SampleState("configured in Program.cs"));
builder.Services.AddInjectlynxServices();

var app = builder.Build();

app.MapGet("/orders/{id:guid}", async (Guid id, IRequestHandler<GetOrderQuery> handler) =>
{
    var order = await handler.HandleAsync(new GetOrderQuery(id));
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.Run();
