using Meridian.Application;
using Meridian.Infrastructure;
using Meridian.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddHostedService<Worker>();

var host = builder.Build();
host.Run();
