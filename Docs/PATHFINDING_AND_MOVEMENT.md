# Pathfinding and Movement (Deterministic, 1000+ Units)

## Scope and Constraints
- Game style: classic RTS, isometric 2D/2.5D sprite rendering.
- Simulation space: tile grid (`x,y`) with passability and integer terrain cost.
- Scale target: 1000-1500 units with frequent group movement.
- Multiplayer model: deterministic lockstep (server-authoritative input bundles, identical sim on all clients).
- Determinism rule: movement outcomes must depend only on tick-ordered inputs and deterministic state, never wall-clock time.

## Deterministic Simulation Rules
- Use integer/fixed-point math only in movement/pathfinding (`Q16.16` or tile-subcell integers).
- Use stable sort keys everywhere (no hash-map iteration order dependence).
- Use canonical tie-breaks by numeric IDs (`player_id`, `group_id`, `unit_id`, `event_id`).
- Use tick-based scheduling only (`current_tick`, `next_repath_tick`, queue order).
- All random-like behavior is prohibited unless driven by deterministic seed + explicit key.

## Coordinate and Rendering Notes
- Simulation coordinates: grid/tile domain with optional fixed-point sub-tile offsets.
- Rendering coordinates (isometric projection) are derived from simulation state only:
  - `screen_x = (world_x - world_y) * iso_x_scale`
  - `screen_y = (world_x + world_y) * iso_y_scale - height_offset`
- Camera affects only rendering, never simulation.

## 1) Navigation Layers

### 1.1 High-Level Layer: Sector/Region Graph
Purpose: long-distance routing with low CPU cost.

Design:
- Partition map into fixed sectors (recommended `16x16` tiles).
- For each movement class, flood-fill passable tiles into regions.
- Create portal nodes along sector boundaries where contiguous passable runs connect sectors.
- Build graph:
  - Nodes: portals (or sector-region centroids).
  - Edges:
    - intra-sector portal connections with integer travel cost
    - inter-sector portal adjacency edges
  - Edge cost = integer path length + terrain-weighted cost.

Deterministic requirements:
- Sector indexing is row-major.
- Portal extraction order is deterministic (scanline).
- A* tie-break key: `f_cost`, then `h_cost`, then `node_id`.
- Rebuild/invalidate graph by `map_passability_version` and movement class.

### 1.2 Low-Level Layer: Integration + Flow Fields
Purpose: shared local movement for many units toward a common destination/corridor.

Design:
- Build integration field over relevant tiles (goal sector neighborhood or corridor window):
  - Dijkstra/bucketed wavefront from goal using integer costs.
- Derive flow field:
  - For each tile, choose best neighbor with minimum integration cost.
  - Direction encoding: 8-way + stay (`0..8`).
- Units sample the shared flow direction each tick, then apply local collision rules.

Cache key (deterministic):
- `goal_tile`
- `movement_class`
- `map_passability_version`
- `terrain_cost_version`
- optional `region_id/corridor_id`

Cache policy:
- Deterministic LRU by `(last_used_tick, field_key)` with stable key comparison.
- Invalidate on map version changes intersecting field bounds.

## 2) Group Movement

### 2.1 Move Order Representation for N Units
Canonical group order (from lockstep command expansion):

```ts
interface GroupMoveOrder {
  order_id: number;              // monotonic per match stream
  issued_tick: number;
  issuer_player_id: number;
  group_id: number;              // stable for order lifetime
  unit_ids: number[];            // sorted ascending (required)
  destination_tile: { x: number; y: number };
  formation: 'BOX' | 'LINE';
  rally_point_tile?: { x: number; y: number };
  movement_class: number;
}
```

Command expansion rules:
- Expand multi-unit command into `unit_ids` sorted by `entity_id` ascending.
- Reject duplicate `unit_ids`.
- Store one shared `GroupMoveOrder`; do not clone per unit unnecessarily.
- Per-unit runtime state references `group_id` + `slot_index`.

### 2.2 Deterministic Formation Slot Assignment
Formation frame:
- `forward` vector: normalized integer direction from group anchor to destination.
- `right` vector: perpendicular integer direction.
- Group anchor: deterministic centroid from member positions at `issued_tick`.

Slot generation:
- `LINE`:
  - slots on `right` axis around anchor.
  - slot order: left-to-right by signed offset, then slot index.
- `BOX`:
  - `cols = ceil(sqrt(N))`, `rows = ceil(N / cols)` (integer math).
  - row-major slot ordering: front-to-back, left-to-right.

Unit-to-slot assignment:
- Compute each unit's projected coordinates in formation frame using fixed-point dot products.
- Sort units deterministically:
  - `LINE`: `(proj_right, proj_forward, unit_id)`
  - `BOX`: `(proj_forward, proj_right, unit_id)`
- Sort slots by canonical slot ordering.
- Assign by index zipper (`sorted_units[i] -> sorted_slots[i]`).

Rally points:
- Spawned units receive a deterministic implicit move order to producer rally point.
- If rally tile blocked, choose nearest passable tile using deterministic spiral scan (radius, then scan order).

## 3) Local Avoidance and Collision Resolution

### 3.1 Occupancy Model
Use two deterministic grids each tick:
- `occupancy_current[tile] -> unit_id | EMPTY`
- `reservation_next[tile] -> unit_id | EMPTY`

For unit footprints (`1x1`, `2x2`, etc.):
- `FootprintDef` stores tile offsets from anchor tile.
- A move is valid only if all footprint tiles are passable and reservable.

### 3.2 Move Intent and Contention Resolution
Per moving unit, generate candidate tiles in deterministic order:
1. primary flow/slot target
2. sidestep left
3. sidestep right
4. stay in place

Build intents:

```ts
interface MoveIntent {
  unit_id: number;
  group_id: number;
  from_tile: number;
  candidate_tiles: number[]; // ordered
  stuck_ticks: number;
}
```

Global resolution order (deterministic):
- Sort intents by:
1. `priority_band` (if used; otherwise constant)
2. `(unit_id + current_tick) mod 8192` (rotating fairness)
3. `unit_id`

Resolution:
- Iterate sorted intents; first reservable candidate wins.
- If none valid, reserve current tile (stay).
- No implicit simultaneous swap optimization (prevents ambiguous pair ordering).
- Commit phase writes all accepted reservations to `occupancy_current` atomically.

Tie-break for same tile claims:
- Already encoded by global intent order above.
- For identical keys (should not happen), smaller `unit_id` wins.

## 4) Repath Strategy and CPU Budgets

### 4.1 Repath Triggers
Schedule repath when any condition is true:
1. New move order assigned.
2. Goal tile changed.
3. Unit/group blocked for `blocked_repath_threshold_ticks`.
4. Path corridor invalidated by `map_passability_version` change.
5. Formation deviation exceeds threshold for `deviation_repath_threshold_ticks`.
6. Periodic refresh tick reached (`next_repath_tick`).

Periodic refresh:
- `next_repath_tick = current_tick + repath_interval + (owner_id mod repath_stagger_span)`
- `owner_id = group_id` for group routes, `unit_id` for unit routes.

### 4.2 Deterministic Work Queues and Budgets
Maintain three queues:
- `high_level_path_jobs`
- `integration_field_jobs`
- `local_detour_jobs`

Queue sort key:
1. `urgency` (higher first)
2. `enqueue_tick` (lower first)
3. `owner_type` (`GROUP` before `UNIT`)
4. `owner_id` (lower first)

Per-tick budgets (example starting points):
- `max_high_level_jobs_per_tick = 32`
- `max_integration_nodes_per_tick = 20000`
- `max_local_detours_per_tick = 128`

If budget exhausted:
- Leave remaining jobs queued.
- Units keep last valid route/flow and may temporarily hold position.

## 5) Data Structures

```ts
interface MapGrid {
  width: number;
  height: number;
  passable: Uint8Array;          // 0/1 by tile and movement class view
  terrain_cost: Uint16Array;     // integer cost per tile
  passability_version: number;
  terrain_cost_version: number;
}

interface SectorGraph {
  sector_size: number;           // e.g., 16
  sector_count_x: number;
  sector_count_y: number;
  nodes: PortalNode[];
  edges: PortalEdge[];
}

interface PortalNode {
  node_id: number;
  sector_id: number;
  tile_x: number;
  tile_y: number;
  movement_class_mask: number;
}

interface PortalEdge {
  from_node_id: number;
  to_node_id: number;
  cost: number;                  // integer
}

interface IntegrationFieldKey {
  goal_tile_id: number;
  movement_class: number;
  passability_version: number;
  terrain_cost_version: number;
  corridor_id: number;
}

interface IntegrationField {
  key: IntegrationFieldKey;
  bounds_min_x: number;
  bounds_min_y: number;
  bounds_max_x: number;
  bounds_max_y: number;
  integration_cost: Uint32Array;
  flow_dir: Uint8Array;          // 0..8
  created_tick: number;
  last_used_tick: number;
}

interface FootprintDef {
  footprint_id: number;
  offsets: Int16Array;           // packed dx,dy pairs
}

interface ReservationTable {
  occupancy_current: Int32Array; // unit_id or -1
  reservation_next: Int32Array;  // unit_id or -1
}
```

Implementation notes:
- Use typed arrays and integer IDs for deterministic memory/layout behavior.
- Avoid floating-point path scores; use integer fixed-point if needed.

## 6) Tick Pipeline Integration
Integrate with existing deterministic stages (`STATE_MACHINE.md`):

### Stage 2.2 (Command Canonicalization)
- Expand multi-unit commands to sorted `unit_ids`.
- Create/update `GroupMoveOrder`.
- Enqueue path/field jobs for affected groups.

### Stage 2.3 Orders Substep
- Refresh formation anchors and slot assignments for new/changed orders.
- Validate rally points and compute fallback rally tile if blocked.

### Stage 2.3 Movement Substep
1. Consume repath queues within per-tick budgets.
2. Resolve high-level corridor if missing/stale.
3. Build/reuse integration+flow fields.
4. Generate per-unit move intents from flow + formation slot target.
5. Run deterministic reservation/collision resolution.
6. Commit new tile/sub-tile positions atomically.
7. Update `stuck_ticks`, `next_repath_tick`, and movement state.

### Stage 2.3 Combat Substep
- Use committed positions from movement substep.
- Combat target tie-break remains deterministic (`target_entity_id`, then `attacker_entity_id`).

### Stage 3/4
- Movement results feed outcome/victory checks indirectly (unit survival/position), no reordering.

## 7) Test Plan

## 7.1 Deterministic Replay Tests
For each test:
- fixed initial snapshot
- fixed tick-tagged command stream
- fixed expected state hash every `N` ticks

Core cases:
1. `PF_REPLAY_001_GROUP_LINE`
- 200 units, one LINE move order.
- Assert identical slot assignment and final hashes across two independent runs.

2. `PF_REPLAY_002_GROUP_BOX`
- 400 units, BOX move through mixed terrain costs.
- Assert identical corridor, flow field key usage, and end positions.

3. `PF_REPLAY_003_CELL_CONTENTION`
- 100 units funnel into narrow choke, many same-cell intents.
- Assert identical winners/losers per tile claim each tick.

4. `PF_REPLAY_004_DYNAMIC_BLOCK_REPATH`
- Mid-route building placement changes passability.
- Assert repath trigger tick and new route are identical.

5. `PF_REPLAY_005_RALLY_BLOCKED`
- Spawn waves with blocked rally tile.
- Assert deterministic fallback tile selection and spawn movement.

6. `PF_REPLAY_006_DIPLOMACY_INTERACTION`
- In-game diplomacy changes passability permissions (if game rules apply).
- Assert map/alliance version invalidation and movement update are deterministic.

## 7.2 Scale/Stress Tests (1000/1500 Units)
1. `PF_STRESS_1000_OPENFIELD`
- 1000 units, 10 groups, crossing long routes.
- Verify:
  - no nondeterministic divergence
  - queue backlog remains bounded
  - per-tick job budgets are respected

2. `PF_STRESS_1500_CHOKES`
- 1500 units across multiple choke points with continuous orders.
- Verify:
  - deterministic collision outcomes
  - no unbounded repath queue growth
  - stable progression (no permanent deadlock)

3. `PF_STRESS_1500_SPAWN_RALLY`
- Continuous production with rally points and mixed formations.
- Verify:
  - deterministic rally fallback behavior
  - consistent spawn-to-formation integration

## 7.3 Instrumentation Requirements
Persist deterministic debug data in replays:
- per-tick movement hash
- field cache hits/misses by deterministic key
- path/repath queue lengths and processed counts
- reservation conflicts resolved (winner `unit_id`, tile)

All assertions must compare tick-indexed deterministic artifacts, not elapsed time.
