using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using TeamBuilder.AI.Contracts;
using TeamBuilder.Data.Models;
using TeamBuilder.Services.AiTools;
using TeamBuilder.Services.Contracts;

namespace TeamBuilder.Services.Services;

/// <summary>
/// Orchestrates AI Advisor chat conversations, delegating to the Azure AI Foundry client and
/// invoking grounded function-calling tools (see <see cref="IAiToolRegistry"/>) instead of relying
/// solely on the model's general knowledge.
/// </summary>
public class AIAdvisorService(IFoundryChatClient foundryChatClient, IAiToolRegistry toolRegistry, ILogger<AIAdvisorService> logger) : IAIAdvisorService
{
    private const int MaxToolCallRounds = 5;

    private readonly IFoundryChatClient _foundryChatClient = foundryChatClient;
    private readonly IAiToolRegistry _toolRegistry = toolRegistry;

    public async Task<AiChatMessage> SendMessageAsync(IList<AiChatMessage> conversation, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(_foundryChatClient.SystemPrompt))
        {
            messages.Add(new SystemChatMessage(_foundryChatClient.SystemPrompt));
        }

        foreach (var message in conversation)
        {
            messages.Add(message.IsUser ? new UserChatMessage(message.Text) : new AssistantChatMessage(message.Text));
        }

        var tools = _toolRegistry.GetToolDefinitions();

        for (var round = 0; round < MaxToolCallRounds; round++)
        {
            var completion = await _foundryChatClient.CompleteAsync(messages, tools, cancellationToken);
            logger.LogInformation("AI Advisor completion round {Round} finished with reason {FinishReason}", round, completion.FinishReason);

            if (completion.FinishReason != ChatFinishReason.ToolCalls || completion.ToolCalls.Count == 0)
            {
                var responseText = completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
                return new AiChatMessage { IsUser = false, Text = responseText };
            }

            // Record the model's requested tool calls, then execute each one and feed the
            // grounded results back into the conversation before asking the model to continue.
            messages.Add(new AssistantChatMessage(completion));

            foreach (var toolCall in completion.ToolCalls)
            {
                logger.LogInformation("AI Advisor requested tool call {ToolName} ({ToolCallId})", toolCall.FunctionName, toolCall.Id);

                var toolResult = await _toolRegistry.ExecuteAsync(
                    toolCall.FunctionName,
                    toolCall.FunctionArguments.ToString(),
                    cancellationToken);

                messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
            }
        }

        logger.LogWarning("AI Advisor exceeded the maximum of {MaxRounds} tool-call rounds without a final answer", MaxToolCallRounds);

        return new AiChatMessage
        {
            IsUser = false,
            Text = "I wasn't able to finish looking that up after several tool calls. Could you try rephrasing your question?"
        };
    }
}
