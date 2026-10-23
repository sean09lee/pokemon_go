using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TeamBuilder.Components;
using TeamBuilder.Extensions;
using TeamBuilder.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

// Add app configuration
services.AddAppConfiguration(configuration);

// Add services to the container
services.AddServices(configuration);

var app = builder.Build();

app.AddHttpPipeline();

builder.AddStaticImages(app);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteResponse,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse,
});

app.Run();
