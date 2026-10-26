using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging.ApplicationInsights;
using TeamBuilder.Diagnostics;

namespace TeamBuilder.Extensions;

public static class TelemetryRegistration
{
    public static void AddApplicationTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplicationInsightsTelemetry(options =>
        {
            options.EnableAdaptiveSampling = configuration.GetValue("ApplicationInsights:EnableAdaptiveSampling", false);
        });
        services.Configure<ApplicationInsightsLoggerOptions>(options => options.IncludeScopes = true);
        services.AddSingleton<ApplicationTelemetryInitializer>();
        services.AddSingleton<ITelemetryInitializer>(provider => provider.GetRequiredService<ApplicationTelemetryInitializer>());
        services.AddScoped<CircuitHandler, CircuitTraceHandler>();
        services.AddLogging(logging =>
        {
            logging.AddFilter("TeamBuilder", LogLevel.Information);
            logging.AddFilter<ApplicationInsightsLoggerProvider>("TeamBuilder", LogLevel.Information);
            logging.AddFilter<ApplicationInsightsLoggerProvider>("Microsoft.AspNetCore.Components.Server.Circuits", LogLevel.Information);
            logging.AddFilter<ApplicationInsightsLoggerProvider>("Microsoft.AspNetCore.Http.Connections", LogLevel.Information);
            logging.AddFilter<ApplicationInsightsLoggerProvider>("Microsoft.AspNetCore.SignalR", LogLevel.Warning);
        });
    }

    public static void AddApplicationLifecycleTracing(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TeamBuilder.Diagnostics.Lifecycle");
        app.Lifetime.ApplicationStarted.Register(() => logger.LogInformation(
            "Application started: boot {BootId}; instance {InstanceId}; process {ProcessId}; web process working set {WorkingSetMB} MB",
            ApplicationTelemetryInitializer.BootId, ApplicationTelemetryInitializer.InstanceId,
            Environment.ProcessId, Environment.WorkingSet / (1024 * 1024)));
        app.Lifetime.ApplicationStopping.Register(() => logger.LogWarning(
            "Application stopping: boot {BootId}", ApplicationTelemetryInitializer.BootId));
        app.Lifetime.ApplicationStopped.Register(() => logger.LogInformation(
            "Application stopped: boot {BootId}", ApplicationTelemetryInitializer.BootId));
    }
}