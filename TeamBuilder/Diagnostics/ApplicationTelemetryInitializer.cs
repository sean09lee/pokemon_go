using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace TeamBuilder.Diagnostics;

public sealed class ApplicationTelemetryInitializer : ITelemetryInitializer
{
    public static string BootId { get; } = Guid.NewGuid().ToString("N");
    public static string InstanceId { get; } = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName;

    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is ISupportProperties properties)
        {
            properties.Properties.TryAdd("BootId", BootId);
            properties.Properties.TryAdd("ProcessId", Environment.ProcessId.ToString());
            properties.Properties.TryAdd("InstanceId", InstanceId);
        }
    }
}