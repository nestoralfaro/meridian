using Meridian.Api.Endpoints;
using Meridian.Api.Hosting;
using Meridian.Application;
using Meridian.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseStatusCodePages();
app.UseSpaStaticFiles();

app.MapHealthEndpoints();
app.MapGroup("/api/v1").MapVersionEndpoints();
app.MapSpaFallback();

app.Run();
