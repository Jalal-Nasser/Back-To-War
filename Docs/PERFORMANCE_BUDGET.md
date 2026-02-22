# Performance Budget (RTS, 1000+ Units)

## Target Profile
- Render target: `60 FPS` client.
- Simulation tick rate: `10-20 Hz` (`100 ms` to `50 ms` tick interval).
- Unit scale target: `1000+` active units (goal validation up to `1500` in stress).
- Multiplayer mode: deterministic lockstep.

Assumption:
- Simulation runs on a dedicated simulation thread (or equivalent job lane).
- If simulation and rendering share one thread, cut simulation CPU budgets by ~40%.

## CPU Budget Per Tick

Budget policy:
- `Target` budget: normal operating range.
- `Hard` budget: maximum tolerated for stability; exceeding too often fails acceptance.

### At 20 Hz (50 ms interval)
| Subsystem | Target ms/tick | Hard ms/tick |
|---|---:|---:|
| Movement + local avoidance + formation | 4.5 | 7.0 |
| Pathfinding + repath + flow field builds | 3.5 | 6.0 |
| Combat + targeting + projectiles | 4.0 | 6.5 |
| Economy + build/production updates | 1.5 | 3.0 |
| Other sim overhead (state transitions, hashing, bookkeeping) | 0.5 | 1.5 |
| **Total simulation** | **14.0** | **24.0** |

### At 10 Hz (100 ms interval)
| Subsystem | Target ms/tick | Hard ms/tick |
|---|---:|---:|
| Movement + local avoidance + formation | 7.0 | 11.0 |
| Pathfinding + repath + flow field builds | 6.0 | 10.0 |
| Combat + targeting + projectiles | 6.0 | 10.0 |
| Economy + build/production updates | 2.0 | 4.0 |
| Other sim overhead | 1.0 | 2.0 |
| **Total simulation** | **22.0** | **37.0** |

## Deterministic Work Caps (Per Tick)
- `high_level_path_jobs <= 32` @20 Hz, `<= 48` @10 Hz
- `integration_nodes_expanded <= 20,000` @20 Hz, `<= 35,000` @10 Hz
- `local_detour_jobs <= 128` @20 Hz, `<= 192` @10 Hz
- `target_evaluations <= 200,000` @20 Hz, `<= 320,000` @10 Hz
- `projectile_impacts_resolved <= 25,000` @20 Hz, `<= 40,000` @10 Hz

If a cap is reached:
- Defer remaining work deterministically (stable queue order).
- Never switch to nondeterministic shortcuts.

## Memory Budget (Navigation + Movement)

Map sizing assumption for budgeting: up to `512 x 512` tiles.

### Static/Dynamic Grid Budgets
| Component | Budget |
|---|---:|
| Passability grids (all movement classes) | 1.5 MB |
| Terrain cost grid(s) | 1.0 MB |
| Region/sector IDs + portal metadata | 4.0 MB |
| Occupancy + next-tick reservation tables | 4.0 MB |
| Unit movement runtime state (up to 1500 units) | 2.0 MB |
| **Subtotal (non-cache)** | **12.5 MB** |

### Flow/Integration Field Cache Budget
Per cached field memory:
- `field_bytes = width * height * (4 bytes integration + 1 byte flow_dir) + metadata`

Recommended operating point:
- Typical field window: `128 x 128` (`~80 KB` payload per field).
- Max cache entries: `<= 160`.
- Cache budget cap: `<= 16 MB`.

### Total Nav/Movement Memory Cap
- **Hard cap:** `<= 32 MB` (navigation + flow field cache + movement tables).

## Instrumentation Plan

## 1) Counters (per tick + rolling aggregates)
- `units_total`, `units_moving`, `units_idle`, `units_stuck`
- `move_intents_generated`, `cell_conflicts`, `reservation_failures`
- `repath_requests`, `repath_executed`, `repath_deferred`
- `path_jobs_queued`, `path_jobs_processed`, `integration_nodes_expanded`
- `target_candidates_checked`, `attacks_resolved`, `projectile_impacts`
- `build_placement_attempts`, `build_placement_conflicts`
- `economy_transactions`, `production_events`

## 2) Timings
Capture per-tick timings:
- total sim tick time
- movement/pathfinding/combat/economy subtimers
- cache lookup/build timings for integration fields
- queue wait time by job class

Report with rolling `avg`, `p95`, `p99`, `max`.

## 3) Memory Telemetry
- nav grid bytes
- flow cache bytes / entries
- cache hit rate / miss rate / evictions
- reservation table usage density

## 4) Debug Overlays
- sector/portal graph overlay
- integration heatmap + flow vectors
- occupancy/reservation heatmap
- conflict markers (winning unit ID per cell)
- stuck-unit and repath-reason overlay
- projectile impact order debug labels

## 5) Determinism Telemetry
- state hash every `N` ticks (recommended `N=64` or `128`)
- deterministic queue snapshots (ordered keys) in debug builds
- desync first-diff capture: tick, subsystem, ordered worklist IDs

## Stress Test Scenarios

All stress tests run with fixed seeds, deterministic command scripts, and replay hash checks.

1. `STRESS_MARCH_1000`
- 1000 units in 10+ groups marching long distance across mixed terrain.
- Validates formation, flow reuse, and sustained path queue behavior.

2. `STRESS_CHOKE_1000_1500`
- 1000 then 1500 units through 1-2 narrow choke corridors with opposing traffic.
- Validates cell conflict resolution, stuck handling, and repath stability.

3. `STRESS_MASS_COMBAT`
- Large engagements (`500v500`, then `750v750`) with melee+ranged+projectiles.
- Validates targeting, projectile ordering, and combat CPU budget.

4. `STRESS_TOWN_BUILDING`
- Macro-heavy scenario: many workers, frequent building placement, production queues, rally movement.
- Validates economy/build subsystem and placement conflict handling.

Recommended run length:
- Minimum `20 minutes` per scenario per tick-rate profile (`10 Hz` and `20 Hz`).

## Acceptance Criteria (Required Before Raising Unit Cap)

All criteria must pass at the **proposed new unit cap**.

1. Determinism
- Zero replay hash mismatches across at least 3 independent clients for all stress scenarios.
- Zero nondeterministic ordering assertions in debug determinism checks.

2. Simulation CPU
- At 20 Hz:
  - total sim `p95 <= 18 ms`, `p99 <= 24 ms`, `max <= 40 ms`
  - no missed tick interval (`>50 ms`) in steady state after warm-up
- At 10 Hz:
  - total sim `p95 <= 28 ms`, `p99 <= 37 ms`, `max <= 70 ms`

3. Subsystem Budgets
- No subsystem exceeds its `Hard` budget on more than `1%` of ticks.
- No subsystem queue backlog grows unbounded for >`1200` consecutive ticks.

4. Memory
- Navigation + movement memory stays below `32 MB` hard cap.
- Flow field cache stays below `16 MB` hard cap.

5. Client Rendering Coexistence
- With simulation active, render frame time:
  - `p95 <= 16.6 ms`
  - `p99 <= 25 ms`

6. Stability
- No deadlocks/livelocks in movement resolution.
- No crash/assert in 20-minute soak per scenario.

If any criterion fails, unit cap increase is blocked until regression is fixed and revalidated.
