using OpenAI.Chat;

namespace TeamBuilder.AI.Contracts;

/// <summary>
/// Thin wrapper around the Azure AI Foundry chat completions client used by the AI Advisor feature.
/// </summary>
public interface IFoundryChatClient
{
    /// <summary>
    /// The configured system prompt, if any, used to steer the AI Advisor's responses.
    /// </summary>
    string? SystemPrompt { get; }

    Task<string> GetResponseAsync(IEnumerable<(bool IsUser, string Text)> conversation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chat completion request that may invoke one or more function-calling tools, allowing
    /// the caller to inspect requested tool calls, execute them, and continue the conversation.
    /// </summary>
    Task<ChatCompletion> CompleteAsync(IList<ChatMessage> messages, IReadOnlyList<ChatTool>? tools = null, CancellationToken cancellationToken = default);
}
