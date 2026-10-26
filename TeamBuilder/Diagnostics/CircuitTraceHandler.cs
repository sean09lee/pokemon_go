using Microsoft.AspNetCore.Components.Server.Circuits;

namespace TeamBuilder.Diagnostics;

public sealed class CircuitTraceHandler(ILogger<CircuitTraceHandler> logger) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogInformation("Blazor circuit opened: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogInformation("Blazor connection up: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogWarning("Blazor connection down: {CircuitId}; web process working set {WorkingSetMB} MB", circuit.Id, Environment.WorkingSet / (1024 * 1024));
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        logger.LogInformation("Blazor circuit closed: {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            using var scope = logger.BeginScope(new Dictionary<string, object> { ["CircuitId"] = context.Circuit.Id });
            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Blazor inbound activity failed: {CircuitId}", context.Circuit.Id);
                throw;
            }
        };
    }
}