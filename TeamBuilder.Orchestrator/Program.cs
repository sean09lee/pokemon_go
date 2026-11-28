using TeamBuilder.Configurations.Extensions;
using TeamBuilder.Orchestrator.Extensions;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

// Add controllers
services.AddControllers();

// Add app configuration
services.AddAppConfiguration(configuration, builder.Environment);

// Add services
services.AddOrchestratorServices();

// Add logging
services.AddLogging();

// Add Swagger/OpenAPI
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

var app = builder.Build();

// Enable Swagger/OpenAPI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Redirect root to Swagger
    app.MapGet("/", () => Results.Redirect("/swagger/index.html"))
        .WithName("Root")
        .DisableAntiforgery();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health")
    .DisableAntiforgery();

app.Run();
