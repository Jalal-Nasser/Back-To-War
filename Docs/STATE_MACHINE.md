# Deterministic State Machine for Multiplayer RTS

This document defines execution order and state transitions for same-tick edge cases.
`ARCHITECTURE.md` is the source of truth; this file operationalizes it into a replay-safe state machine.

## Deterministic Constants
- Time unit: integer simulation tick (`tick`).
- No wall-clock decisions are allowed.
- Player iteration order: ascending `player_id`.
- Diplomacy event order: ascending `(apply_tick, event_id)`.
- Outcome transition priority for an `Active` player at Stage 3:
1. `surrendered == true` -> `Defeated_Surrendered`
2. elimination predicate true -> `Defeated_Eliminated`
3. disconnect timeout predicate true -> `Defeated_DisconnectTimeout`

## State Diagrams (Text)

### Match Lifecycle
```text
[Created]
  -> (lobby initialized) [LobbyOpen]
  -> (start command accepted; start_tick fixed) [Running]
  -> (Stage 4 victory returns terminal result) [Resolved]
  -> (result persisted/finalized) [Closed]

Optional:
[LobbyOpen] -> (cancel) [Closed]
```

Transition notes:
- `Running -> Resolved` can only occur at Stage 4 of a tick.
- `Resolved` result is immutable (`winner set` or `draw`).

### Player Outcome Lifecycle
```text
Outcome FSM (single source of truth):
[Active]
  -> (Stage 3: surrendered) [Defeated_Surrendered]
  -> (Stage 3: eliminated) [Defeated_Eliminated]
  -> (Stage 3: disconnect timeout) [Defeated_DisconnectTimeout]

All Defeated_* states are terminal (no exits).
```

```text
Connection sub-FSM (orthogonal runtime state):
[Connected]
  -> (disconnect observed at tick D) [Disconnected(timer starts)]
  -> (reconnect accepted before timeout) [Connected]

[Disconnected(timer starts)]
  -> (policy AUTO_SURRENDER_ON_TIMEOUT and Stage 3 timeout predicate true) [Triggers Defeated_DisconnectTimeout in Outcome FSM]
```

Transition notes:
- If multiple defeat predicates are true in the same Stage 3, apply the priority chain above.
- `elimination_tick` is written once at transition and never modified.

### Diplomacy Lifecycle (Including Queue)
```text
Per relation pair (a,b), a != b:
[Stable(state, flags)]
  -> (request validated) [QueuedChange(apply_tick, event_id, new_state, new_flags)]
  -> (Stage 1 at apply_tick; in event_id order) [Applying]
  -> (atomic symmetric write to [a,b] and [b,a], flags normalization) [Stable(state, flags)]
```

Queue/event lifecycle:
```text
[Proposed] -> [Validated] -> [Queued] -> [Applied] -> [Archived]
```

Transition notes:
- Atomic update must preserve symmetry for both matrix entries in one operation.
- If `new_state != Ally`, `flags = 0` for that pair.

## Diplomacy Request -> `DiplomacyChangeEvent`

Authority and ID assignment:
- The authoritative server/simulation host assigns `event_id`.
- `event_id` comes from a single monotonic `next_event_id` counter (strictly increasing per match).
- If multiple diplomacy requests are accepted in the same tick, assign `event_id` in deterministic request order: `(request_tick, initiated_by, request_seq)`.

`apply_tick` selection:
- Let `t` be the tick where the request is validated/accepted.
- Use `apply_tick = t + max(input_delay_ticks, diplomacy_delay_ticks)`.
- If `diplomacy_delay_ticks` is not configured separately, use `apply_tick = t + input_delay_ticks`.

Validation and rejection (run in this exact order):
1. match must be `Running`
2. `allow_midgame_diplomacy == true`
3. `a != b` and both player IDs in range
4. both players currently `outcome == Active`
5. `new_state` is allowed by rules (`Neutral` requires `neutral_enabled`)
6. if `new_state != Ally`, effective flags must be `0` (normalize to `0`)
7. request must produce a real change (`state` or effective `flags` differs)

Deterministic rejection:
- On first failed validation step, reject with a stable `reject_code` and do not allocate an `event_id`.
- Rejected requests never enter the diplomacy queue.

## Canonical Tick Pipeline

Every tick executes in this exact order.

1. Stage 0: Tick Setup
- Load previous tick snapshot.
- Materialize all events with `apply_tick == tick`.
- Build deterministic worklists.

2. Stage 1: Diplomacy Apply
- Process `DiplomacyChangeEvent` sorted by `(apply_tick, event_id)`.
- Apply symmetric matrix writes atomically.
- Normalize flags (`state != Ally => flags = 0`).
- Recompute diplomacy masks after all Stage 1 diplomacy events.

3. Stage 2: Gameplay Simulation
- Process accepted gameplay/control inputs for this tick in deterministic order.
- Stage 2.1 Control-state updates: apply reconnect/surrender/disconnect-state inputs first, sorted by `(tick, player_id, command_seq)`.
- Stage 2.2 Command canonicalization: expand multi-unit commands into per-entity operations sorted by `entity_id`.
- Stage 2.3 Subsystem execution in fixed order (example: economy -> orders -> movement -> combat -> spawning -> objective progress).
- Combat tie-break when multiple valid choices exist:
1. lower `target_entity_id`
2. lower `attacker_entity_id`
- Reconnect acceptance in Stage 2.1 is final for this tick and must be visible to Stage 3 timeout checks.

4. Stage 3: Outcome Transitions
- For each `player_id` ascending, if `outcome == Active`, evaluate defeat predicates in priority order:
1. surrendered
2. eliminated (no units, no structures, no pending spawn/respawn)
3. disconnect timeout (policy-dependent)
- First matching predicate wins; write terminal `OutcomeState` and `elimination_tick = tick`.

5. Stage 4: Victory Evaluation
- Evaluate only after Stage 1-3 state is fully applied.
- `LAST_PLAYER`: winner if exactly one alive player; draw if zero alive.
- `LAST_ALLIANCE`: build connected components over alive players with `Ally` edges; winner if exactly one component, draw if zero.
- `OBJECTIVE`: evaluate objective wins; if multiple at same tick use:
1. higher `priority`
2. higher objective score
3. lower objective-completion `event_id`
4. lower winner `player_id`

6. Stage 5: Commit
- Persist tick snapshot, event cursors, and deterministic hash/checkpoint.
- If Stage 4 resolved terminal match state, transition `Running -> Resolved`.

## Deterministic Key Hierarchy

Use this hierarchy whenever multiple outcomes appear concurrent.

1. `stage_order` (Stage 1 before 2 before 3 before 4 before 5)
2. event ordering keys (for queued events): `(apply_tick, event_id)`
3. within Stage 2 subsystem order (fixed constant order in code)
4. player scan order in Stage 3: `player_id` ascending
5. outcome priority in Stage 3: `Surrendered` > `Eliminated` > `DisconnectTimeout`
6. objective tie-break keys in Stage 4:
1. `priority` desc
2. `objective_score` desc
3. `event_id` asc
4. `winner_min_player_id` asc

Objective completion `event_id` definition:
- Use one field name only: `event_id`.
- It is the authoritative ID of the gameplay event that first makes the objective true.
- Generation is deterministic via the same monotonic match `next_event_id` allocator in Stage 2 event processing order.
- If more than one objective becomes true from the same simulation step, each completion gets its own `event_id` in deterministic objective scan order (`objective_id` ascending).

## Same-Tick Timeline Examples

### 1) Betrayal + Elimination
Scenario: `A` and `B` start allied. At tick `120`, betrayal event `A<->B -> Enemy` and `B` loses last structure in combat.

| Tick | Stage 1 (Diplomacy) | Stage 2 (Gameplay) | Stage 3 (Outcome) | Stage 4 (Victory) |
|---|---|---|---|---|
| 119 | A-B = Ally | B has 1 structure | all Active | no winner |
| 120 | apply `DiplomacyChangeEvent(event_id=900)` => A-B Enemy | combat destroys B last structure | B -> `Defeated_Eliminated` | evaluated with post-betrayal graph |

Resolution rule:
- Keys: `stage_order` then `(apply_tick,event_id)` then Stage 3 priority.
- Betrayal is effective before combat; elimination is evaluated after combat.

### 2) Betrayal + Objective Completion
Scenario: `A` betrays `B` at tick `200`; `B` completes an objective in the same tick.

| Tick | Stage 1 (Diplomacy) | Stage 2 (Gameplay) | Stage 4 (Objective Eval) | Result |
|---|---|---|---|---|
| 199 | A-B = Ally | objective at 90% | none | no winner |
| 200 | apply betrayal event `(event_id=1001)` | B captures final objective node (`event_id=3340`) | objective winner computed after betrayal | B wins as solo, not as A-B alliance |

Resolution rule:
- Keys: `stage_order` then objective tie-break keys (`priority`, `score`, `event_id`, `winner_min_player_id`).
- Alliance membership used by objective resolver is the Stage 1 post-betrayal state.

### 3) Surrender + Last Structure Destroyed
Scenario: Player `P` sends surrender command; in same tick their last structure is destroyed.

| Tick | Stage 2 (Gameplay) | Stage 3 (Outcome checks for P) | Final Outcome |
|---|---|---|---|
| 310 | surrender accepted; last structure destroyed in combat | both predicates true; apply priority chain | `Defeated_Surrendered` |

Resolution rule:
- Keys: `stage_order` then Stage 3 outcome priority.
- `Surrendered` outranks `Eliminated` for same-tick dual truth.

### 4) Disconnect Timeout + Reconnect Arrival
Scenario: `Q` reaches grace boundary at tick `450`; reconnect event also arrives for `apply_tick=450`.

| Tick | Stage 2 (Control Inputs) | Stage 3 (Outcome) | Final Outcome |
|---|---|---|---|
| 449 | disconnected, not timed out yet | timeout false | `Active` |
| 450 | reconnect accepted and `connected=true` | timeout predicate now false | remains `Active` |

Resolution rule:
- Keys: `stage_order` then input order in Stage 2.
- Reconnect applied in Stage 2 is visible to Stage 3 timeout evaluation in same tick.

### 5) Ally Disconnect Affecting Last-Alliance Victory
Scenario: Last-alliance mode. Red alliance `{A,B}` vs Blue `{C}`. At tick `500`, `A` is eliminated and disconnected ally `B` hits timeout.

| Tick | Stage 2 (Gameplay) | Stage 3 (Outcome) | Stage 4 (Last-Alliance) | Result |
|---|---|---|---|---|
| 499 | A alive, B disconnected (near timeout), C alive | none | components: `{A,B}`, `{C}` | no winner |
| 500 | C destroys A last structure | A -> `Defeated_Eliminated`; B -> `Defeated_DisconnectTimeout` | alive components: `{C}` only | C wins |

Resolution rule:
- Keys: `stage_order` then Stage 3 player scan (`player_id` asc) with outcome priority.
- Last-alliance graph excludes defeated players after Stage 3, so B timeout can change component count immediately.

### 6) Simultaneous Last-Two-Player Elimination (Draw Policy)
Scenario: Last-player mode. Only `X` and `Y` alive; both lose final assets in same combat step.

| Tick | Stage 2 (Gameplay) | Stage 3 (Outcome) | Stage 4 (Last-Player) | Result |
|---|---|---|---|---|
| 600 | X and Y final structures destroyed | X -> `Defeated_Eliminated`, Y -> `Defeated_Eliminated` | alive count = 0 | Draw |

Resolution rule:
- Keys: `stage_order` then Stage 3 player scan order.
- Draw decision is deterministic from alive count at Stage 4 (`0 => draw`).

## Test Cases

Each case should be executable as a deterministic replay:
- fixed initial snapshot
- fixed tick-tagged event stream
- fixed expected terminal snapshot/hash

| Test ID | Scenario | Replay Inputs (deterministic) | Assertions |
|---|---|---|---|
| `SM_001_BETRAYAL_ELIM` | betrayal + elimination | init A-B allied; schedule betrayal `apply_tick=120,event_id=900`; combat seed destroys B structure at tick 120 | diplomacy at tick 120 stage 1 is Enemy; B outcome becomes `Defeated_Eliminated`; victory uses post-betrayal graph |
| `SM_002_BETRAYAL_OBJECTIVE` | betrayal + objective completion | betrayal `apply_tick=200,event_id=1001`; objective completion event `event_id=3340` at tick 200 | objective winner computed after betrayal; tie-break chain stable across replays |
| `SM_003_SURRENDER_ELIM` | surrender + last structure destroyed | surrender command for P at tick 310; scripted combat removes final structure tick 310 | P ends as `Defeated_Surrendered` (not eliminated); `elimination_tick=310` |
| `SM_004_TIMEOUT_RECONNECT` | disconnect timeout + reconnect arrival | disconnect at tick 430, grace=20; reconnect accepted with `apply_tick=450` | Q remains `Active`; no disconnect-timeout defeat at tick 450 |
| `SM_005_ALLY_TIMEOUT_ALLIANCE_WIN` | ally disconnect affects last-alliance | last-alliance mode; A eliminated at tick 500; B timeout also true at tick 500 | Stage 4 components collapse to `{C}` only; C declared winner |
| `SM_006_DOUBLE_ELIM_DRAW` | simultaneous last-two elimination | last-player mode; scripted symmetric combat removes X and Y final assets tick 600 | both defeated at tick 600; Stage 4 alive count 0; draw result |

## Implementation Notes
- Store and replay deterministic metadata:
  - `event_id`
  - `apply_tick`
  - `elimination_tick`
  - per-tick stage checksum/hash
- For debugability, log per-tick stage boundaries and key-sorted worklists.
