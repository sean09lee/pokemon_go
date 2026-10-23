using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TeamBuilder.HealthChecks;

/// <summary>
/// Verifies that the external PogoAPI dependency is reachable.
/// </summary>
public class PogoApiHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(PogoApiHealthCheck));
            using var response = await client.GetAsync("api/v1/pokemon_stats.json", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("PogoAPI is reachable.")
                : HealthCheckResult.Degraded($"PogoAPI responded with status code {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PogoAPI is unreachable.", ex);
        }
    }
}
