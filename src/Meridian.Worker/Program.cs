using Meridian.Application;
using Meridian.Application.DemoData;
using Meridian.Infrastructure;
using Meridian.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// One-off maintenance commands (run by the demo-data workflow): `dotnet run --project src/Meridian.Worker -- demo-data reset`
if (args is ["demo-data", "reset"])
{
    using var commandHost = builder.Build();
    await using var scope = commandHost.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IDemoData>().ResetAsync();
    Console.WriteLine("Demo data reset.");
    return;
}

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
