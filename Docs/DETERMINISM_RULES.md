# Determinism Rules (RTS, 1000+ Units)

## Scope
This document defines canonical deterministic tie-break rules for core RTS simulation conflicts.
It applies to lockstep multiplayer with up to 8 players and 1000+ units.

Authoritative assumptions:
- Integer tick simulation only.
- No wall-clock-based decisions.
- Server assigns authoritative `event_id` for ordered events.
- Tick stage order follows `STATE_MACHINE.md`.

## Deterministic Key Set
Use only these stable keys (or strict subsets) for ordering:
- `tick`
- `stage_order`
- `player_id`
- `entity_id`
- `group_id`
- `command_seq` (per-player, per-tick submission order)
- `command_id` (within a player command bundle)
- `event_id` (authoritative monotonic match event ID)
- `apply_tick` (for deferred events)
- `projectile_id` (deterministically derived, see below)

Never use:
- wall-clock time
- network arrival timing
- hash-map iteration order
- floating-point epsilon comparisons for order decisions

## Global Ordering Hierarchy
When two outcomes seem concurrent, resolve using this hierarchy:
1. `tick`
2. `stage_order`
3. subsystem-specific key chain (defined below)
4. final fallback: smallest stable numeric ID in scope

`stage_order` (within a tick):
1. Diplomacy apply
2. Gameplay simulation
3. Outcome transitions
4. Victory evaluation

## Tie-Break Rules by Subsystem

### 1) Multi-Unit Command Expansion (Unit Ordering)
Input: one command referencing `N` units.

Canonical expansion:
1. Validate and dedupe unit list.
2. Sort unit list by `entity_id` ascending.
3. Generate per-unit operations in that sorted order.

If two commands in same tick affect same unit:
- Global command apply order in Stage 2:
1. `player_id` asc
2. `command_seq` asc
3. `command_id` asc
4. `entity_id` asc (expanded op level)
- First operation that consumes/reserves an exclusive action slot wins.
- Later conflicting operations for that unit in same tick are rejected/no-op with deterministic reason code.

### 2) Formation Slot Assignment
Applies to BOX and LINE formations.

Slot generation:
- LINE: signed lateral offset order (left->right), then slot index.
- BOX: row-major (front->back, left->right), then slot index.

Unit ordering before assignment:
1. project unit into formation frame (fixed-point)
2. sort by formation key:
  - LINE: `(proj_right, proj_forward, entity_id)`
  - BOX: `(proj_forward, proj_right, entity_id)`

Assignment:
- Zip sorted units to sorted slots by index.
- If projected values are identical, `entity_id` breaks ties.

### 3) Movement Cell Conflicts (Same Destination Cell)
Each moving unit produces ordered candidate tiles.

Intent ordering key:
1. `priority_band` (if used; otherwise constant)
2. `(entity_id + tick) mod fairness_modulus` (recommended `8192`)
3. `entity_id`

Resolution:
1. Iterate intents in key order.
2. Reserve first available candidate tile.
3. If no candidate available, reserve current tile (stay).

If two units request same tile:
- Earlier unit by intent ordering wins reservation.
- Later unit retries next candidate or stays.

### 4) Target Selection (Multiple Valid Targets)
When attacker has multiple equally valid targets after gameplay filters:

Deterministic target key:
1. smallest computed tactical score key (integer, lower is better) OR highest priority bucket if bucketized
2. `target_entity_id` asc
3. `attacker_entity_id` asc

If rules specify distance-based preference:
- Use integer distance metric (`manhattan` or fixed-point squared euclidean), then the above tie-break chain.

### 5) Projectile Impact Ordering
If projectile simulation is enabled, all impact events are deterministic.

Projectile identity:
- `projectile_id` is deterministic from spawn context:
  - `(spawn_tick, attacker_entity_id, local_shot_seq)` packed into integer/tuple.

Impact processing (within Stage 2 combat step):
1. `impact_tick` asc
2. `target_entity_id` asc
3. `projectile_spawn_tick` asc
4. `attacker_entity_id` asc
5. `projectile_id` asc

For same-tick lethal overlaps:
- Apply damage in the order above.
- Entity death is committed once health crosses threshold; later impacts on dead target are ignored or redirected by explicit rules (must be consistent across clients).

### 6) Building Placement Conflicts
When two placements compete for overlapping footprint in same tick:

Placement request ordering:
1. `player_id` asc
2. `command_seq` asc
3. `command_id` asc
4. `builder_entity_id` asc

Resolution:
1. First valid request reserves footprint tiles in placement reservation table.
2. Later overlapping requests fail deterministically with `PLACEMENT_RESERVED`.
3. Non-overlapping requests proceed normally.

Tie on exact same builder/command keys (should not occur):
- smaller `entity_id` wins.

### 7) Diplomacy Change Apply Ordering
For in-match diplomacy changes represented as `DiplomacyChangeEvent`:

Event ordering:
1. `apply_tick` asc
2. `event_id` asc

Assignment guarantees:
- `event_id` is authoritative and strictly increasing per match.
- Same-tick accepted requests receive IDs in deterministic request order.

Apply semantics:
- Process events sequentially by ordering key.
- Atomic symmetric update for `[a,b]` and `[b,a]`.
- If multiple events modify the same pair at same `apply_tick`, later `event_id` is final state for end-of-stage snapshot (last write wins in deterministic order).

## Same-Tick Tricky Examples

### Example A: Two Group Commands Share a Unit
State:
- Tick `100`, unit `U=500` appears in two commands from same player.
- `C1`: `command_seq=10`, move east.
- `C2`: `command_seq=11`, move west.

Outcome:
- `C1` applies first (`command_seq` lower).
- `C2` conflicting op for `U` is rejected/no-op.
- `U` executes east move deterministically.

### Example B: Symmetric LINE Formation Assignment
State:
- Tick `220`, units `{101, 102}` have identical projected values for two center slots.

Outcome:
- Tie broken by `entity_id`.
- `101` gets lower-index slot; `102` gets next slot.

### Example C: Two Units Request Same Cell
State:
- Tick `340`, units `2001` and `2002` both request tile `T`.

Outcome:
- Compare intent key `(entity_id + tick) mod 8192`.
- Lower key reserves `T`.
- Other unit takes next candidate or stays.

### Example D: Multiple Equal Targets
State:
- Tick `410`, attacker `A=90` can hit targets `{300, 301}` with equal tactical score and distance.

Outcome:
- Lower `target_entity_id` selected.
- `A` targets `300`.

### Example E: Projectile Double Impact Same Tick
State:
- Tick `512`, target `700` receives two impacts from projectiles `P1` and `P2`.
- Both have same `impact_tick` and same target.

Outcome:
- Compare `(projectile_spawn_tick, attacker_entity_id, projectile_id)`.
- Earlier key applies first; second applies after.
- If first impact kills target, second is ignored by deterministic dead-target rule.

### Example F: Overlapping Building Placements
State:
- Tick `620`, Player 1 and Player 2 place building on overlapping footprint.
- Commands: P1 `command_seq=5`, P2 `command_seq=3`.

Outcome:
- Global ordering is `player_id` first, then `command_seq`.
- If `player_id(1) < player_id(2)`, P1 request resolves first and reserves tiles.
- P2 request fails with `PLACEMENT_RESERVED`.

### Example G: Conflicting Diplomacy Changes Same Apply Tick
State:
- Two accepted events for pair `(A,B)`, both `apply_tick=900`:
  - `event_id=1500`: set `ALLY`
  - `event_id=1501`: set `ENEMY`

Outcome:
- Apply `1500` then `1501`.
- End-of-stage relation is `ENEMY` (last write by higher `event_id`).

## Implementation Checklist
- Enforce all sort keys explicitly before processing.
- Persist key fields in replay logs: `tick`, `stage`, `player_id`, `command_seq`, `command_id`, `entity_id`, `event_id`.
- Assert deterministic invariants in debug builds:
  - identical input -> identical ordered worklists
  - identical ordered worklists -> identical state hash
- Treat any non-deterministic fallback (unordered container iteration, float compare ties) as a defect.
