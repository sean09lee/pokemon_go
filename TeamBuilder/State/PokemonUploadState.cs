namespace TeamBuilder.State;

/// <summary>
/// Tracks whether the user has successfully uploaded a Pokémon CSV during the current session,
/// so UI elements (like nav tabs) can react without requiring a page reload.
/// </summary>
public class PokemonUploadState
{
    public bool HasUploadedPokemon { get; private set; }

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
}
