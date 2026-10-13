using TeamBuilder.Components;
using TeamBuilder.Extensions;

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
app.MapHealthChecks("/health");

app.Run();
