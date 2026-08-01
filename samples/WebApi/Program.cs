using WebApi.DependencyInjection;
using WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IAuditSink, ConsoleAuditSink>();
builder.Services.AddWebApiServices();

var app = builder.Build();

app.MapControllers();

app.Run();
