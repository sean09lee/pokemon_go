using TeamBuilder.Orchestrator.Agents;
using TeamBuilder.Orchestrator.Contracts;
using TeamBuilder.Orchestrator.Factories;
using TeamBuilder.Orchestrator.Services;
using TeamBuilder.Orchestrator.Workflows;

namespace TeamBuilder.Orchestrator.Extensions;

public static class OrchestratorRegistration
{
    public static void AddOrchestratorServices(this IServiceCollection services)
    {
        // Register the AgentFactory as Singleton (stateless, provides AIAgent instances)
        services.AddSingleton<AgentFactory>();

        // Register services
        services.AddScoped<IOrchestratorService, OrchestratorService>();

        // Register agents (Scoped for Foundry AIAgent lifecycle)
        services.AddScoped<IQAAgent, QAAgent>();
        services.AddScoped<IPMAgent, PMAgent>();
        services.AddScoped<IDevAgent, DevAgent>();

        // Register workflow
        services.AddScoped<IWorkflow, Workflow>();
    }
}
