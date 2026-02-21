# Multiplayer RTS Alliance and Victory Architecture

## Scope
This document defines a deterministic model for:
- Player relationships (`Ally`, `Enemy`, optional `Neutral`)
- Optional relationship flags (`shared_vision`, `shared_resources`, `shared_unit_control`)
- Victory conditions:
  - Last surviving player
  - Last surviving alliance
  - Objective-based

Target use case includes dynamic lobbies (2v2, 1v6, FFA) and optional mid-game diplomacy changes.

## Core Principles
- Server-authoritative lockstep or server-tick simulation.
- All state changes are applied at an explicit simulation tick.
- Diplomacy updates are atomic and symmetric for both players in a pair.
- Win checks happen in a fixed order every tick.
- If outcomes are simultaneous, use deterministic tie rules (never real-time wall clock).

## Player Identity and Indexing
Use stable `player_id` values from match start (`0..N-1`) for all arrays and masks.

## Data Model

### Relationship State

```ts
enum RelationState {
  Enemy = 0,
  Ally = 1,
  Neutral = 2, // optional; if disabled, treat all non-allies as Enemy
}
```

### Relationship Flags

```ts
const REL_SHARED_VISION      = 1 << 0;
const REL_SHARED_RESOURCES   = 1 << 1;
const REL_SHARED_UNIT_CONTROL= 1 << 2;
type RelationFlags = number; // uint8 bitset
```

Semantics:
- `shared_vision`: receives current line-of-sight from ally units/buildings.
- `shared_resources`: can transfer, pool, or auto-share resources (mode-specific).
- `shared_unit_control`: can issue commands to ally units (policy-limited).

### Canonical Storage (Matrix)
Use NxN flattened arrays as source of truth.

```ts
interface DiplomacyMatrix {
  state: Uint8Array; // length N*N, values RelationState
  flags: Uint8Array; // length N*N, values RelationFlags
  version: number;   // increments on each accepted diplomacy change
}
```

Index function:

```ts
idx(a, b) = a * N + b
```

Invariants:
- `state[a,a] = Ally`
- `flags[a,a] = 0`
- `state[a,b] == state[b,a]`
- `flags[a,b] == flags[b,a]`
- If `state[a,b] != Ally`, then `flags[a,b] = 0` (recommended hard rule)

### Runtime Acceleration (Bitmask Cache)
For fast checks, maintain derived per-player masks (recomputed when diplomacy changes):

```ts
interface DiplomacyMasks {
  ally_mask: bigint[];
  enemy_mask: bigint[];
  neutral_mask?: bigint[];
  shared_vision_mask: bigint[];
  shared_resources_mask: bigint[];
  shared_unit_control_mask: bigint[];
}
```

Use matrix as canonical, masks as cache only.

## Diplomacy Change Protocol
All diplomacy operations are events with explicit apply tick:

```ts
interface DiplomacyChangeEvent {
  event_id: number;        // strictly increasing sequence per match
  apply_tick: number;      // authoritative simulation tick
  a: number;
  b: number;
  new_state: RelationState;
  new_flags: RelationFlags;
  initiated_by: number;
}
```

Rules:
- Process events ordered by `(apply_tick, event_id)`.
- Apply to both `[a,b]` and `[b,a]` atomically.
- Clear flags if state becomes non-ally.
- Rebuild masks after all diplomacy events for that tick.

## Deterministic Player Lifecycle

### Player Outcome State

```ts
enum OutcomeState {
  Active,
  Defeated_Eliminated,
  Defeated_Surrendered,
  Defeated_DisconnectTimeout,
}
```

### Required Runtime Fields

```ts
interface PlayerMatchState {
  connected: boolean;
  disconnected_at_tick?: number;
  surrendered: boolean;
  outcome: OutcomeState; // starts Active
  elimination_tick?: number;
}
```

### Alive Definition
A player is `alive` iff `outcome == Active`.

### Defeated Definition (Deterministic)
A player transitions from `Active` to defeated when first matching one of:
1. `surrendered == true` -> `Defeated_Surrendered`.
2. Elimination rule is true -> `Defeated_Eliminated`.
3. Disconnected longer than configured grace and no AI takeover -> `Defeated_DisconnectTimeout`.

Suggested elimination rule (explicit and deterministic):
- `owns_any_units == false`
- `owns_any_structures == false`
- `has_pending_spawn_or_respawn == false`

When transition occurs, set `elimination_tick = current_tick` once and never mutate again.

### Disconnect Policy
Configure exactly one policy per match:
- `AI_TAKEOVER`: player remains `Active`; AI controls assets.
- `FREEZE`: assets idle; player remains `Active` until reconnect or timeout.
- `AUTO_SURRENDER_ON_TIMEOUT`: if `current_tick - disconnected_at_tick >= disconnect_grace_ticks`, mark defeated.

Use server ticks only, never wall-clock timestamps.

## Victory Engine

### Evaluation Order Per Tick
Use this order every simulation tick:
1. Apply queued diplomacy events for this tick.
2. Resolve gameplay simulation (commands, combat, economy, spawning).
3. Update player outcome transitions (`Active` -> defeated).
4. Evaluate victory conditions.

This avoids ambiguous outcomes in same-tick betrayal/elimination scenarios.

### Mode 1: Last Surviving Player
- Candidate set: `alive_players`.
- If size == 1: winner is that player.
- If size == 0: draw (or configured tie-break policy).
- Else: no winner yet.

### Mode 2: Last Surviving Alliance
Define alliance groups as connected components of alive players in the undirected graph:
- Nodes: alive players
- Edge between `a,b` if `state[a,b] == Ally`

Evaluation:
- Compute alive alliance components at tick end.
- If exactly 1 component: that component wins.
- If 0 components: draw.
- Else: no winner yet.

Notes:
- This supports dynamic alliance splits/merges.
- Betrayal at tick T can immediately split one alliance into multiple components.

### Mode 3: Objective-Based
Implement objective evaluators as deterministic functions:

```ts
interface ObjectiveEvaluator {
  id: string;
  checkWin(state): WinResult | null;
}
```

`WinResult` should include:
- `winner_players` or `winner_alliance_ids`
- `resolved_at_tick`
- `priority`

If multiple objective wins occur same tick:
1. Higher objective `priority`
2. Higher objective score (if applicable)
3. Lower `event_id` that completed objective
4. Lowest `player_id` fallback (final deterministic tie-break)

## Edge Cases

### Betrayal Mid-Game
At apply tick:
- Relationship flips to `Enemy` (or `Neutral`).
- Clear shared flags immediately.
- Revoke shared unit control instantly.
- Fog of war re-evaluates from new visibility masks.
- Victory check uses post-change relationships in same tick.

### Ally Disconnect
If ally disconnects:
- Relationship does not change automatically.
- Behavior depends on disconnect policy:
  - `AI_TAKEOVER`: alliance remains intact.
  - `AUTO_SURRENDER_ON_TIMEOUT`: player becomes defeated after grace, potentially collapsing alliance strength.

### Surrender
When surrender event is accepted:
- Player becomes `Defeated_Surrendered` at that tick.
- Their diplomacy edges can remain stored but are ignored for alliance-component computation because defeated players are excluded.

### Stalemate
Add optional stalemate detector for long no-progress games.

Example deterministic trigger:
- No change in objective progress, owned assets, or territory for `stalemate_ticks`.

Deterministic resolution options (choose one in match settings):
- `DRAW`
- `SCORE_ADJUDICATION` (fixed score formula + tie-break chain)
- `FORCED_OBJECTIVE` (start sudden-death objective timer)

## Recommended Match Config Schema

```ts
interface MatchRules {
  neutral_enabled: boolean;
  allow_midgame_diplomacy: boolean;
  disconnect_policy: 'AI_TAKEOVER' | 'FREEZE' | 'AUTO_SURRENDER_ON_TIMEOUT';
  disconnect_grace_ticks: number;
  victory_mode: 'LAST_PLAYER' | 'LAST_ALLIANCE' | 'OBJECTIVE';
  stalemate_enabled: boolean;
  stalemate_ticks: number;
  stalemate_resolution: 'DRAW' | 'SCORE_ADJUDICATION' | 'FORCED_OBJECTIVE';
}
```

## Determinism Checklist
- Use integer tick counters only.
- Sort all event application by deterministic keys.
- Avoid floating-point comparisons for win checks.
- Persist `elimination_tick` and `event_id` for replay/debug.
- Ensure all clients/servers run identical alliance component logic.

## Minimum Test Matrix
- 2v2 static alliances, annihilation win.
- 1v6 where 6-player alliance loses one member to surrender.
- FFA with temporary alliance then betrayal on same tick as base kill.
- Disconnect recovery before timeout vs after timeout.
- Simultaneous elimination of last two alive players (draw path).
- Objective completion tie on same tick (tie-break determinism).
