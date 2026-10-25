# Pokémon Ranking & Raid Team Business Logic

This document describes the business logic used to rank a player's owned Pokémon
against Pokémon GO Hub tier lists and assemble raid teams. The logic lives in
`TeamBuilder.Services`, primarily in:

- `TeamBuilder.Services/Services/RankingService.cs` — orchestration entry point
- `TeamBuilder.Services/Helpers/RankingHelper.cs` — core ranking/team-building algorithm
- `TeamBuilder.Services/Services/HubTierListService.cs` — crawls Pokémon GO Hub tier lists
- `TeamBuilder.Data/Responses/HubRankingResponse.cs` / `RankingResponse.cs` — data shapes
- `TeamBuilder.Data/Models/RaidTeamVariantResult.cs` — final per-type output

## 1. Overview

For each of the 18 attacker types (normal, fire, water, electric, grass, ice,
fighting, poison, ground, flying, psychic, bug, rock, ghost, dragon, dark,
steel, fairy), the system builds **three raid team variants**, each capped at
6 Pokémon:

| Variant | Description |
|---|---|
| **No Investment Team** | Best 6 owned Pokémon that are *already* evolved/leveled to match a Hub tier-list entry for that attacker type — no candy, dust, or evolution required. |
| **Current Resources Team** | Best 6 owned Pokémon, upgraded (evolved/powered-up) as far as the player's current candy and stardust allow. |
| **Unlimited Resources Team** | Best 6 owned Pokémon assuming unlimited candy/dust — i.e. the theoretical best team if fully evolved and maxed out. |

The entry point is `IRankingService.BuildRankingResponseAsync`, called once per
attacker type, producing a `RankingResponse` whose `RaidTeamsByType` dictionary
maps attacker type → `RaidTeamVariantResult` (the three team variants above).

## 2. Inputs

`BuildRankingResponseAsync` takes:

- **`ownedPokemon`** — the player's roster, parsed from their uploaded CSV.
- **`hubRanking`** — a `HubRankingResponse` containing, per attacker type, the
  ranked list of `HubRankedPokemon` from Pokémon GO Hub (crawled separately by
  `IHubTierListService`/`IHubApiService`). Rank 1 is the best attacker for
  that type.
- **`dustLimit`** — the player's available stardust, used only for the
  "Current Resources" variant.
- **`candyByPokemonNumber`** — known candy counts per evolution-family root
  Pokémon number. Unknown counts can be resolved lazily via
  `resolveCandyAsync` (prompts the user, or assumes a default amount).
- **`reportProgressAsync`** — optional progress callback invoked as each
  attacker type's teams are built (used to drive UI progress bars).

Supporting reference data (evolutions, mega Pokémon, type chart, powerup
requirements) is fetched once via `IPogoApiService` and reused across all 18
attacker types.

## 3. Step-by-step algorithm (per attacker type)

### 3.1 Build supporting maps

- **Evolution map** (`BuildEvolutionMap`): for every Pokémon number, the set of
  numbers reachable via evolution (including mega evolutions), used to find a
  Pokémon's **evolution-family root** and to enumerate "future" upgrade targets.
- **Evolution candy cost map** (`BuildEvolutionCandyCostMap`): candy cost for
  each `(source, target)` evolution step.
- **Hub tier list lookup**: the Hub ranked list for the current attacker type,
  grouped by Pokémon number for fast lookup.

### 3.2 Generate ranking candidates for each owned Pokémon

For every owned Pokémon with a known species number:

1. Compute its **evolution-family root** and the **family reserve cap** (how
   many team slots a single evolution family — including its mega/primal
   forms — may occupy).
2. Compute **candy required to reach level 30** and **dust required to reach
   level 30** from its current level (`CalculateCandyRequiredToLevel30`,
   `CalculateDustRequiredToLevel30`), used later for the "Current Resources"
   variant's powerup simulation.
3. **No-investment candidate**: if the owned Pokémon's *current* species/form
   already matches an entry in the Hub tier list for this attacker type, it's
   added as a "no investment" candidate — no evolution or candy spend needed.
4. **Investment candidates**: `GetRankCandidatesForOwned` enumerates every
   reachable evolution target (including the Pokémon's current form) and, for
   each one that matches a Hub tier-list entry for this attacker type
   (`IsTargetMatchingAttackerType`, which accounts for type matchups and
   mega/primal forms), creates a `RaidCandidate` carrying:
   - The Hub rank of the matched target (lower is better).
   - Required evolution candy to reach that target
     (`CalculateEvolutionCandyRequired`).
   - Required first-time mega energy, if the target is a mega/primal form
     (`CalculateFirstTimeMegaEnergyRequired`).
   - Whether this candidate is an "unevolved placeholder" (the owned Pokémon
     itself isn't the final evolved form yet) and its reserve cap for how many
     such placeholders of the same species may be used.

### 3.3 Order and select candidates

Candidates are ordered using a consistent tie-break chain, best first:

1. Hub rank (ascending — lower rank = stronger attacker).
2. Owned Pokémon number (stable tie-break).
3. Individual Value average, descending.
4. Attack IV, descending.
5. Whether it is Lucky (favors Lucky Pokémon).
6. Current CP, descending (only for the no-investment ordering).

`SelectWithTeamConstraints` then greedily walks the ordered list and picks up
to 6 candidates while enforcing:

- **No duplicate owned Pokémon** across slots.
- **At most one mega/primal Pokémon** on the team, preferring the
  highest-ranked mega candidate; once a mega target is chosen, only that exact
  family/target combination can fill mega slots.
- **Unevolved placeholder caps** — a given *unevolved* species may only occupy
  as many slots as its `UnevolvedReserveCap` allows (prevents, e.g., stacking
  many unevolved Magikarp when only Gyarados should count).
- **Family reserve caps** (only enforced for the Unlimited Resources variant,
  via `enforceFamilyMegaCap: true`) — limits how many slots a single evolution
  family (counting distinct mega forms) may occupy, so teams aren't dominated
  by one family.

### 3.4 Build each variant

- **No Investment Team**: `SelectWithTeamConstraints` over the no-investment
  candidate pool, ordered by current CP (desc), Hub rank, IV average, Attack
  IV, and Lucky status.
- **Unlimited Resources Team**: `SelectWithTeamConstraints` over *all*
  investment candidates with `enforceFamilyMegaCap: true`, representing the
  best possible team with no resource constraints. Once chosen, these
  Pokémon are marked as "globally reserved" so they are excluded from other
  attacker types' Unlimited teams (preventing the same Pokémon from appearing
  in every single type's "best team").
- **Current Resources Team** (`BuildCurrentResourcesTeamAsync`) — the most
  involved variant:
  1. Start from the same core 6 selected for "Unlimited"-style ordering
     (`currentCore`), cloned so projected stats can be mutated independently.
  2. Verify the team can actually afford the *evolution* candy needed to reach
     each member's matched target (`HasEvolutionCandyAsync`); if not,
     `BuildEvolutionAffordableSelectionAsync` falls back to a team built only
     from candidates whose evolution is currently affordable.
  3. Simulate **incremental power-ups**: for each remaining dust/candy budget,
     repeatedly find the next power-up step for every team member
     (`TryGetNextPowerupStep`, driven by `powerupRequirements`), summing the
     dust and candy cost across the whole team. If the team can afford a
     "round" of simultaneous power-ups (enough dust overall and enough candy
     per evolution-family bucket), apply it and advance everyone's projected
     level/CP; otherwise stop.
  4. **Opportunistic replacement**: after power-up simulation stalls, try
     swapping in stronger benched candidates (CP > 1000) for the weakest
     current team member, as long as the swap still satisfies team
     constraints and doesn't require unaffordable evolution candy
     (`TrySwapEvolutionCandyReservationAsync`).
  5. Final team is ordered by projected CP (desc) then Hub rank.

### 3.5 Map to output

`MapToRankedPokemon` converts the internal `RaidCandidate` list into
`RankedPokemon` DTOs for display, choosing projected vs. current CP/level
depending on the variant, and including resource costs (candy/dust/mega
energy) where relevant (omitted for the No Investment variant).

## 4. Candy resolution

Candy counts are tracked per **evolution-family root** Pokémon number, not per
individual Pokémon, since candy is shared across a family. When a required
candy amount is unknown for a given family:

- If the caller has enabled "assume full candy," a default amount (100) is
  used automatically.
- Otherwise, `resolveCandyAsync` is invoked to prompt the user for that
  family's candy count on demand (and the Raid Builder page caches the answer
  so it's only asked once per session).

## 5. Hub tier list crawling

Independently of ranking, `IHubTierListService.CrawlAllTypesAsync` builds the
`HubRankingResponse` input by crawling `IHubApiService.CrawlBestPerTypeAsync`
for each of the 18 attacker types (up to 200 ranked Pokémon per type), flattens
the results into `HubTierRow` entries, and sorts them by attacker type then
rank. This can also be rebuilt from a previously cached set of rows
(`BuildFromCachedRows`) without additional network calls — used when the Raid
Builder recalculates teams after candy/dust settings change without needing to
re-crawl the Hub.

## 6. Key model types

- **`HubRankedPokemon`** — a single ranked entry from Pokémon GO Hub for one
  attacker type (rank, Pokémon number/form, fast/charged moves, DPS/TDO/score).
- **`RaidCandidate`** (internal) — a candidate owned Pokémon matched to a Hub
  tier-list target, carrying all cost/eligibility data needed for selection.
- **`RankedPokemon`** — the public-facing DTO rendered in the UI for a team
  slot (name, CP, moves, resource costs).
- **`RaidTeamVariantResult`** — the three team variants for one attacker type.
- **`RankingResponse`** — the full result: attacker type → `RaidTeamVariantResult`.
