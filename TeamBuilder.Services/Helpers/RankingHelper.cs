using TeamBuilder.Data.Models;
using System.Runtime.CompilerServices;

namespace TeamBuilder.Services.Helpers;

internal static class RankingHelper
{
    internal static async Task<RaidTeamVariantResult> BuildRaidTeamVariantsAsync(
        IEnumerable<Pokemon> ownedPokemon,
        IEnumerable<HubRankedPokemon> tierList,
        string attackerType,
        IReadOnlyDictionary<int, HashSet<int>> evolutionMap,
        IReadOnlyDictionary<(int Source, int Target), int> evolutionCandyCostMap,
        IReadOnlyDictionary<decimal, PokemonPowerupRequirement> powerupRequirements,
        IEnumerable<PokemonTypeInfo> pokemonTypes,
        IEnumerable<MegaPokemon> megaPokemon,
        int dustLimit,
        IDictionary<int, int> candyByPokemonNumber,
        Func<int, string?, CancellationToken, Task<int>>? resolveCandyAsync,
        ISet<string>? globallyReservedOwnedKeys = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ownedList = ownedPokemon.ToList();
        var byNumber = tierList
            .Where(x => x.PokemonNumber is not null)
            .GroupBy(x => x.PokemonNumber!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var megaList = megaPokemon.ToList();
        var familyNameByRoot = BuildFamilyNameByRoot(ownedList, evolutionMap);

        var rankedOwned = new List<RaidCandidate>();

        foreach (var owned in ownedList)
        {
            if (owned.PokemonNumber is null)
            {
                continue;
            }

            var familyRoot = GetEvolutionLineRoot(owned.PokemonNumber.Value, evolutionMap);
            var familyReserveCap = GetFamilyReserveCap(familyRoot, evolutionMap, megaList);
            var requiredCandyToLevel30 = CalculateCandyRequiredToLevel30(owned, powerupRequirements);
            var requiredDustToLevel30 = CalculateDustRequiredToLevel30(owned, powerupRequirements);

            foreach (var option in GetRankCandidatesForOwned(owned, ownedList, byNumber, evolutionMap))
            {
                if (!IsTargetMatchingAttackerType(option.Matched, attackerType, pokemonTypes, megaList))
                {
                    continue;
                }

                var requiredEvolutionCandy = CalculateEvolutionCandyRequired(
                    owned.PokemonNumber,
                    option.Matched.PokemonNumber,
                    evolutionCandyCostMap);
                var requiredFirstTimeMegaEnergy = CalculateFirstTimeMegaEnergyRequired(option.Matched, megaList);
                var isUnevolvedPlaceholder = owned.PokemonNumber != option.Matched.PokemonNumber;
                var unevolvedReserveCap = isUnevolvedPlaceholder
                    ? GetUnevolvedReserveCap(owned.PokemonNumber, evolutionMap, megaList)
                    : int.MaxValue;

                rankedOwned.Add(new RaidCandidate
                {
                    Owned = owned,
                    Matched = option.Matched,
                    EvolvedOwned = option.EvolvedOwned,
                    Rank = option.Rank,
                    FamilyKey = familyRoot,
                    TargetForm = NormalizeForm(option.Matched.Form),
                    CandyBucketPokemonNumber = familyRoot,
                    CandyPromptName = familyNameByRoot.GetValueOrDefault(familyRoot),
                    RequiredEvolutionCandy = requiredEvolutionCandy,
                    RequiredCandyToLevel30 = requiredCandyToLevel30,
                    RequiredDustToLevel30 = requiredDustToLevel30,
                    TotalRequiredCandy = requiredEvolutionCandy + requiredCandyToLevel30,
                    RequiredFirstTimeMegaEnergy = requiredFirstTimeMegaEnergy,
                    CurrentLevel = GetCurrentLevel(owned),
                    CurrentCp = Math.Max(0, owned.CP ?? 0),
                    ProjectedLevel = GetCurrentLevel(owned),
                    ProjectedCp = Math.Max(0, owned.CP ?? 0),
                    IsUnevolvedPlaceholder = isUnevolvedPlaceholder,
                    UnevolvedSpeciesNumber = owned.PokemonNumber,
                    UnevolvedReserveCap = unevolvedReserveCap,
                    FamilyReserveCap = familyReserveCap
                });
            }
        }

        var externallyReserved = globallyReservedOwnedKeys is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(globallyReservedOwnedKeys, StringComparer.OrdinalIgnoreCase);

        var rankOrderedCandidates = rankedOwned
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Owned.PokemonNumber ?? int.MaxValue)
            .ThenByDescending(x => x.Owned.IvAvg ?? int.MinValue)
            .ThenByDescending(x => x.Owned.AtkIV ?? int.MinValue)
            .ThenByDescending(x => IsLucky(x.Owned))
            .ThenByDescending(x => x.Owned.CP ?? int.MinValue)
            .ToList();

        var noInvestmentOrdered = rankedOwned
            .OrderByDescending(x => x.CurrentCp)
            .ThenBy(x => x.Rank)
            .ThenByDescending(x => x.Owned.IvAvg ?? int.MinValue)
            .ThenByDescending(x => x.Owned.AtkIV ?? int.MinValue)
            .ThenByDescending(x => IsLucky(x.Owned))
            .ToList();

        var noInvestmentPool = noInvestmentOrdered
            .Where(x => !externallyReserved.Contains(GetOwnedUniqueKey(x)))
            .ToList();
        var noInvestment = SelectWithTeamConstraints(noInvestmentPool, 6);

        var currentPool = rankOrderedCandidates
            .Where(x => !externallyReserved.Contains(GetOwnedUniqueKey(x)))
            .ToList();
        var currentCore = SelectWithTeamConstraints(currentPool, 6);
        var currentResources = await BuildCurrentResourcesTeamAsync(
            currentCore,
            currentPool,
            dustLimit,
            powerupRequirements,
            candyByPokemonNumber,
            resolveCandyAsync,
            cancellationToken);

        var unlimitedPool = rankOrderedCandidates
            .Where(x => !externallyReserved.Contains(GetOwnedUniqueKey(x)))
            .ToList();
        var unlimited = SelectWithTeamConstraints(unlimitedPool, 6, enforceFamilyMegaCap: true);
        unlimited = EnsureMegaFormCoverage(unlimited, unlimitedPool, 6);
        foreach (var key in unlimited.Select(GetOwnedUniqueKey))
        {
            globallyReservedOwnedKeys?.Add(key);
        }

        return new RaidTeamVariantResult
        {
            NoInvestmentTeam = MapToRankedPokemon(noInvestment, useProjectedCp: false, noInvestmentCosts: true),
            CurrentResourcesTeam = MapToRankedPokemon(currentResources, useProjectedCp: true, noInvestmentCosts: false),
            UnlimitedResourcesTeam = MapToRankedPokemon(unlimited, useProjectedCp: false, noInvestmentCosts: false)
        };
    }

    private static async Task<IList<RaidCandidate>> BuildCurrentResourcesTeamAsync(
        IList<RaidCandidate> coreSix,
        IList<RaidCandidate> allCandidates,
        int dustLimit,
        IReadOnlyDictionary<decimal, PokemonPowerupRequirement> powerupRequirements,
        IDictionary<int, int> candyByPokemonNumber,
        Func<int, string?, CancellationToken, Task<int>>? resolveCandyAsync,
        CancellationToken cancellationToken)
    {
        var selected = coreSix.Select(CloneCandidate).ToList();
        if (selected.Count == 0)
        {
            return selected;
        }

        var remainingCandy = new Dictionary<int, int>(candyByPokemonNumber);
        var remainingDust = Math.Max(0, dustLimit);

        while (selected.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var steps = new List<PowerupStep>(selected.Count);
            foreach (var candidate in selected)
            {
                if (!TryGetNextPowerupStep(candidate.ProjectedLevel, powerupRequirements, out var step))
                {
                    return selected;
                }

                steps.Add(step);
            }

            var dustRequired = steps.Sum(x => x.DustCost);
            if (dustRequired > remainingDust)
            {
                break;
            }

            var candyNeedsByBucket = new Dictionary<int, int>();
            for (var i = 0; i < selected.Count; i++)
            {
                var bucket = selected[i].CandyBucketPokemonNumber;
                candyNeedsByBucket[bucket] = candyNeedsByBucket.GetValueOrDefault(bucket) + steps[i].CandyCost;
            }

            var canPayAll = true;
            foreach (var (bucket, requiredCandy) in candyNeedsByBucket)
            {
                var name = selected.FirstOrDefault(x => x.CandyBucketPokemonNumber == bucket)?.CandyPromptName;
                var available = await GetAvailableCandyAsync(bucket, name, remainingCandy, resolveCandyAsync, cancellationToken);
                if (available < requiredCandy)
                {
                    canPayAll = false;
                    break;
                }
            }

            if (!canPayAll)
            {
                break;
            }

            remainingDust -= dustRequired;
            for (var i = 0; i < selected.Count; i++)
            {
                var candidate = selected[i];
                var step = steps[i];

                remainingCandy[candidate.CandyBucketPokemonNumber] -= step.CandyCost;
                candidate.ProjectedLevel = step.NextLevel;
                candidate.ProjectedCp = ProjectCp(candidate.CurrentCp, candidate.CurrentLevel, candidate.ProjectedLevel);
            }
        }

        var usedOwnedKeys = selected.Select(GetOwnedUniqueKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var replacements = allCandidates
            .Where(x => !usedOwnedKeys.Contains(GetOwnedUniqueKey(x)))
            .Where(x => x.CurrentCp > 1000)
            .OrderByDescending(x => x.CurrentCp)
            .ThenBy(x => x.Rank)
            .ToList();

        foreach (var replacement in replacements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (selected.Count == 0)
            {
                break;
            }

            var weakest = selected
                .OrderBy(x => x.ProjectedCp)
                .ThenByDescending(x => x.Rank)
                .First();

            if (replacement.CurrentCp <= weakest.ProjectedCp)
            {
                continue;
            }

            var trial = selected.Where(x => !ReferenceEquals(x, weakest)).ToList();
            trial.Add(CloneCandidate(replacement));

            var rebalanced = SelectWithTeamConstraints(
                trial.OrderByDescending(x => x.ProjectedCp).ThenBy(x => x.Rank),
                6);

            var replacementKey = GetOwnedUniqueKey(replacement);
            if (rebalanced.Count == selected.Count && rebalanced.Any(x => GetOwnedUniqueKey(x) == replacementKey))
            {
                selected = rebalanced.Select(CloneCandidate).ToList();
            }
        }

        return selected
            .OrderByDescending(x => x.ProjectedCp)
            .ThenBy(x => x.Rank)
            .ToList();
    }

    private static IList<RaidCandidate> SelectWithTeamConstraints(
        IEnumerable<RaidCandidate> orderedCandidates,
        int maxTeamSize,
        bool enforceFamilyMegaCap = false)
    {
        var candidates = orderedCandidates.ToList();
        var selected = new List<RaidCandidate>();
        var usedOwnedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unevolvedCountsBySpecies = new Dictionary<int, int>();
        var familyCountsByRoot = new Dictionary<int, int>();
        var usedMegaTargetsByFamily = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var effectiveFamilyCapByRoot = new Dictionary<int, int>();
        if (enforceFamilyMegaCap)
        {
            effectiveFamilyCapByRoot = candidates
                .GroupBy(x => x.FamilyKey)
                .ToDictionary(
                    x => x.Key,
                    x => Math.Max(
                        1,
                        x.Where(c => IsMegaOrPrimal(c.Matched))
                         .Select(c => NormalizeForm(c.Matched.Form))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .Count()));
        }

        foreach (var candidate in candidates)
        {
            if (selected.Count >= maxTeamSize)
            {
                break;
            }

            var ownedKey = GetOwnedUniqueKey(candidate);
            var alreadyUsedOwned = !usedOwnedKeys.Add(ownedKey);
            if (alreadyUsedOwned)
            {
                continue;
            }

            var isMegaOrPrimalTarget = IsMegaOrPrimal(candidate.Matched);
            var bypassUnevolvedCapForMega = enforceFamilyMegaCap && isMegaOrPrimalTarget;

            if (!bypassUnevolvedCapForMega
                && candidate.IsUnevolvedPlaceholder
                && candidate.UnevolvedSpeciesNumber is int speciesNumber)
            {
                var currentCount = unevolvedCountsBySpecies.GetValueOrDefault(speciesNumber);
                if (currentCount >= candidate.UnevolvedReserveCap)
                {
                    if (!alreadyUsedOwned)
                    {
                        usedOwnedKeys.Remove(ownedKey);
                    }
                    continue;
                }

                unevolvedCountsBySpecies[speciesNumber] = currentCount + 1;
            }

            if (enforceFamilyMegaCap)
            {
                var currentFamilyCount = familyCountsByRoot.GetValueOrDefault(candidate.FamilyKey);
                var maxFamilyCount = effectiveFamilyCapByRoot.GetValueOrDefault(candidate.FamilyKey, candidate.FamilyReserveCap);
                if (currentFamilyCount >= maxFamilyCount)
                {
                    if (!alreadyUsedOwned)
                    {
                        usedOwnedKeys.Remove(ownedKey);
                    }
                    continue;
                }

                familyCountsByRoot[candidate.FamilyKey] = currentFamilyCount + 1;
            }

            if (isMegaOrPrimalTarget)
            {
                var megaTargetKey = $"{candidate.FamilyKey}|{candidate.Matched.PokemonNumber}|{NormalizeForm(candidate.Matched.Form)}";
                if (!usedMegaTargetsByFamily.Add(megaTargetKey))
                {
                    if (!alreadyUsedOwned)
                    {
                        usedOwnedKeys.Remove(ownedKey);
                    }
                    continue;
                }
            }

            selected.Add(CloneCandidate(candidate));
        }

        return selected;
    }

    private static IList<RaidCandidate> EnsureMegaFormCoverage(
        IList<RaidCandidate> selected,
        IList<RaidCandidate> candidatePool,
        int maxTeamSize)
    {
        var result = selected.Select(CloneCandidate).ToList();
        var megaCandidates = candidatePool
            .Where(x => IsMegaOrPrimal(x.Matched))
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Owned.PokemonNumber ?? int.MaxValue)
            .ToList();

        if (megaCandidates.Count == 0)
        {
            return result;
        }

        var familyCapByRoot = candidatePool
            .GroupBy(x => x.FamilyKey)
            .ToDictionary(
                x => x.Key,
                x => Math.Max(
                    1,
                    x.Where(c => IsMegaOrPrimal(c.Matched))
                     .Select(c => NormalizeForm(c.Matched.Form))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Count()));

        var selectedMegaKeys = result
            .Where(x => IsMegaOrPrimal(x.Matched))
            .Select(x => $"{x.FamilyKey}|{x.Matched.PokemonNumber}|{NormalizeForm(x.Matched.Form)}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedFamilyCounts = result
            .GroupBy(x => x.FamilyKey)
            .ToDictionary(x => x.Key, x => x.Count());

        foreach (var candidate in megaCandidates)
        {
            var megaKey = $"{candidate.FamilyKey}|{candidate.Matched.PokemonNumber}|{NormalizeForm(candidate.Matched.Form)}";
            if (selectedMegaKeys.Contains(megaKey))
            {
                continue;
            }

            var currentFamilyCount = selectedFamilyCounts.GetValueOrDefault(candidate.FamilyKey);
            var maxFamilyCount = familyCapByRoot.GetValueOrDefault(candidate.FamilyKey, candidate.FamilyReserveCap);

            if (result.Count < maxTeamSize)
            {
                if (currentFamilyCount >= maxFamilyCount)
                {
                    continue;
                }

                result.Add(CloneCandidate(candidate));
                selectedMegaKeys.Add(megaKey);
                selectedFamilyCounts[candidate.FamilyKey] = currentFamilyCount + 1;
                continue;
            }

            if (currentFamilyCount >= maxFamilyCount)
            {
                continue;
            }

            var removable = result
                .OrderByDescending(x => x.Rank)
                .ThenBy(x => x.Owned.PokemonNumber ?? int.MaxValue)
                .FirstOrDefault(x => x.FamilyKey != candidate.FamilyKey);

            if (removable is null)
            {
                continue;
            }

            var trial = result
                .Where(x => !ReferenceEquals(x, removable))
                .Append(CloneCandidate(candidate))
                .OrderBy(x => x.Rank)
                .ToList();

            var reselected = SelectWithTeamConstraints(trial, maxTeamSize, enforceFamilyMegaCap: true);
            var hasCandidate = reselected.Any(x =>
                x.FamilyKey == candidate.FamilyKey
                && x.Matched.PokemonNumber == candidate.Matched.PokemonNumber
                && string.Equals(NormalizeForm(x.Matched.Form), NormalizeForm(candidate.Matched.Form), StringComparison.OrdinalIgnoreCase));

            if (!hasCandidate)
            {
                continue;
            }

            result = reselected.Select(CloneCandidate).ToList();
            selectedMegaKeys = result
                .Where(x => IsMegaOrPrimal(x.Matched))
                .Select(x => $"{x.FamilyKey}|{x.Matched.PokemonNumber}|{NormalizeForm(x.Matched.Form)}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            selectedFamilyCounts = result
                .GroupBy(x => x.FamilyKey)
                .ToDictionary(x => x.Key, x => x.Count());
        }

        return result;
    }

    private static IList<RankedPokemon> MapToRankedPokemon(
        IEnumerable<RaidCandidate> selected,
        bool useProjectedCp,
        bool noInvestmentCosts)
    {
        return selected
            .Select(x => new RankedPokemon
            {
                Index = x.Rank,
                Name = x.Owned.Name,
                Form = x.Owned.Form,
                PokemonNumber = x.Owned.PokemonNumber,
                CP = useProjectedCp ? x.ProjectedCp : x.CurrentCp,
                HP = x.Owned.HP,
                EvolvedName = x.Matched.PokemonNumber == x.Owned.PokemonNumber ? null : (x.EvolvedOwned?.Name ?? x.Matched.Name),
                EvolvedForm = x.Matched.PokemonNumber == x.Owned.PokemonNumber ? null : x.Matched.Form,
                EvolvedPokemonNumber = x.Matched.PokemonNumber == x.Owned.PokemonNumber ? null : x.Matched.PokemonNumber,
                EvolvedCP = x.Matched.PokemonNumber == x.Owned.PokemonNumber ? null : x.EvolvedOwned?.CP,
                EvolvedHP = x.Matched.PokemonNumber == x.Owned.PokemonNumber ? null : x.EvolvedOwned?.HP,
                RequiredEvolutionCandy = noInvestmentCosts ? 0 : x.RequiredEvolutionCandy,
                RequiredCandyToLevel30 = noInvestmentCosts ? 0 : x.RequiredCandyToLevel30,
                RequiredDustToLevel30 = noInvestmentCosts ? 0 : x.RequiredDustToLevel30,
                TotalRequiredCandy = noInvestmentCosts ? 0 : x.TotalRequiredCandy,
                RequiredFirstTimeMegaEnergy = noInvestmentCosts ? 0 : x.RequiredFirstTimeMegaEnergy
            })
            .ToList();
    }

    private static bool TryGetNextPowerupStep(
        decimal currentLevel,
        IReadOnlyDictionary<decimal, PokemonPowerupRequirement> powerupRequirements,
        out PowerupStep step)
    {
        step = default;
        if (powerupRequirements.Count == 0)
        {
            return false;
        }

        var key = powerupRequirements
            .Keys
            .OrderBy(x => x)
            .FirstOrDefault(x => x >= currentLevel);

        if (!powerupRequirements.TryGetValue(key, out var requirement))
        {
            return false;
        }

        var nextLevel = requirement.LevelAfterPowering.GetValueOrDefault(key + 0.5m);
        if (nextLevel <= currentLevel)
        {
            nextLevel = currentLevel + 0.5m;
        }

        step = new PowerupStep
        {
            CandyCost = Math.Max(0, requirement.CandyToUpgrade ?? 0),
            DustCost = Math.Max(0, requirement.StardustToUpgrade ?? 0),
            NextLevel = nextLevel
        };

        return true;
    }

    private static int ProjectCp(int currentCp, decimal currentLevel, decimal targetLevel)
    {
        if (currentCp <= 0)
        {
            return 0;
        }

        if (targetLevel <= 0m || currentLevel <= 0m)
        {
            return currentCp;
        }

        var scaled = (int)Math.Round(currentCp * (double)(targetLevel / currentLevel));
        return Math.Max(currentCp, scaled);
    }

    private static int GetUnevolvedReserveCap(
        int? sourcePokemonNumber,
        IReadOnlyDictionary<int, HashSet<int>> evolutionMap,
        IReadOnlyList<MegaPokemon> megaPokemon)
    {
        if (sourcePokemonNumber is not int source)
        {
            return 1;
        }

        var reachable = new HashSet<int> { source };
        if (evolutionMap.TryGetValue(source, out var evolutions))
        {
            foreach (var evo in evolutions)
            {
                reachable.Add(evo);
            }
        }

        var megaCount = megaPokemon
            .Where(x => x.PokemonId is int id && reachable.Contains(id))
            .Select(x => $"{x.PokemonId}|{NormalizeForm(x.Form)}|{NormalizeForm(x.MegaName)}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return Math.Max(1, megaCount);
    }

    private static int GetFamilyReserveCap(
        int familyRoot,
        IReadOnlyDictionary<int, HashSet<int>> evolutionMap,
        IReadOnlyList<MegaPokemon> megaPokemon)
    {
        return GetUnevolvedReserveCap(familyRoot, evolutionMap, megaPokemon);
    }

    private static string GetOwnedUniqueKey(RaidCandidate candidate)
    {
        var objectId = RuntimeHelpers.GetHashCode(candidate.Owned);
        return $"{objectId}|{candidate.Owned.Index}|{candidate.Owned.PokemonNumber}|{NormalizeForm(candidate.Owned.Form)}|{candidate.Owned.CP}|{candidate.Owned.HP}";
    }

    private static RaidCandidate CloneCandidate(RaidCandidate source)
    {
        return new RaidCandidate
        {
            Owned = source.Owned,
            Matched = source.Matched,
            EvolvedOwned = source.EvolvedOwned,
            Rank = source.Rank,
            FamilyKey = source.FamilyKey,
            TargetForm = source.TargetForm,
            CandyBucketPokemonNumber = source.CandyBucketPokemonNumber,
            CandyPromptName = source.CandyPromptName,
            RequiredEvolutionCandy = source.RequiredEvolutionCandy,
            RequiredCandyToLevel30 = source.RequiredCandyToLevel30,
            RequiredDustToLevel30 = source.RequiredDustToLevel30,
            TotalRequiredCandy = source.TotalRequiredCandy,
            RequiredFirstTimeMegaEnergy = source.RequiredFirstTimeMegaEnergy,
            CurrentLevel = source.CurrentLevel,
            CurrentCp = source.CurrentCp,
            ProjectedLevel = source.ProjectedLevel,
            ProjectedCp = source.ProjectedCp,
            IsUnevolvedPlaceholder = source.IsUnevolvedPlaceholder,
            UnevolvedSpeciesNumber = source.UnevolvedSpeciesNumber,
            UnevolvedReserveCap = source.UnevolvedReserveCap,
            FamilyReserveCap = source.FamilyReserveCap
        };
    }

    private sealed class RaidCandidate
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
        public required decimal CurrentLevel { get; init; }
        public required int CurrentCp { get; init; }
        public required decimal ProjectedLevel { get; set; }
        public required int ProjectedCp { get; set; }
        public required bool IsUnevolvedPlaceholder { get; init; }
        public int? UnevolvedSpeciesNumber { get; init; }
        public required int UnevolvedReserveCap { get; init; }
        public required int FamilyReserveCap { get; init; }
    }

    private readonly struct PowerupStep
    {
        public required int CandyCost { get; init; }
        public required int DustCost { get; init; }
        public required decimal NextLevel { get; init; }
    }

    private static async Task<int> GetAvailableCandyAsync(
        int pokemonNumber,
        string? pokemonName,
        IDictionary<int, int> remainingCandy,
        Func<int, string?, CancellationToken, Task<int>>? resolveCandyAsync,
        CancellationToken cancellationToken)
    {
        if (remainingCandy.TryGetValue(pokemonNumber, out var existing))
        {
            return existing;
        }

        if (resolveCandyAsync is null)
        {
            remainingCandy[pokemonNumber] = 0;
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var resolved = await resolveCandyAsync(pokemonNumber, pokemonName, cancellationToken);
        var safe = Math.Max(0, resolved);
        remainingCandy[pokemonNumber] = safe;
        return safe;
    }

    /// <summary>
    /// Get the best ranked candidate for a given owned pokemon
    /// based on the hub tier list
    /// </summary>
    private static IEnumerable<(int Rank, HubRankedPokemon Matched, Pokemon? EvolvedOwned)> GetRankCandidatesForOwned(
        Pokemon owned,
        IReadOnlyList<Pokemon> ownedList,
        IReadOnlyDictionary<int, List<HubRankedPokemon>> rankedByNumber,
        IReadOnlyDictionary<int, HashSet<int>> evolutionMap)
    {
        if (owned.PokemonNumber is null)
        {
            return [];
        }

        var candidates = new List<(HubRankedPokemon Ranked, Pokemon? OwnedEvolution)>();

        var selfMatch = FindBestMatchForNumber(owned.PokemonNumber.Value, owned.Form, rankedByNumber);
        if (selfMatch is not null)
        {
            candidates.Add((selfMatch, null));
        }

        candidates.AddRange(FindSpecialFormMatchesForNumber(owned.PokemonNumber.Value, rankedByNumber, "mega", "primal")
            .Select(x => (x, (Pokemon?)owned)));

        if (evolutionMap.TryGetValue(owned.PokemonNumber.Value, out var evolutions))
        {
            foreach (var evolutionNumber in evolutions)
            {
                if (evolutionNumber == owned.PokemonNumber.Value)
                {
                    continue;
                }

                var evolvedOwned = ownedList
                    .Where(x => x.PokemonNumber == evolutionNumber)
                    .Where(x => IsEvolutionFormCompatible(owned.Form, x.Form))
                    .OrderByDescending(x => x.IvAvg ?? int.MinValue)
                    .ThenByDescending(x => x.AtkIV ?? int.MinValue)
                    .ThenByDescending(x => IsLucky(x))
                    .ThenByDescending(x => x.CP ?? int.MinValue)
                    .FirstOrDefault();

                var preferredForm = evolvedOwned?.Form ?? owned.Form;
                var evolutionMatch = FindBestMatchForNumber(evolutionNumber, preferredForm, rankedByNumber);
                if (evolutionMatch is not null)
                {
                    candidates.Add((evolutionMatch, evolvedOwned));
                }

                candidates.AddRange(FindSpecialFormMatchesForNumber(evolutionNumber, rankedByNumber, "mega", "primal")
                    .Select(x => (x, evolvedOwned)));
            }
        }

        return candidates
            .GroupBy(x => $"{x.Ranked.PokemonNumber}|{NormalizeForm(x.Ranked.Form)}|{x.Ranked.Rank}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Select(x => (x.Ranked.Rank ?? int.MaxValue, x.Ranked, x.OwnedEvolution))
            .OrderBy(x => x.Item1)
            .ToList();
    }

    /// <param name="owned"></param>
    /// <param name="ownedList"></param>
    /// <param name="rankedByNumber"></param>
    /// <param name="evolutionMap"></param>
    /// <returns></returns>
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

        if (IsMegaCapable(owned.PokemonNumber.Value, evolutionMap))
        {
            var selfMegaMatch = FindBestSpecialFormMatchForNumber(owned.PokemonNumber.Value, rankedByNumber, "mega", "primal");
            if (selfMegaMatch is not null)
            {
                candidates.Add((selfMegaMatch, owned));
            }
        }

        if (evolutionMap.TryGetValue(owned.PokemonNumber.Value, out var evolutions))
        {
            foreach (var evolutionNumber in evolutions)
            {
                if (evolutionNumber == owned.PokemonNumber.Value)
                {
                    continue;
                }

                var evolvedOwned = ownedList
                    .Where(x => x.PokemonNumber == evolutionNumber)
                    .Where(x => IsEvolutionFormCompatible(owned.Form, x.Form))
                    .OrderByDescending(x => x.IvAvg ?? int.MinValue)
                    .ThenByDescending(x => x.AtkIV ?? int.MinValue)
                    .ThenByDescending(x => IsLucky(x))
                    .ThenByDescending(x => x.CP ?? int.MinValue)
                    .FirstOrDefault();

                var preferredForm = evolvedOwned?.Form ?? owned.Form;
                var evolutionMatch = FindBestMatchForNumber(evolutionNumber, preferredForm, rankedByNumber);
                if (evolutionMatch is not null)
                {
                    candidates.Add((evolutionMatch, evolvedOwned));
                }

                if (IsMegaCapable(evolutionNumber, evolutionMap))
                {
                    var megaMatch = FindBestSpecialFormMatchForNumber(evolutionNumber, rankedByNumber, "mega", "primal");
                    if (megaMatch is not null)
                    {
                        candidates.Add((megaMatch, evolvedOwned));
                    }
                }
            }
        }

        var best = candidates
            .OrderByDescending(x => (x.OwnedEvolution ?? owned).IvAvg ?? int.MinValue)
            .ThenByDescending(x => (x.OwnedEvolution ?? owned).AtkIV ?? int.MinValue)
            .ThenByDescending(x => IsLucky(x.OwnedEvolution ?? owned))
            .ThenByDescending(x => (x.OwnedEvolution ?? owned).CP ?? int.MinValue)
            .ThenBy(x => x.Ranked.Rank ?? int.MaxValue)
            .FirstOrDefault();

        if (best.Ranked is null)
        {
            return null;
        }

        return (owned, best.Ranked.Rank ?? int.MaxValue, best.Ranked, best.OwnedEvolution);
    }

    private static HubRankedPokemon? FindBestSpecialFormMatchForNumber(
        int pokemonNumber,
        IReadOnlyDictionary<int, List<HubRankedPokemon>> rankedByNumber,
        params string[] formKeywords)
    {
        if (!rankedByNumber.TryGetValue(pokemonNumber, out var entries) || entries.Count == 0)
        {
            return null;
        }

        return entries
            .Where(x => formKeywords.Any(keyword => NormalizeForm(x.Form).Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Rank ?? int.MaxValue)
            .FirstOrDefault();
    }

    private static IReadOnlyList<HubRankedPokemon> FindSpecialFormMatchesForNumber(
        int pokemonNumber,
        IReadOnlyDictionary<int, List<HubRankedPokemon>> rankedByNumber,
        params string[] formKeywords)
    {
        if (!rankedByNumber.TryGetValue(pokemonNumber, out var entries) || entries.Count == 0)
        {
            return [];
        }

        return entries
            .Where(x => formKeywords.Any(keyword => NormalizeForm(x.Form).Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Rank ?? int.MaxValue)
            .ToList();
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

        if (string.IsNullOrWhiteSpace(preferredForm))
        {
            var normalForm = entries
                .Where(x => NormalizeForm(x.Form) == "normal")
                .OrderBy(x => x.Rank ?? int.MaxValue)
                .FirstOrDefault();

            return normalForm ?? entries
                .OrderBy(x => x.Rank ?? int.MaxValue)
                .FirstOrDefault();
        }

        var normalizedForm = NormalizeForm(preferredForm);
        return entries
            .Where(x => NormalizeForm(x.Form) == normalizedForm)
            .OrderBy(x => x.Rank ?? int.MaxValue)
            .FirstOrDefault();
    }

    internal static int CalculateCandyRequiredToLevel30(
        Pokemon pokemon,
        IReadOnlyDictionary<decimal, PokemonPowerupRequirement> powerupRequirements)
    {
        const decimal targetLevel = 30m;
        var currentLevel = GetCurrentLevel(pokemon);
        if (currentLevel >= targetLevel)
        {
            return 0;
        }

        var totalCandy = 0;
        foreach (var step in powerupRequirements.OrderBy(x => x.Key))
        {
            if (step.Key < currentLevel || step.Key >= targetLevel)
            {
                continue;
            }

            totalCandy += Math.Max(0, step.Value.CandyToUpgrade ?? 0);
        }

        return totalCandy;
    }

    internal static int CalculateDustRequiredToLevel30(
        Pokemon pokemon,
        IReadOnlyDictionary<decimal, PokemonPowerupRequirement> powerupRequirements)
    {
        const decimal targetLevel = 30m;
        var currentLevel = GetCurrentLevel(pokemon);
        if (currentLevel >= targetLevel)
        {
            return 0;
        }

        var totalDust = 0;
        foreach (var step in powerupRequirements.OrderBy(x => x.Key))
        {
            if (step.Key < currentLevel || step.Key >= targetLevel)
            {
                continue;
            }

            totalDust += Math.Max(0, step.Value.StardustToUpgrade ?? 0);
        }

        return totalDust;
    }

    internal static Dictionary<(int Source, int Target), int> BuildEvolutionCandyCostMap(IEnumerable<PokemonEvolution> evolutions)
    {
        var adjacency = new Dictionary<int, List<(int Target, int CandyCost)>>();

        foreach (var evo in evolutions)
        {
            if (evo.PokemonId is not int sourceId)
            {
                continue;
            }

            if (!adjacency.TryGetValue(sourceId, out var edges))
            {
                edges = [];
                adjacency[sourceId] = edges;
            }

            foreach (var detail in evo.Evolutions)
            {
                if (detail.PokemonId is not int targetId || targetId == sourceId)
                {
                    continue;
                }

                edges.Add((targetId, Math.Max(0, detail.CandyRequired ?? 0)));
            }
        }

        var costMap = new Dictionary<(int Source, int Target), int>();

        foreach (var source in adjacency.Keys)
        {
            var distances = Dijkstra(source, adjacency);
            foreach (var result in distances)
            {
                if (result.Key == source)
                {
                    continue;
                }

                costMap[(source, result.Key)] = result.Value;
            }
        }

        return costMap;
    }

    internal static Dictionary<int, HashSet<int>> BuildEvolutionMap(
        IEnumerable<PokemonEvolution> evolutions,
        IEnumerable<MegaPokemon>? megaPokemon = null)
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

        foreach (var megaId in megaPokemon?
            .Where(x => x.PokemonId is not null)
            .Select(x => x.PokemonId!.Value)
            .Distinct()
            ?? [])
        {
            if (!edges.TryGetValue(megaId, out var set))
            {
                set = new HashSet<int>();
                edges[megaId] = set;
            }

            set.Add(megaId);
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

    private static int CalculateEvolutionCandyRequired(
        int? ownedPokemonNumber,
        int? targetPokemonNumber,
        IReadOnlyDictionary<(int Source, int Target), int> evolutionCandyCostMap)
    {
        if (ownedPokemonNumber is null || targetPokemonNumber is null)
        {
            return 0;
        }

        if (ownedPokemonNumber == targetPokemonNumber)
        {
            return 0;
        }

        return evolutionCandyCostMap.GetValueOrDefault((ownedPokemonNumber.Value, targetPokemonNumber.Value), 0);
    }

    private static bool IsTargetMatchingAttackerType(
        HubRankedPokemon matched,
        string attackerType,
        IEnumerable<PokemonTypeInfo> pokemonTypes,
        IReadOnlyList<MegaPokemon> megaPokemon)
    {
        attackerType = NormalizeForm(attackerType);
        if (matched.PokemonNumber is not int pokemonNumber)
        {
            return false;
        }

        var matchedForm = NormalizeForm(matched.Form);
        var matchedName = NormalizeForm(matched.Name);

        if (IsMegaOrPrimal(matched))
        {
            var megaCandidates = megaPokemon.Where(x => x.PokemonId == pokemonNumber).ToList();
            if (megaCandidates.Count == 0)
            {
                return false;
            }

            IEnumerable<MegaPokemon> selected = megaCandidates;
            if (matchedForm.Contains("mega y") || matchedName.Contains("mega y"))
            {
                selected = megaCandidates.Where(x => string.Equals(x.Form, "Y", StringComparison.OrdinalIgnoreCase));
            }
            else if (matchedForm.Contains("mega x") || matchedName.Contains("mega x"))
            {
                selected = megaCandidates.Where(x => string.Equals(x.Form, "X", StringComparison.OrdinalIgnoreCase));
            }
            else if (matchedForm.Contains("primal") || matchedName.Contains("primal"))
            {
                selected = megaCandidates.Where(x => (x.MegaName ?? string.Empty).Contains("Primal", StringComparison.OrdinalIgnoreCase));
            }
            else if (matchedForm.Contains("mega") || matchedName.Contains("mega"))
            {
                selected = megaCandidates.Where(x => string.Equals(x.Form, "Normal", StringComparison.OrdinalIgnoreCase));
            }

            if (!selected.Any())
            {
                selected = megaCandidates;
            }

            return selected.Any(x => x.Type.Any(t => string.Equals(NormalizeForm(t), attackerType, StringComparison.OrdinalIgnoreCase)));
        }

        var candidates = pokemonTypes.Where(x => x.PokemonId == pokemonNumber).ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var formKeys = BuildCandidateFormKeys(matchedForm);
        var formMatches = candidates
            .Where(x => formKeys.Contains(NormalizeForm(x.Form)))
            .ToList();

        // Conservative fallback: if no form match, only fall back to the Normal form.
        if (formMatches.Count == 0)
        {
            formMatches = candidates
                .Where(x => NormalizeForm(x.Form) == "normal")
                .ToList();
        }

        if (formMatches.Count == 0)
        {
            return false;
        }

        return formMatches.Any(x => x.Type.Any(t => string.Equals(NormalizeForm(t), attackerType, StringComparison.OrdinalIgnoreCase)));
    }

    private static HashSet<string> BuildCandidateFormKeys(string matchedForm)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(matchedForm))
        {
            keys.Add("normal");
            return keys;
        }

        keys.Add(matchedForm);

        if (matchedForm.Contains("shadow", StringComparison.OrdinalIgnoreCase)
            || matchedForm.Contains("purified", StringComparison.OrdinalIgnoreCase))
        {
            keys.Add("normal");
        }

        return keys;
    }

    private static int CalculateFirstTimeMegaEnergyRequired(HubRankedPokemon matched, IReadOnlyList<MegaPokemon> megaPokemon)
    {
        if (matched.PokemonNumber is null || !IsMegaOrPrimal(matched))
        {
            return 0;
        }

        var candidates = megaPokemon
            .Where(x => x.PokemonId == matched.PokemonNumber)
            .ToList();

        if (candidates.Count == 0)
        {
            return 0;
        }

        var form = NormalizeForm(matched.Form);
        var name = NormalizeForm(matched.Name);

        MegaPokemon? selected = null;
        if (form.Contains("mega y") || name.Contains("mega y"))
        {
            selected = candidates.FirstOrDefault(x => string.Equals(x.Form, "Y", StringComparison.OrdinalIgnoreCase));
        }
        else if (form.Contains("mega x") || name.Contains("mega x"))
        {
            selected = candidates.FirstOrDefault(x => string.Equals(x.Form, "X", StringComparison.OrdinalIgnoreCase));
        }
        else if (form.Contains("primal") || name.Contains("primal"))
        {
            selected = candidates.FirstOrDefault(x => (x.MegaName ?? string.Empty).Contains("Primal", StringComparison.OrdinalIgnoreCase));
        }
        else if (form.Contains("mega") || name.Contains("mega"))
        {
            selected = candidates.FirstOrDefault(x => string.Equals(x.Form, "Normal", StringComparison.OrdinalIgnoreCase));
        }

        selected ??= candidates.FirstOrDefault();
        return Math.Max(0, selected?.FirstTimeMegaEnergyRequired ?? 0);
    }

    private static bool IsMegaOrPrimal(HubRankedPokemon matched)
    {
        return NormalizeForm(matched.Form).Contains("mega")
            || NormalizeForm(matched.Form).Contains("primal")
            || NormalizeForm(matched.Name).Contains("mega")
            || NormalizeForm(matched.Name).Contains("primal");
    }

    private static decimal GetCurrentLevel(Pokemon pokemon)
    {
        var level = pokemon.LevelMax ?? pokemon.LevelMin ?? 1m;
        return level < 1m ? 1m : level;
    }

    private static int GetEvolutionLineRoot(int pokemonNumber, IReadOnlyDictionary<int, HashSet<int>> evolutionMap)
    {
        var reverseEdges = new Dictionary<int, HashSet<int>>();
        foreach (var (parent, children) in evolutionMap)
        {
            foreach (var child in children)
            {
                if (child == parent)
                {
                    continue;
                }

                if (!reverseEdges.TryGetValue(child, out var parents))
                {
                    parents = new HashSet<int>();
                    reverseEdges[child] = parents;
                }

                parents.Add(parent);
            }
        }

        var visited = new HashSet<int> { pokemonNumber };
        var queue = new Queue<int>();
        queue.Enqueue(pokemonNumber);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!reverseEdges.TryGetValue(current, out var parents))
            {
                continue;
            }

            foreach (var parent in parents)
            {
                if (!visited.Add(parent))
                {
                    continue;
                }

                queue.Enqueue(parent);
            }
        }

        var roots = visited
            .Where(node => !reverseEdges.ContainsKey(node))
            .ToList();

        return roots.Count == 0 ? pokemonNumber : roots.Min();
    }

    private static Dictionary<int, int> Dijkstra(
        int source,
        IReadOnlyDictionary<int, List<(int Target, int CandyCost)>> adjacency)
    {
        var distances = new Dictionary<int, int> { [source] = 0 };
        var queue = new PriorityQueue<int, int>();
        queue.Enqueue(source, 0);

        while (queue.TryDequeue(out var current, out var currentCost))
        {
            if (currentCost > distances.GetValueOrDefault(current, int.MaxValue))
            {
                continue;
            }

            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var (target, candyCost) in neighbors)
            {
                var nextCost = currentCost + candyCost;
                if (nextCost >= distances.GetValueOrDefault(target, int.MaxValue))
                {
                    continue;
                }

                distances[target] = nextCost;
                queue.Enqueue(target, nextCost);
            }
        }

        return distances;
    }

    private static Dictionary<int, string?> BuildFamilyNameByRoot(
        IReadOnlyList<Pokemon> ownedList,
        IReadOnlyDictionary<int, HashSet<int>> evolutionMap)
    {
        var result = new Dictionary<int, string?>();

        foreach (var pokemon in ownedList.Where(x => x.PokemonNumber is not null))
        {
            var root = GetEvolutionLineRoot(pokemon.PokemonNumber!.Value, evolutionMap);
            if (result.ContainsKey(root))
            {
                continue;
            }

            var rootName = ownedList
                .FirstOrDefault(x => x.PokemonNumber == root)
                ?.Name;

            result[root] = rootName ?? pokemon.Name;
        }

        return result;
    }

    private static bool IsMegaCapable(int pokemonNumber, IReadOnlyDictionary<int, HashSet<int>> evolutionMap)
    {
        return evolutionMap.TryGetValue(pokemonNumber, out var evolutions)
            && evolutions.Contains(pokemonNumber);
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

        var normalized = form.Trim().ToLowerInvariant();
        normalized = normalized.Replace('_', ' ').Replace('-', ' ');
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
        return normalized;
    }

    private static bool IsEvolutionFormCompatible(string? sourceForm, string? targetForm)
    {
        var source = NormalizeForm(sourceForm);
        var target = NormalizeForm(targetForm);

        var sourceShadow = source.Contains("shadow", StringComparison.OrdinalIgnoreCase);
        var sourcePurified = source.Contains("purified", StringComparison.OrdinalIgnoreCase);
        var targetShadow = target.Contains("shadow", StringComparison.OrdinalIgnoreCase);
        var targetPurified = target.Contains("purified", StringComparison.OrdinalIgnoreCase);

        if (sourceShadow)
        {
            return targetShadow;
        }

        if (sourcePurified)
        {
            return targetPurified;
        }

        return !targetShadow && !targetPurified;
    }

    private static int IsLucky(Pokemon pokemon) => pokemon.Lucky.GetValueOrDefault() > 0 ? 1 : 0;
}
