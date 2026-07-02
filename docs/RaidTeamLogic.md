# Raid Team Logic

## Goal

For each attacker type (18 total), generate three raid-team outputs:

1. No Investment Team: Best team possible right now without spending candy or stardust.
2. Current Resources Team: Team built toward ideal rankings using current candy and stardust, with equalized investment across 6 members.
3. Unlimited Resources Team: Best long-term team assuming unlimited candy and stardust.

Each team has 6 slots.

---

## Common Inputs

- attackerType (e.g., Fire)
- Ranked hub candidates for attackerType (ordered by rank)
- Owned Pokémon list
- Evolution graph and evolution candy costs
- Mega and primal metadata
- Power-up requirements by level
- Current resources:
  - Candy inventory (family-level bucket)
  - Stardust total

---

## Core Constraints (Applies to All 3 Outputs)

### Type Eligibility

A candidate is eligible only if its effective typing matches the requested attacker type.

### Form Correctness

Form matching must be strict and safe. Do not mix unrelated forms.

### Single Mega/Primal Per Team

Each generated raid team can include at most one mega or primal target.

- If no valid mega/primal candidate exists, the team can have none.
- If one or more valid mega/primal candidates exist, the selected mega/primal must be the highest-ranked mega/primal candidate for that team build.
- This applies to all three variants: No Investment, Current Resources, and Unlimited Resources.

### Unevolved Slot Cap by Mega Count

If a selected entry is unevolved (base or mid-stage used as a placeholder for a final ranked target), the number of slots allowed for that unevolved species is capped by:

- maxUnevolvedSlots = number of distinct mega/primal evolutions reachable from that line
- If no mega/primal exists in that line, cap is 1

Examples:

- Charmander line -> Mega Charizard X + Mega Charizard Y => cap 2
- Buneary line -> Mega Lopunny => cap 1

This cap is enforced per team.

### Evolution-Family Cap in Unlimited Team

For the Unlimited Resources output, the cap is stricter at the evolution-line level:

- A single evolution family cannot occupy more slots than the number of distinct mega/primal outcomes in that family.
- If no mega/primal exists for the family, cap is 1.

Example:

- Charmander/Charmeleon/Charizard combined cannot exceed 2 slots total in Unlimited Resources.

### No Duplicate Owned Record

Do not reuse the exact same owned record in multiple slots.

### No Reuse Across Raid Teams

Once an owned Pokémon is assigned to one generated raid team, it is reserved and cannot be reused in another generated raid team.

This applies across:

- Variants within a type (No Investment -> Current Resources -> Unlimited Resources)
- Different attacker types (global reservation across all type builds)

---

## 1) No Investment Team

### Intent (No Investment)

Use what is strongest now, with no upgrades or evolutions.

### Steps (No Investment)

1. Build all eligible owned candidates that map to ranked targets for the type.
2. Score candidates by:
   - Highest current CP first
   - Tie-breaker: better rank
   - Tie-breaker: IV and lucky preference
3. Fill up to 6 slots, enforcing the unevolved-slot cap.
4. Output as NoInvestmentTeam[type].

### Notes (No Investment)

- Required candy spend is 0.
- Required stardust spend is 0.
- If fewer than 6 valid entries exist, output fewer than 6.

---

## 2) Current Resources Team

### Intent (Current Resources)

Build toward ideal ranking while still using high-CP Pokémon you already have.

### Required Behavior

- Start from highest-ranked candidates.
- Spend current candy and stardust to improve them.
- If a selected member targets an evolution (owned number != target number), reserve that evolution candy from the family candy bucket before any power-up investment.
- Do not include a candidate that requires evolution candy when the current family candy bucket cannot pay the required evolution cost.
- Split investment equally across the 6 selected members.
- After investment, allow lower-ranked alternatives with CP > 1000 to replace weakest slots.

### Steps (Current Resources)

#### Step 2.1: Initial Ranked Selection

1. Sort eligible candidates by best rank (ascending).
2. Select up to 6 candidates, enforcing unevolved-slot cap.
3. This set is CoreSix.

#### Step 2.2: Equalized Investment

1. Define a shared level progression for CoreSix.
2. Iteratively raise all 6 equally (same step for each), consuming:
   - Candy from each member’s family bucket
   - Global stardust pool
3. Stop when the next equal step cannot be paid for all 6.

Result: each member has projected post-investment CP.

#### Step 2.3: CP>1000 Fallback Replacement

1. Build remaining eligible lower-ranked pool.
2. Filter to current CP > 1000.
3. Compare these candidates against weakest CoreSix members.
4. Replace weakest slots when fallback members provide better immediate battle value, while still enforcing all constraints.

Result: CurrentResourcesTeam[type].

### Notes (Current Resources)

- This output is a hybrid: rank-forward planning plus immediate usability.
- Report:
  - Total candy consumed by family
  - Total stardust consumed
  - Final projected CP per member

---

## 3) Unlimited Resources Team

### Intent (Unlimited Resources)

Show the ideal long-term team with no resource limits.

### Steps (Unlimited Resources)

1. From eligible ranked candidates, choose best rank-first composition up to 6 slots.
2. Assume all selected members can be evolved/powered to the configured target endpoint.
3. Enforce unevolved-slot cap.
4. Enforce evolution-family mega-count cap (family-wide cap).
5. Output as UnlimitedResourcesTeam[type].

### Notes (Unlimited Resources)

- No candy or stardust gating in selection.
- This is the strategic end-state team.

---

## Recommended Output Shape (Per Type)

```text
TypeRaidTeamResult {
  attackerType,
  noInvestmentTeam: RaidTeam,
  currentResourcesTeam: RaidTeam,
  unlimitedResourcesTeam: RaidTeam
}

RaidTeam {
  members: RaidTeamMember[] (max 6),
  summary: {
    totalCandySpent?,
    totalStardustSpent?,
    constraintsApplied
  }
}
```

---

## Validation Checklist

For each attacker type:

- [ ] Exactly 3 team variants are produced.
- [ ] No-investment team uses zero spend.
- [ ] Current-resources team invests equally across the initial six.
- [ ] CP>1000 fallback replacement is applied after equalized investment.
- [ ] Unlimited team ignores resource limits.
- [ ] Unevolved-slot cap by mega-count is enforced in all three.
- [ ] Unlimited team enforces family-wide mega-count cap.
- [ ] No owned Pokémon is reused across team variants.
- [ ] No owned Pokémon is reused across attacker types.
- [ ] Form/type matching remains strict and correct.
