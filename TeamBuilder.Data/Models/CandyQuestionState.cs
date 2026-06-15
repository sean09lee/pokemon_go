namespace TeamBuilder.Data.Models;

public sealed class CandyQuestionState
{
    public required int PokemonNumber { get; init; }
    public required string DisplayName { get; init; }
    public string InputValue { get; set; } = "0";
    public bool IsSubmitted { get; set; }
    public TaskCompletionSource<int> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}