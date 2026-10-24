using Meridian.Application;
using Meridian.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure();

var app = builder.Build();

app.MapGet("/", () => "Meridian API");

app.Run();
