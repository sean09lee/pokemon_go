namespace TeamBuilder.Data.Models;

public class RankedPokemon
{
    // Current pokemon info
    public int? Index { get; set; }
    public string? Name { get; set; }
    public string? Form { get; set; }
    public int? PokemonNumber { get; set; }
    public int? CP { get; set; }
    public int? HP { get; set; }

    // Evolved pokemon info (if applicable)
    public string? EvolvedName { get; set; }
    public string? EvolvedForm { get; set; }
    public int? EvolvedPokemonNumber { get; set; }
    public int? EvolvedCP { get; set; }
    public int? EvolvedHP { get; set; }

    // Resource requirements for this selected team slot
    public int? RequiredEvolutionCandy { get; set; }
    public int? RequiredCandyToLevel30 { get; set; }
    public int? RequiredDustToLevel30 { get; set; }
    public int? TotalRequiredCandy { get; set; }
    public int? RequiredFirstTimeMegaEnergy { get; set; }
}
