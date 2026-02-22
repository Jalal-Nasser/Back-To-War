# Input and Controls (Unity C#, Mouse-First RTS)

## Scope
This spec defines player input, camera controls, and command translation for a classic-style RTS in Unity.
It is implementation-oriented and deterministic-lockstep compatible.

Goals:
- Familiar RTS UX (left select, right-click context, hotkeys, control groups).
- High unit-count usability (1000+ units).
- Deterministic command generation for networking.

---

## 1) Input Architecture (Unity)

## 1.1 Input Stack
- Unity Input System or legacy input is acceptable; behavior must match this spec.
- Input is processed in this order each frame:
1. UI gating (`IsPointerOverUI` checks).
2. Selection interactions.
3. Command interactions.
4. Camera/minimap interactions.

Recommended components:
- `InputController` (top-level routing)
- `SelectionController`
- `CommandController`
- `ControlGroupController`
- `CameraController`
- `CursorFeedbackController`
- `CommandSerializer` (deterministic quantization + message build)

## 1.2 Pointer World Query
- Ground click position uses one deterministic raycast plane (`Y=0`) or nav terrain collider layer.
- Entity hit tests use fixed layer masks and deterministic raycast sorting by `distance`, then `entity_id`.

---

## 2) Selection

## 2.1 Single Select (Left Click)
- `LMB click` on selectable unit/building:
  - Select that entity.
  - If no modifier, replace selection.
  - If `Shift`, toggle membership (add if absent, remove if present).
- `LMB click` empty terrain (no drag):
  - Clear selection unless `Shift` held.

## 2.2 Drag Box Select
- `LMB down` + drag beyond threshold (`drag_start_px`).
- Selection uses screen-rect -> world frustum test against visible/selectable entities.
- Box select defaults:
  - Units only (buildings optional by rules).
  - Only player-owned controllable entities.
- Modifier behavior:
  - No modifier: replace selection.
  - `Shift`: additive/toggle merge.

Deterministic selection ordering:
- Store selection list sorted by `entity_id` ascending.

## 2.3 Double Click (Optional)
- `LMB double-click` a unit selects same `unit_type` currently in camera view.
- View test uses camera frustum + ownership filter.
- Result selection sorted by `entity_id`.

## 2.4 Control Groups
- Assign: `Ctrl + [1..9]` -> set group to current selection.
- Recall: `[1..9]` -> select group.
- Add to group: `Shift + [1..9]` -> union(current selection, group).
- Optional common RTS behavior:
  - Double-tap group key centers camera on group anchor.

Group storage rules:
- Each group stores `entity_id[]` sorted ascending.
- On recall, remove dead/non-owned entities, keep deterministic order.

---

## 3) Command Issuing

## 3.1 Right-Click Context Command
`RMB` on target resolves command by context priority:
1. Enemy entity in range/valid -> `ATTACK`
2. Resource node and worker selected -> `GATHER`
3. Damaged friendly repairable and worker selected -> `REPAIR`
4. Garrison-capable target and unit can garrison -> `GARRISON`
5. Else -> `MOVE`

If multiple selected unit types do not share capability:
- Split into deterministic sub-commands by capability class.
- Within each class, `entity_ids` sorted ascending.

## 3.2 Explicit Commands
- `A` then `LMB`/`RMB`: `ATTACK_MOVE` to ground/entity.
- `S`: `STOP`.
- `H`: `HOLD_POSITION`.
- `P` then click: `PATROL` waypoint target.

## 3.3 Queueing with Shift
- Holding `Shift` queues command instead of replacing current queue.
- Supports:
  - waypoint move chains
  - queued attack-move
  - queued gather/repair/garrison actions
- Queue order is exactly input order for that tick by `command_seq`.

## 3.4 Rally Points
- Selected production building + `R` (optional) or direct RMB sets rally point.
- Rally target can be ground, resource, or garrisonable object (if supported).
- Serialized as deterministic `SET_RALLY` command.

---

## 4) Camera

## 4.1 Edge Scroll
- If cursor is within `edge_px` from screen border, pan camera in that direction.
- Speed curve may depend on distance-to-edge but must not affect sim (camera-only).

## 4.2 Middle-Mouse Drag Pan
- `MMB hold + drag` pans camera on ground plane.
- Pan uses camera-facing ground projection and preserves deterministic feel locally (not networked).

## 4.3 Mouse Wheel Zoom
- Wheel zoom between clamped min/max heights.
- Optional smoothing; camera state never enters lockstep simulation.

## 4.4 Minimap Controls
- `LMB click` minimap: jump camera to clicked world position.
- `LMB drag` minimap: pan camera continuously.
- Minimap input is blocked when UI overlay captures pointer.

---

## 5) UI Interactions and Cursor Feedback

## 5.1 Prevent Click-Through
- Before handling world input, call UI gate:
  - `EventSystem.current.IsPointerOverGameObject()`
  - plus explicit checks for custom UI panels/minimap masks.
- If pointer is over blocking UI, do not issue world select/command.

## 5.2 Cursor Feedback Rules
Cursor icon should resolve by highest-priority valid action under cursor:
1. Build placement mode -> build cursor (`valid` / `invalid`)
2. Enemy target + selected attacker -> attack cursor
3. Resource + selected worker -> gather cursor
4. Damaged repairable + selected worker -> repair cursor
5. Garrison target + valid unit -> garrison cursor
6. Ground passable -> move cursor
7. Otherwise -> default cursor

Cursor feedback is client-side only and must not alter command semantics.

---

## 6) Deterministic Constraints (Critical)

## 6.1 Input -> Command Translation
- Raw mouse input (float screen/world values) must be quantized before serialization.
- Commands are stamped for a future simulation tick:
  - `target_tick = local_sim_tick + input_delay_ticks`
- Per-player sequence keys:
  - `command_seq` increments by accepted local command in input order.
  - `command_id` unique within command bundle.

## 6.2 Position/Direction Quantization
Use integer representations in network command payloads:
- `tile_x = floor(world_x / tile_size)`
- `tile_y = floor(world_y / tile_size)`
- Optional sub-tile: fixed-point `sub_x`, `sub_y` in signed int16/int32.
- Direction/facing quantized to fixed bins if needed (for patrol/formation orientation).

Never transmit raw floating-point world coordinates as authoritative command data.

## 6.3 Deterministic Entity Lists
- Any multi-entity command payload must contain `entity_ids` sorted ascending.
- Deduplicate entity IDs before serialization.
- Any context split (mixed capabilities) must produce sub-commands in stable capability order, then by `entity_ids[0]`.

## 6.4 Deterministic Context Resolution
- Entity under cursor tie-break: nearest hit distance, then `entity_id`.
- If two command contexts are both valid, apply fixed priority chain from Section 3.1.

---

## 7) Command Message Shape (Recommended)

Use lockstep message model (compatible with `CLIENT_COMMANDS` + per-tick bundles):

```ts
interface ClientCommands {
  match_id: string;
  sender_player_id: number;
  client_input_seq: number;
  ack_bundle_tick: number;
  bundles: Array<{
    target_tick: number;
    commands: SimCommand[];
  }>;
}

interface SimCommand {
  command_id: number;
  op:
    | 'MOVE'
    | 'ATTACK'
    | 'ATTACK_MOVE'
    | 'GATHER'
    | 'REPAIR'
    | 'GARRISON'
    | 'STOP'
    | 'HOLD_POSITION'
    | 'PATROL'
    | 'SET_RALLY';
  entity_ids?: number[];         // sorted asc
  target_entity_id?: number;
  target_tile?: { x: number; y: number }; // quantized
  queue?: boolean;               // true when Shift held
}
```

---

## 8) Examples: Player Actions -> Resulting Command Messages

Assume:
- `input_delay_ticks = 3`
- current local sim tick = `1200`
- therefore `target_tick = 1203`

## Example A: Right-click ground move
Action:
- Selected units: `[101, 205, 333]`
- Player right-clicks ground at world `(48.73, 19.21)` with no Shift

Serialization:
- Quantize to tile `(48,19)`
- Emit:

```json
{
  "type": "CLIENT_COMMANDS",
  "match_id": "m_001",
  "sender_player_id": 0,
  "client_input_seq": 77,
  "ack_bundle_tick": 1198,
  "bundles": [
    {
      "target_tick": 1203,
      "commands": [
        {
          "command_id": 5001,
          "op": "MOVE",
          "entity_ids": [101, 205, 333],
          "target_tile": { "x": 48, "y": 19 },
          "queue": false
        }
      ]
    }
  ]
}
```

## Example B: Shift-queued patrol
Action:
- Selected units `[101,205,333]`
- Player presses `P`, clicks tile `(52,21)` while holding Shift

Result:
- Same target tick policy, queued action:

```json
{
  "target_tick": 1203,
  "commands": [
    {
      "command_id": 5002,
      "op": "PATROL",
      "entity_ids": [101, 205, 333],
      "target_tile": { "x": 52, "y": 21 },
      "queue": true
    }
  ]
}
```

## Example C: Context attack on enemy
Action:
- Selected units `[101,205,333]`
- RMB on enemy entity `9001`

Result:

```json
{
  "target_tick": 1203,
  "commands": [
    {
      "command_id": 5003,
      "op": "ATTACK",
      "entity_ids": [101, 205, 333],
      "target_entity_id": 9001,
      "queue": false
    }
  ]
}
```

## Example D: Mixed selection split (gather + move)
Action:
- Selected entities `[100(worker), 101(soldier)]`
- RMB on resource entity `700`

Deterministic split:
1. `GATHER` for workers
2. `MOVE` fallback for non-workers (or ignore by design; must be fixed and documented)

```json
{
  "target_tick": 1203,
  "commands": [
    {
      "command_id": 5004,
      "op": "GATHER",
      "entity_ids": [100],
      "target_entity_id": 700,
      "queue": false
    },
    {
      "command_id": 5005,
      "op": "MOVE",
      "entity_ids": [101],
      "target_tile": { "x": 48, "y": 19 },
      "queue": false
    }
  ]
}
```

---

## 9) Unity C# Implementation Notes
- Keep input sampling in `Update()`, but command dispatch to net layer should use deterministic per-frame ordering queue.
- Use explicit comparator methods for all sorted collections used in selection/commands.
- Keep camera logic fully decoupled from simulation state mutation.
- Add debug log mode:
  - prints `tick`, `command_seq`, `command_id`, `entity_ids`, quantized targets.
- Replay tests should verify identical command streams for identical action scripts.
