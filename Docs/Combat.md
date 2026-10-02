# Combat rules

## Element IDs

| Family | Base | Reactive stages | First/second reaction stages |
|---|---:|---|---|
| Fire | 100 | 100–107 in current rules | 110 / 111 |
| Water | 200 | 200–201 in current rules | 210 / 211 |
| Electricity | 300 | 300–302 in current rules | 310 / 311 |
| Stone | 400 | 400 / 401 | 410 / 411 (lava) |

ElementState provides named family/stage interpretation. A reactive state has an ID of at least 100 and a suffix below 10. First reaction stages have suffix 10; spent effects have suffix 11. Ordinary stone blocks walking and sight. Lava does not use the ordinary-stone wall classification.

ElementDefinitions supplies damage, alpha, mana cost, decay effects, and default reaction priority. Water 202–207 have definitions, although the authored reaction text does not produce them. Definitions are distinct from reachable states.

## Resolution order

### 1. Fading

Clone the incoming board. First clear old spent effects on existing cells. Then visit fading origins in row-major order and inspect the original phase snapshot.

All four fading types share cardinal geometry. A target is eligible only when terrain permits spells and the elevation ray passes. Replacement rules are:

- Empty/non-element targets and spent effects may be replaced.
- Steam decay can replace fire-family states.
- Charged electricity decay can replace water-family states.
- Accepted targets receive source type + 1.
- The fading origin clears.

The fading phase's newly written spent effects survive until a subsequent tick. They are not removed by the initial clear pass.

### 2. Ordinary reactions

Read the complete post-fading snapshot. For each reactive origin, inspect the authored reactions for its element family.

Every requirement must refer to an existing cell, satisfy its exact or family match, and pass the ordered elevation test. Successful reactions queue outputs.

### 3. Overlap reactions

Compare the post-reaction board with the retained post-fading snapshot. Eligible changed-family cells query reactions of the previous family. An overlap rule must have exactly one requirement at the origin that matches the incoming type.

The same output queue/apply implementation handles ordinary and overlap phases. Matching remains different.

## Priority and snapshots

Output priorities are baked at startup from the output tile definition unless the spell supplies an explicit fourth tuple field. Outputs are sorted by ascending priority and then ascending insertion order. They are applied in that order, so the later accepted write wins.

Insertion order follows row-major origins, authored rule order, direction order, and output order. Each output has a unique insertion order within its phase.

Walls are extracted once when applying a phase. Writes within that phase do not change its wall snapshot. Each phase clones its input, so the overlap snapshot can be retained by reference instead of cloned again.

Do not reorder authored rules, deduplicate symmetric rotations, merge phases, or change when walls are sampled as a mechanical optimization. Each can affect the winning write or visual orientation.

## Visual ownership

ReactionResolver creates ReactionVisual records, containing effect ID, origin, direction, and accepted output cells. It owns a cell-to-visual map throughout the tick.

When another accepted write claims a cell, the previous visual loses that cell. A write with no effect also removes the previous owner. ParticleVFX only plays nonempty effects with known catalog entries after all phases finish.

This prevents losing reactions or superseded writes from displaying effects they no longer own. The same effect records are reused only within one resolution result; consumers should finish consuming them before the next Resolve call.

## Placement and mana

SpellQueue records one spell and its original cost per queued coordinate. Reserved mana is the sum of those costs. Removing the last spell explicitly resets the reservation to zero.

GameLogic exposes spendable mana as max(0, current mana minus reservations). Regeneration uses a placement multiplier and a maximum of max mana plus reservations.

Submitting:

1. Invalidates queued cells against current terrain/state.
2. Requires a nonempty queue and the five-mana submission charge.
3. Charges reserved costs plus the submission charge.
4. Applies all queued types.
5. Clears reservations/previews and performs one presentation batch.

Queue invalidation runs after every combat phase. A cell can become invalid in an intermediate phase and valid again later; postponing invalidation until the end would change behavior.

Already queued spells do not recheck player range/sight on submission. They survive the player moving away if terrain and cell state remain valid.

## Movement, visibility, and elevation

Casting uses square (Chebyshev) range and line of sight. It does not use elevation.

Blink destinations are limited by both the cast visibility region and Blink range, must be walkable, must pass the elevation ray, and must differ from the origin. Enemies may occupy the destination. The movement outline and teleport hover marker use the same destination eligibility as Blink. Spatial outlines intentionally omit mana and cooldown checks; actual Blink checks those when issuing the command.

The wall ray allows an exact corner when at least one side is open. Elevation permits consecutive differences of -0.5, 0, or +0.5, and requires at least one side to connect both diagonal cells. The direct diagonal difference must also pass.

Wall and elevation tests remain independent. The open side for one test need not be the same side as for the other. Lava's target-wall exception does not bypass intermediate walls or the two-side corner rule.

Enemy movement uses cardinal path steps, wandering, lookahead, and smoothing. The scalar velocity cap is sqrt(3) times speed.
