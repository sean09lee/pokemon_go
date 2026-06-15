namespace TeamBuilder.Data.Models;

public sealed class RankedOwnedCandidate
{
    public required Pokemon Owned { get; init; }
    public required HubRankedPokemon Matched { get; init; }
    public Pokemon? EvolvedOwned { get; init; }
    public required int Rank { get; init; }
    public required int FamilyKey { get; init; }
    public required string TargetForm { get; init; }
    public required int CandyBucketPokemonNumber { get; init; }
    public string? CandyPromptName { get; init; }
    public required int RequiredEvolutionCandy { get; init; }
    public required int RequiredCandyToLevel30 { get; init; }
    public required int RequiredDustToLevel30 { get; init; }
    public required int TotalRequiredCandy { get; init; }
    public int? RequiredFirstTimeMegaEnergy { get; init; }
}