# Command Model (Unity C#, Deterministic Lockstep RTS)

## Scope
This document defines the deterministic command payload model for lockstep simulation.
It is Unity-oriented but uses serialization structs with no Unity types.

Goals:
- Deterministic command generation and replay.
- Stable ordering for multi-unit inputs.
- Quantized targets suitable for network sync.

## 1) Command Types

```csharp
public enum CommandType : byte
{
    Move = 1,
    Attack = 2,
    AttackMove = 3,
    Stop = 4,
    Hold = 5,
    Patrol = 6,
    Gather = 7,
    Build = 8,
    Train = 9,
    Research = 10,
    SetRallyPoint = 11,
    SetAlliance = 12 // only valid when diplomacy is enabled
}
```

## 2) Serialized C# Struct Definitions (No Unity Types)

```csharp
public enum TargetKind : byte
{
    None = 0,
    Entity = 1,
    Position = 2,
    Alliance = 3
}

public enum AllianceRelation : byte
{
    Enemy = 0,
    Ally = 1,
    Neutral = 2
}

// Position in milli-tiles: 1000 units = 1 tile
public readonly struct QuantizedPos2
{
    public readonly int XMt;
    public readonly int YMt;

    public QuantizedPos2(int xMt, int yMt)
    {
        XMt = xMt;
        YMt = yMt;
    }
}

public readonly struct AllianceTarget
{
    public readonly byte OtherPlayerId;
    public readonly AllianceRelation Relation;
    public readonly byte SharedFlags; // bit0=vision, bit1=resources, bit2=unit_control

    public AllianceTarget(byte otherPlayerId, AllianceRelation relation, byte sharedFlags)
    {
        OtherPlayerId = otherPlayerId;
        Relation = relation;
        SharedFlags = sharedFlags;
    }
}

public readonly struct SimCommand
{
    public readonly uint ApplyTick;
    public readonly byte PlayerId;
    public readonly uint CommandSeq;              // monotonic per player
    public readonly CommandType Type;
    public readonly bool Queued;                  // true when Shift queueing

    // Must be sorted ascending and unique.
    public readonly uint[] SelectedEntityIds;

    public readonly TargetKind TargetKind;
    public readonly uint TargetEntityId;          // valid when TargetKind.Entity
    public readonly QuantizedPos2 TargetPos;      // valid when TargetKind.Position
    public readonly AllianceTarget Alliance;       // valid when TargetKind.Alliance

    public SimCommand(
        uint applyTick,
        byte playerId,
        uint commandSeq,
        CommandType type,
        bool queued,
        uint[] selectedEntityIds,
        TargetKind targetKind,
        uint targetEntityId,
        QuantizedPos2 targetPos,
        AllianceTarget alliance)
    {
        ApplyTick = applyTick;
        PlayerId = playerId;
        CommandSeq = commandSeq;
        Type = type;
        Queued = queued;
        SelectedEntityIds = selectedEntityIds;
        TargetKind = targetKind;
        TargetEntityId = targetEntityId;
        TargetPos = targetPos;
        Alliance = alliance;
    }
}

public readonly struct CommandBatch
{
    public readonly string MatchId;
    public readonly uint ClientInputSeq;
    public readonly int AckBundleTick;
    public readonly SimCommand[] Commands;

    public CommandBatch(string matchId, uint clientInputSeq, int ackBundleTick, SimCommand[] commands)
    {
        MatchId = matchId;
        ClientInputSeq = clientInputSeq;
        AckBundleTick = ackBundleTick;
        Commands = commands;
    }
}
```

## 3) Required Command Fields and Validation

Every `SimCommand` must include:
- `ApplyTick`
- `PlayerId`
- `CommandSeq`
- `SelectedEntityIds` (sorted ascending, unique; can be empty only for global commands like `SetAlliance`)
- one target mode (`TargetKind`) with matching target payload
- `Queued` flag

Validation rules:
1. `ApplyTick >= current_server_tick + input_delay_ticks`
2. `CommandSeq` strictly increases per `PlayerId`
3. `SelectedEntityIds` sorted ascending and unique
4. Target validity by type:
   - `Move`, `AttackMove`, `Patrol`, `SetRallyPoint`: `TargetKind.Position`
   - `Attack`, `Gather`: usually `TargetKind.Entity`
   - `Build`: `TargetKind.Position`
   - `Train`, `Research`, `Stop`, `Hold`: `TargetKind.None` or context-specific
   - `SetAlliance`: `TargetKind.Alliance` and diplomacy enabled
5. If diplomacy is disabled, reject `SetAlliance`

## 4) Quantization Rules

## 4.1 Fixed-Point Grid
- Unit: `milli-tile` (`mt`)
- `1 tile = 1000 mt`
- Serialized positions use signed `int32` (`XMt`, `YMt`)

## 4.2 Raycast Hit -> Quantized Position
Input:
- world hit on nav plane `(worldX, worldY)` in tile-space units

Deterministic snap:
1. `tileX = floor(worldX)`
2. `tileY = floor(worldY)`
3. For center-snapped commands (`Move`, `AttackMove`, `Patrol`, `SetRallyPoint`):
   - `XMt = tileX * 1000 + 500`
   - `YMt = tileY * 1000 + 500`
4. For footprint-anchored `Build`:
   - snap to deterministic anchor cell based on building footprint rule (top-left or center-anchor, fixed per building type)

Do not serialize raw float coordinates.

## 5) Deterministic Expansion Rules (Multi-Unit Commands)

Given a command over N selected entities:
1. Filter to controllable entities for `PlayerId`.
2. Deduplicate entity IDs.
3. Sort entity IDs ascending.
4. Apply capability split if needed (for mixed selections) in stable class order.
5. Emit one or more `SimCommand` payloads with sorted `SelectedEntityIds`.

Stable capability split order (recommended):
1. `Gather`
2. `Attack` / `AttackMove`
3. `Move`

Per-player command ordering in a tick:
1. `PlayerId` asc
2. `CommandSeq` asc

## 6) Tie-Break Rules (Determinism Keys Only)

## 6.1 Entity Ordering
- Always use `entity_id` ascending for selection lists and expanded operations.

## 6.2 Target Ties (Multiple Valid Targets)
When multiple targets are equally valid:
1. tactical score key (integer) best-first
2. `target_entity_id` asc
3. `attacker_entity_id` asc

## 6.3 Cell Conflicts (Movement)
If multiple units request same destination cell:
1. intent priority band (if used)
2. `(entity_id + tick) mod 8192`
3. `entity_id` asc
Winner reserves cell; losers try next candidate or stay.

## 6.4 Diplomacy Apply Ordering
For `SetAlliance` converted to diplomacy events:
1. `apply_tick` asc
2. `event_id` asc

## 7) JSON Serialization Schema (Command Payload)

```json
{
  "$id": "urn:cossacks-back-to-war:command-model:v1",
  "type": "object",
  "required": ["match_id", "client_input_seq", "ack_bundle_tick", "commands"],
  "properties": {
    "match_id": { "type": "string", "minLength": 1, "maxLength": 64 },
    "client_input_seq": { "type": "integer", "minimum": 0, "maximum": 4294967295 },
    "ack_bundle_tick": { "type": "integer", "minimum": -1, "maximum": 2147483647 },
    "commands": {
      "type": "array",
      "minItems": 1,
      "maxItems": 256,
      "items": {
        "type": "object",
        "required": [
          "apply_tick",
          "player_id",
          "command_seq",
          "type",
          "queued",
          "selected_entity_ids",
          "target_kind"
        ],
        "properties": {
          "apply_tick": { "type": "integer", "minimum": 0, "maximum": 2147483647 },
          "player_id": { "type": "integer", "minimum": 0, "maximum": 7 },
          "command_seq": { "type": "integer", "minimum": 0, "maximum": 4294967295 },
          "type": {
            "type": "string",
            "enum": [
              "Move",
              "Attack",
              "AttackMove",
              "Stop",
              "Hold",
              "Patrol",
              "Gather",
              "Build",
              "Train",
              "Research",
              "SetRallyPoint",
              "SetAlliance"
            ]
          },
          "queued": { "type": "boolean" },
          "selected_entity_ids": {
            "type": "array",
            "items": { "type": "integer", "minimum": 1, "maximum": 4294967295 },
            "uniqueItems": true
          },
          "target_kind": { "type": "string", "enum": ["None", "Entity", "Position", "Alliance"] },
          "target_entity_id": { "type": "integer", "minimum": 1, "maximum": 4294967295 },
          "target_pos": {
            "type": "object",
            "required": ["x_mt", "y_mt"],
            "properties": {
              "x_mt": { "type": "integer", "minimum": -2147483648, "maximum": 2147483647 },
              "y_mt": { "type": "integer", "minimum": -2147483648, "maximum": 2147483647 }
            },
            "additionalProperties": false
          },
          "alliance_target": {
            "type": "object",
            "required": ["other_player_id", "relation", "shared_flags"],
            "properties": {
              "other_player_id": { "type": "integer", "minimum": 0, "maximum": 7 },
              "relation": { "type": "string", "enum": ["Enemy", "Ally", "Neutral"] },
              "shared_flags": { "type": "integer", "minimum": 0, "maximum": 7 }
            },
            "additionalProperties": false
          }
        },
        "additionalProperties": false
      }
    }
  },
  "additionalProperties": false
}
```

## 8) Example: Box Select + Right-Click Move + Shift Waypoint

Assume:
- `input_delay_ticks = 3`
- local current sim tick = `1200`
- `apply_tick = 1203`
- box-select result (unsorted) = `[25, 7, 44, 19]`
- deterministic sorted selection = `[7, 19, 25, 44]`

Actions:
1. Right-click ground at world `(48.73, 19.21)` -> snapped center `(48500, 19500)` mt
2. Shift-right-click waypoint at world `(60.10, 22.84)` -> snapped center `(60500, 22500)` mt

Produced payload:

```json
{
  "match_id": "m_001",
  "client_input_seq": 88,
  "ack_bundle_tick": 1198,
  "commands": [
    {
      "apply_tick": 1203,
      "player_id": 0,
      "command_seq": 9101,
      "type": "Move",
      "queued": false,
      "selected_entity_ids": [7, 19, 25, 44],
      "target_kind": "Position",
      "target_pos": { "x_mt": 48500, "y_mt": 19500 }
    },
    {
      "apply_tick": 1203,
      "player_id": 0,
      "command_seq": 9102,
      "type": "Move",
      "queued": true,
      "selected_entity_ids": [7, 19, 25, 44],
      "target_kind": "Position",
      "target_pos": { "x_mt": 60500, "y_mt": 22500 }
    }
  ]
}
```
