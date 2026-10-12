namespace TeamBuilder.Data.Models;

/// <summary>
/// Represents a single message exchanged with the AI Advisor chat feature.
/// </summary>
public class AiChatMessage
{
    public bool IsUser { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
