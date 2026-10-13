using TeamBuilder.AI;
using TeamBuilder.Data.Models;
using TeamBuilder.Services.Contracts;

namespace TeamBuilder.Services.Services;

/// <summary>
/// Orchestrates AI Advisor chat conversations, delegating to the Azure AI Foundry client.
/// </summary>
public class AIAdvisorService(FoundryChatClient foundryChatClient) : IAIAdvisorService
{
    private readonly FoundryChatClient _foundryChatClient = foundryChatClient;

    public async Task<AiChatMessage> SendMessageAsync(IList<AiChatMessage> conversation, CancellationToken cancellationToken = default)
    {
        var history = conversation.Select(m => (m.IsUser, m.Text));
        var responseText = await _foundryChatClient.GetResponseAsync(history, cancellationToken);

        return new AiChatMessage
        {
            IsUser = false,
            Text = responseText
        };
    }
}
