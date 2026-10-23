using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using TeamBuilder.AI.Configuration;
using TeamBuilder.AI.Contracts;

namespace TeamBuilder.AI;

/// <summary>
/// Thin wrapper around the Azure AI Foundry chat completions client used by the AI Advisor feature.
/// </summary>
public class FoundryChatClient : IFoundryChatClient
{
    private readonly ChatClient _chatClient;
    private readonly AIAdvisorOptions _options;

    public FoundryChatClient(IOptions<AIAdvisorOptions> options) 
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException("AIAdvisor:Endpoint must be configured in Azure App Configuration.");
        }

        var endpoint = new Uri(_options.Endpoint);

        AzureOpenAIClient azureClient = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new AzureKeyCredential(_options.ApiKey));

        _chatClient = azureClient.GetChatClient(_options.DeploymentName);
    }

    /// <summary>
    /// The configured system prompt, if any, used to steer the AI Advisor's responses.
    /// </summary>
    public string? SystemPrompt => _options.SystemPrompt;

    public async Task<string> GetResponseAsync(IEnumerable<(bool IsUser, string Text)> conversation, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
        {
            messages.Add(new SystemChatMessage(_options.SystemPrompt));
        }

        foreach (var (isUser, text) in conversation)
        {
            messages.Add(isUser ? new UserChatMessage(text) : new AssistantChatMessage(text));
        }

        var completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
        return completion.Value.Content.Count > 0
            ? completion.Value.Content[0].Text
            : string.Empty;
    }

    /// <summary>
    /// Sends a chat completion request that may invoke one or more function-calling tools, allowing
    /// the caller to inspect requested tool calls, execute them, and continue the conversation.
    /// </summary>
    public async Task<ChatCompletion> CompleteAsync(IList<ChatMessage> messages, IReadOnlyList<ChatTool>? tools = null, CancellationToken cancellationToken = default)
    {
        var options = new ChatCompletionOptions();

        if (tools is not null)
        {
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }
        }

        var completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
        return completion.Value;
    }
}
