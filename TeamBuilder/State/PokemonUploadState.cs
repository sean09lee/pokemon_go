using TeamBuilder.Data.Models;
using TeamBuilder.Data.Responses;

namespace TeamBuilder.State;

/// <summary>
/// Tracks whether the user has successfully uploaded a Pokémon CSV during the current session,
/// so UI elements (like nav tabs) can react without requiring a page reload. Also holds the
/// uploaded Pokémon roster so it can be shared between the Raid Builder page and the My Pokemon page.
/// </summary>
public class PokemonUploadState
{
    public bool HasUploadedPokemon { get; private set; }

    public IReadOnlyList<Pokemon> Pokemon { get; private set; } = [];

    public IReadOnlyList<CsvValidationFailure> ValidationFailures { get; private set; } = [];

    public event Action? Changed;

    public void MarkUploaded()
    {
        if (HasUploadedPokemon)
        {
            return;
        }

        HasUploadedPokemon = true;
        Changed?.Invoke();
    }

    public void SetPokemon(IReadOnlyList<Pokemon> pokemon, IReadOnlyList<CsvValidationFailure> validationFailures)
    {
        Pokemon = pokemon;
        ValidationFailures = validationFailures;
        Changed?.Invoke();
    }

    public void Clear()
    {
        HasUploadedPokemon = false;
        Pokemon = [];
        ValidationFailures = [];
        Changed?.Invoke();
    }
}
