using TeamBuilder.Data.Models;

namespace TeamBuilder.Services.Helpers;

internal static class RankingHelper
{
    internal static IList<RankedPokemon> BuildTopRaidTeam(IEnumerable<Pokemon> ownedPokemon, IEnumerable<HubRankedPokemon> tierList)
    {
        var byNumber = tierList
            .Where(x => x.PokemonNumber is not null)
            .GroupBy(x => x.PokemonNumber!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var rankedOwned = new List<(Pokemon Owned, int Rank)>();

        foreach (var owned in ownedPokemon)
        {
            if (owned.PokemonNumber is null)
            {
                continue;
            }

            if (!byNumber.TryGetValue(owned.PokemonNumber.Value, out var candidates))
            {
                continue;
            }

            var ownedForm = NormalizeForm(owned.Form);
            var exact = candidates
                .Where(x => NormalizeForm(x.Form) == ownedForm)
                .OrderBy(x => x.Rank ?? int.MaxValue)
                .FirstOrDefault();

            var matched = exact
                ?? candidates.OrderBy(x => x.Rank ?? int.MaxValue).FirstOrDefault();

            if (matched is null)
            {
                continue;
            }

            rankedOwned.Add((owned, matched.Rank ?? int.MaxValue));
        }

        return rankedOwned
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => x.Owned.CP ?? int.MinValue)
            .Take(6)
            .Select(x => new RankedPokemon
            {
                Index = x.Owned.Index,
                Name = x.Owned.Name,
                Form = x.Owned.Form,
                PokemonNumber = x.Owned.PokemonNumber,
                CP = x.Owned.CP,
                HP = x.Owned.HP
            })
            .ToList();
    }

    internal static string NormalizeForm(string? form)
    {
        if (string.IsNullOrWhiteSpace(form))
        {
            return "normal";
        }

        return form.Trim().ToLowerInvariant();
    }
}
