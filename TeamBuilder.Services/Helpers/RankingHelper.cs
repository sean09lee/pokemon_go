using TeamBuilder.Data.Models;

namespace TeamBuilder.Services.Helpers;

internal static class RankingHelper
{
    internal static Task<IList<RankedPokemon>> BuildTopRaidTeamAsync(
        IEnumerable<Pokemon> ownedPokemon,
        IEnumerable<HubRankedPokemon> tierList,
        IEnumerable<PokemonEvolution> evolutions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ownedList = ownedPokemon.ToList();
        var byNumber = tierList
            .Where(x => x.PokemonNumber is not null)
            .GroupBy(x => x.PokemonNumber!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());

        var evolutionMap = BuildEvolutionMap(evolutions);

        var rankedOwned = new List<(Pokemon Owned, int Rank, HubRankedPokemon Matched, Pokemon? EvolvedOwned)>();

        foreach (var owned in ownedList)
        {
            if (owned.PokemonNumber is null)
            {
                continue;
            }

            var bestMatch = GetBestRankCandidate(owned, ownedList, byNumber, evolutionMap);
            if (bestMatch is null)
            {
                continue;
            }

            rankedOwned.Add(bestMatch.Value);
        }

        IList<RankedPokemon> results = rankedOwned
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => x.Owned.CP ?? int.MinValue)
            .Take(6)
            .Select(x => new RankedPokemon
            {
                Index = x.Rank,
                Name = x.Owned.Name,
                Form = x.Owned.Form,
                PokemonNumber = x.Owned.PokemonNumber,
                CP = x.Owned.CP,
                HP = x.Owned.HP,
                EvolvedName = x.Matched.PokemonNumber == x.Owned.PokemonNumber ? null : (x.EvolvedOwned?.Name ?? x.Matched.Name),
                EvolvedPokemonNumber = x.Matched.PokemonNumber == x.Owned.PokemonNumber ? null : x.Matched.PokemonNumber,
                EvolvedCP = x.Matched.PokemonNumber == x.Owned.PokemonNumber ? null : x.EvolvedOwned?.CP,
                EvolvedHP = x.Matched.PokemonNumber == x.Owned.PokemonNumber ? null : x.EvolvedOwned?.HP
            })
            .ToList();

            return Task.FromResult(results);
    }

    private static (Pokemon Owned, int Rank, HubRankedPokemon Matched, Pokemon? EvolvedOwned)? GetBestRankCandidate(
        Pokemon owned,
        IReadOnlyList<Pokemon> ownedList,
        IReadOnlyDictionary<int, List<HubRankedPokemon>> rankedByNumber,
        IReadOnlyDictionary<int, HashSet<int>> evolutionMap)
    {
        if (owned.PokemonNumber is null)
        {
            return null;
        }

        var candidates = new List<(HubRankedPokemon Ranked, Pokemon? OwnedEvolution)>();

        var selfMatch = FindBestMatchForNumber(owned.PokemonNumber.Value, owned.Form, rankedByNumber);
        if (selfMatch is not null)
        {
            candidates.Add((selfMatch, null));
        }

        if (evolutionMap.TryGetValue(owned.PokemonNumber.Value, out var evolutions))
        {
            foreach (var evolutionNumber in evolutions)
            {
                var evolvedOwned = ownedList
                    .Where(x => x.PokemonNumber == evolutionNumber)
                    .OrderByDescending(x => x.CP ?? int.MinValue)
                    .FirstOrDefault();

                var preferredForm = evolvedOwned?.Form;
                var evolutionMatch = FindBestMatchForNumber(evolutionNumber, preferredForm, rankedByNumber);
                if (evolutionMatch is not null)
                {
                    candidates.Add((evolutionMatch, evolvedOwned));
                }
            }
        }

        var best = candidates
            .OrderBy(x => x.Ranked.Rank ?? int.MaxValue)
            .FirstOrDefault();

        if (best.Ranked is null)
        {
            return null;
        }

        return (owned, best.Ranked.Rank ?? int.MaxValue, best.Ranked, best.OwnedEvolution);
    }

    private static HubRankedPokemon? FindBestMatchForNumber(
        int pokemonNumber,
        string? preferredForm,
        IReadOnlyDictionary<int, List<HubRankedPokemon>> rankedByNumber)
    {
        if (!rankedByNumber.TryGetValue(pokemonNumber, out var entries) || entries.Count == 0)
        {
            return null;
        }

        var normalizedForm = NormalizeForm(preferredForm);
        var exact = entries
            .Where(x => NormalizeForm(x.Form) == normalizedForm)
            .OrderBy(x => x.Rank ?? int.MaxValue)
            .FirstOrDefault();

        return exact ?? entries.OrderBy(x => x.Rank ?? int.MaxValue).FirstOrDefault();
    }

    private static Dictionary<int, HashSet<int>> BuildEvolutionMap(IEnumerable<PokemonEvolution> evolutions)
    {
        var edges = new Dictionary<int, HashSet<int>>();

        foreach (var evolution in evolutions)
        {
            if (evolution.PokemonId is null || evolution.Evolutions.Count == 0)
            {
                continue;
            }

            var sourceId = evolution.PokemonId.Value;
            if (!edges.TryGetValue(sourceId, out var set))
            {
                set = new HashSet<int>();
                edges[sourceId] = set;
            }

            foreach (var detail in evolution.Evolutions)
            {
                if (detail.PokemonId is int evoId && evoId != sourceId)
                {
                    set.Add(evoId);
                }
            }
        }

        var expanded = new Dictionary<int, HashSet<int>>();
        foreach (var node in edges.Keys)
        {
            var visited = new HashSet<int>();
            Expand(node, edges, visited);
            expanded[node] = visited;
        }

        return expanded;
    }

    private static void Expand(int current, IReadOnlyDictionary<int, HashSet<int>> edges, HashSet<int> visited)
    {
        if (!edges.TryGetValue(current, out var nextNodes))
        {
            return;
        }

        foreach (var next in nextNodes)
        {
            if (!visited.Add(next))
            {
                continue;
            }

            Expand(next, edges, visited);
        }
    }

    private static string NormalizeForm(string? form)
    {
        if (string.IsNullOrWhiteSpace(form))
        {
            return "normal";
        }

        return form.Trim().ToLowerInvariant();
    }
}
