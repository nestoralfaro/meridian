using Meridian.Api.Auth;
using Meridian.Api.Endpoints;
using Meridian.Api.Hosting;
using Meridian.Application;
using Meridian.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.Services.AddApiDocumentation();
builder.Services.AddMeridianAuth(secureCookies: builder.Configuration.GetValue("Auth:SecureCookies", defaultValue: true));
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
builder.Services.Configure<DemoDataOptions>(builder.Configuration.GetSection(DemoDataOptions.Section));
builder.Services.AddHostedService<DemoDataMaintenance>();

var app = builder.Build();

app.UseTraceIdHeader();
app.UseExceptionHandler(); // unhandled exceptions → 500 ProblemDetails with traceId; details stay in logs
app.UseStatusCodePages();
app.UseSpaStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapApiDocumentation();
var api = app.MapGroup("/api/v1");
api.MapVersionEndpoints();
api.MapAuthEndpoints();
app.MapSpaFallback();

app.Run();
