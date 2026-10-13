namespace TeamBuilder.AI.Configuration;

/// <summary>
/// Options bound from Azure App Configuration describing how to connect to the
/// Azure AI Foundry project hosting the AI Advisor chat deployment.
/// </summary>
public class AIAdvisorOptions
{
    public const string SectionName = "AIAdvisor";

    /// <summary>
    /// The Azure AI Foundry project endpoint, e.g. https://{resource}.services.ai.azure.com/api/projects/{project}
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// The name of the chat completion deployment hosted in the Foundry project.
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Optional API key used when key-based authentication is configured instead of Entra ID.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional system prompt used to steer the AI Advisor's responses.
    /// </summary>
    public string? SystemPrompt { get; set; }
}
