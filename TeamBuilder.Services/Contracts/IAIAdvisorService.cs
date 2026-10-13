using TeamBuilder.Data.Models;

namespace TeamBuilder.Services.Contracts;

/// <summary>
/// Orchestrates AI Advisor chat conversations, delegating to the Azure AI Foundry client.
/// </summary>
public interface IAIAdvisorService
{
    Task<AiChatMessage> SendMessageAsync(IList<AiChatMessage> conversation, CancellationToken cancellationToken = default);
}
