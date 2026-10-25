using Meridian.Api.Endpoints;
using Meridian.Api.Hosting;
using Meridian.Application;
using Meridian.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.Services.AddApiDocumentation();
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseTraceIdHeader();
app.UseExceptionHandler(); // unhandled exceptions → 500 ProblemDetails with traceId; details stay in logs
app.UseStatusCodePages();
app.UseSpaStaticFiles();

app.MapHealthEndpoints();
app.MapApiDocumentation();
app.MapGroup("/api/v1").MapVersionEndpoints();
app.MapSpaFallback();

app.Run();
