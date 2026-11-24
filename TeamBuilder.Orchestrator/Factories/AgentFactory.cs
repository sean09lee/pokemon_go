using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using TeamBuilder.Orchestrator.Constants;

namespace TeamBuilder.Orchestrator.Factories;

public sealed class AgentFactory
{
    private readonly IChatClient _model;

    public AgentFactory(IConfiguration configuration)
    {
        var endpoint = configuration[ConfigurationConstants.OllamaEndpoint];
        var modelName = configuration[ConfigurationConstants.OllamaModel];

        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.IsNullOrWhiteSpace(modelName))
        {
            throw new InvalidOperationException("Configure Ollama:Endpoint and Ollama:Model.");
        }

        _model = new OllamaApiClient(
            new Uri(endpoint),
            modelName);
    }

    public AIAgent CreateQAAgent() =>
        _model.AsAIAgent(
            name: AgentConstants.QAAgentName,
            instructions: "You are a Quality Assurance Agent. Your job is to validate code submissions against strict quality standards. " +
                         "Check that code compiles, has unit tests for valid/invalid/edge cases, functional tests, and UI tests when needed. " +
                         "Return a JSON response with { passed: boolean, feedback: string, issues: string[] }");

    public AIAgent CreatePMAgent() =>
        _model.AsAIAgent(
            name: AgentConstants.PMAgentName,
            instructions: "You are a Product Manager Agent. Your job is to understand user requirements and define clear specifications. " +
                         "Break down requests into actionable requirements and validate feature requests. " +
                         "Return clear, structured requirements that developers can implement.");

    public AIAgent CreateDevAgent() =>
        _model.AsAIAgent(
            name: AgentConstants.DevAgentName,
            instructions: "You are a Development Agent. Your job is to implement features based on specifications and requirements. " +
                         "Write production-ready C# code with comprehensive tests. " +
                         "When given feedback, revise code to address the issues. " +
                         "Always include unit tests, functional tests, and UI tests when appropriate.");
}
