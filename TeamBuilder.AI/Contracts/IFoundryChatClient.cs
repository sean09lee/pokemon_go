namespace TeamBuilder.AI.Contracts;

internal interface IFoundryChatClient
{
    Task<string> GetResponseAsync(IEnumerable<(bool IsUser, string Text)> conversation, CancellationToken cancellationToken = default);
}
