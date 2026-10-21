using TeamBuilder.AI;
using TeamBuilder.AI.Configuration;
using TeamBuilder.Data.Constants;
using TeamBuilder.HealthChecks;
using TeamBuilder.Services.Contracts;
using TeamBuilder.Services.Helpers;
using TeamBuilder.Services.Services;

namespace TeamBuilder.Extensions;

public static class ServiceRegistration
{
    public static void AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add health checks and razor components
        services.AddHttpClient(nameof(PogoApiHealthCheck), client => client.BaseAddress = new Uri(ServiceConstants.BaseUri));
        services.AddHealthChecks()
            .AddCheck<PogoApiHealthCheck>("pogo-api", tags: ["ready"]);
        services.AddRazorComponents().AddInteractiveServerComponents();

        // Add singleton services
        services.AddSingleton<ICacheService, CacheService>();

        // Add application services
        services.AddScoped<CsvParserService>();
        services.AddScoped<IRankingService, RankingService>();
        services.AddScoped<IHubApiService, HubApiService>();
        services.AddScoped<IPogoApiService, PogoApiService>();

        // Add helper services
        services.AddHttpClient<PogoHelpers>(client => client.BaseAddress = new Uri(ServiceConstants.BaseUri));

        // Add AI Advisor services
        services.Configure<AIAdvisorOptions>(configuration.GetSection(AIAdvisorOptions.SectionName));
        services.AddSingleton<FoundryChatClient>();
        services.AddScoped<IAIAdvisorService, AIAdvisorService>();
    }
}
