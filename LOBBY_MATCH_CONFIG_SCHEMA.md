# Lobby and Match Configuration Schema (Up to 8 Players)

## 1) JSON Schema (Draft 2020-12)
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "urn:cossacks-back-to-war:lobby-match-config:v1",
  "title": "RTS Lobby/Match Configuration",
  "oneOf": [
    { "$ref": "#/$defs/LobbyConfig" },
    { "$ref": "#/$defs/MatchConfig" }
  ],
  "$defs": {
    "Player": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "player_id",
        "display_name",
        "slot_index",
        "color",
        "faction_id",
        "starting_position"
      ],
      "properties": {
        "player_id": { "type": "integer", "minimum": 0, "maximum": 7 },
        "display_name": { "type": "string", "minLength": 1, "maxLength": 32 },
        "slot_index": { "type": "integer", "minimum": 0, "maximum": 7 },
        "color": { "type": "string", "pattern": "^#[0-9A-Fa-f]{6}$" },
        "faction_id": { "type": "string", "pattern": "^FACTION_[A-Z0-9_]+$" },
        "starting_position": { "type": "integer", "minimum": 0, "maximum": 7 },
        "is_host": { "type": "boolean", "default": false }
      }
    },
    "Slot": {
      "type": "object",
      "additionalProperties": false,
      "required": ["slot_index", "state"],
      "properties": {
        "slot_index": { "type": "integer", "minimum": 0, "maximum": 7 },
        "state": { "type": "string", "enum": ["open", "human", "bot", "closed"] },
        "occupied_by_player_id": { "type": "integer", "minimum": 0, "maximum": 7 },
        "bot_profile": { "type": "string", "enum": ["easy", "normal", "hard", "insane"] }
      },
      "allOf": [
        {
          "if": { "properties": { "state": { "enum": ["human", "bot"] } } },
          "then": { "required": ["occupied_by_player_id"] }
        },
        {
          "if": { "properties": { "state": { "enum": ["open", "closed"] } } },
          "then": { "not": { "required": ["occupied_by_player_id"] } }
        }
      ]
    },
    "HostControls": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "host_player_id",
        "can_edit_slots",
        "can_edit_alliances",
        "can_edit_rules",
        "can_edit_settings",
        "can_start_match",
        "lock_teams",
        "lock_colors",
        "lock_factions",
        "lock_starting_positions"
      ],
      "properties": {
        "host_player_id": { "type": "integer", "minimum": 0, "maximum": 7 },
        "can_edit_slots": { "type": "boolean" },
        "can_edit_alliances": { "type": "boolean" },
        "can_edit_rules": { "type": "boolean" },
        "can_edit_settings": { "type": "boolean" },
        "can_start_match": { "type": "boolean" },
        "lock_teams": { "type": "boolean" },
        "lock_colors": { "type": "boolean" },
        "lock_factions": { "type": "boolean" },
        "lock_starting_positions": { "type": "boolean" }
      }
    },
    "Rules": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "allow_in_game_diplomacy",
        "default_shared_vision",
        "default_shared_resources"
      ],
      "properties": {
        "allow_in_game_diplomacy": { "type": "boolean" },
        "default_shared_vision": { "type": "boolean" },
        "default_shared_resources": { "type": "boolean" },
        "default_shared_unit_control": { "type": "boolean", "default": false }
      }
    },
    "GameSettings": {
      "type": "object",
      "additionalProperties": false,
      "required": ["map_id", "seed", "tick_rate", "input_delay_ticks"],
      "properties": {
        "map_id": { "type": "string", "minLength": 1, "maxLength": 128 },
        "seed": { "type": "integer", "minimum": 0, "maximum": 4294967295 },
        "tick_rate": { "type": "integer", "minimum": 10, "maximum": 128 },
        "input_delay_ticks": { "type": "integer", "minimum": 0, "maximum": 30 }
      }
    },
    "RelationValue": {
      "type": "string",
      "enum": ["ALLY", "ENEMY", "NEUTRAL"]
    },
    "RelationMatrix": {
      "type": "array",
      "minItems": 1,
      "maxItems": 8,
      "items": {
        "type": "array",
        "minItems": 1,
        "maxItems": 8,
        "items": { "$ref": "#/$defs/RelationValue" }
      }
    },
    "BoolMatrix": {
      "type": "array",
      "minItems": 1,
      "maxItems": 8,
      "items": {
        "type": "array",
        "minItems": 1,
        "maxItems": 8,
        "items": { "type": "boolean" }
      }
    },
    "MaskArray": {
      "type": "array",
      "minItems": 1,
      "maxItems": 8,
      "items": { "type": "integer", "minimum": 0, "maximum": 255 }
    },
    "AllianceConfigMatrix": {
      "type": "object",
      "additionalProperties": false,
      "required": ["encoding", "relation_matrix"],
      "properties": {
        "encoding": { "const": "matrix" },
        "relation_matrix": { "$ref": "#/$defs/RelationMatrix" },
        "shared_vision_matrix": { "$ref": "#/$defs/BoolMatrix" },
        "shared_resources_matrix": { "$ref": "#/$defs/BoolMatrix" },
        "shared_unit_control_matrix": { "$ref": "#/$defs/BoolMatrix" }
      }
    },
    "AllianceConfigBitmask": {
      "type": "object",
      "additionalProperties": false,
      "required": ["encoding", "ally_masks"],
      "properties": {
        "encoding": { "const": "bitmasks" },
        "ally_masks": { "$ref": "#/$defs/MaskArray" },
        "neutral_masks": { "$ref": "#/$defs/MaskArray" },
        "shared_vision_masks": { "$ref": "#/$defs/MaskArray" },
        "shared_resources_masks": { "$ref": "#/$defs/MaskArray" },
        "shared_unit_control_masks": { "$ref": "#/$defs/MaskArray" }
      }
    },
    "AllianceConfig": {
      "oneOf": [
        { "$ref": "#/$defs/AllianceConfigMatrix" },
        { "$ref": "#/$defs/AllianceConfigBitmask" }
      ]
    },
    "LobbyConfig": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "config_type",
        "lobby_id",
        "max_players",
        "slots",
        "players",
        "host_controls",
        "alliance_config",
        "rules",
        "game_settings"
      ],
      "properties": {
        "config_type": { "const": "lobby" },
        "lobby_id": { "type": "string", "minLength": 1, "maxLength": 64 },
        "max_players": { "type": "integer", "minimum": 2, "maximum": 8 },
        "slots": {
          "type": "array",
          "minItems": 2,
          "maxItems": 8,
          "items": { "$ref": "#/$defs/Slot" }
        },
        "players": {
          "type": "array",
          "minItems": 1,
          "maxItems": 8,
          "items": { "$ref": "#/$defs/Player" }
        },
        "host_controls": { "$ref": "#/$defs/HostControls" },
        "alliance_config": { "$ref": "#/$defs/AllianceConfig" },
        "rules": { "$ref": "#/$defs/Rules" },
        "game_settings": { "$ref": "#/$defs/GameSettings" }
      }
    },
    "MatchConfig": {
      "type": "object",
      "additionalProperties": false,
      "required": [
        "config_type",
        "match_id",
        "source_lobby_id",
        "start_tick",
        "max_players",
        "players",
        "alliance_config",
        "rules",
        "game_settings"
      ],
      "properties": {
        "config_type": { "const": "match" },
        "match_id": { "type": "string", "minLength": 1, "maxLength": 64 },
        "source_lobby_id": { "type": "string", "minLength": 1, "maxLength": 64 },
        "start_tick": { "type": "integer", "minimum": 0 },
        "max_players": { "type": "integer", "minimum": 2, "maximum": 8 },
        "players": {
          "type": "array",
          "minItems": 2,
          "maxItems": 8,
          "items": { "$ref": "#/$defs/Player" }
        },
        "alliance_config": { "$ref": "#/$defs/AllianceConfig" },
        "rules": { "$ref": "#/$defs/Rules" },
        "game_settings": { "$ref": "#/$defs/GameSettings" }
      }
    }
  }
}
```

## 2) Validation Rules (Server-Side)
JSON schema handles shape and basic bounds. Enforce these cross-field rules on the server:

1. `max_players <= 8`, `players.length <= max_players`, and `slots.length == max_players` for lobby configs.
2. `slot_index` values are unique and contiguous (`0..max_players-1`).
3. `player_id` values are unique and stable for the match lifetime.
4. Every player references a valid slot, and that slot has `state` in `human|bot` with matching `occupied_by_player_id`.
5. Exactly one host player exists:
   - one `players[i].is_host == true`
   - `host_controls.host_player_id` equals that `player_id`
6. `color` must be unique among players.
7. `starting_position` must be unique among players and valid for `map_id`.
8. `map_id` must exist and support `max_players` spawn points.
9. Alliance dimension `N` must equal `players.length`.
10. For matrix encoding:
    - `relation_matrix` is exactly `N x N`
    - `relation_matrix[i][i] == ALLY`
    - symmetry: `relation_matrix[i][j] == relation_matrix[j][i]`
    - any shared matrix present is exactly `N x N`, symmetric, and has `false` on diagonal
    - shared flags are only allowed where relation is `ALLY`
11. For bitmask encoding:
    - each mask array length is exactly `N`
    - bits above `N-1` must be zero
    - self bit is set in `ally_masks[i]`
    - symmetry: `i` allied with `j` iff `j` allied with `i`
    - shared flag masks only set bits for ally relations
12. If explicit shared matrices/masks are absent, derive shared flags from:
    - `rules.default_shared_vision`
    - `rules.default_shared_resources`
    - `rules.default_shared_unit_control` (if used)
13. If `rules.allow_in_game_diplomacy == false`, reject diplomacy change requests at runtime.
14. On lobby->match transition, freeze players/alliances/rules/settings into immutable match config.

## 3) Example Configs

### A) 2v2 (Matrix Encoding)
```json
{
  "config_type": "lobby",
  "lobby_id": "lobby_2v2_001",
  "max_players": 4,
  "slots": [
    { "slot_index": 0, "state": "human", "occupied_by_player_id": 0 },
    { "slot_index": 1, "state": "human", "occupied_by_player_id": 1 },
    { "slot_index": 2, "state": "human", "occupied_by_player_id": 2 },
    { "slot_index": 3, "state": "human", "occupied_by_player_id": 3 }
  ],
  "players": [
    { "player_id": 0, "display_name": "Alpha", "slot_index": 0, "color": "#E53935", "faction_id": "FACTION_A", "starting_position": 0, "is_host": true },
    { "player_id": 1, "display_name": "Bravo", "slot_index": 1, "color": "#1E88E5", "faction_id": "FACTION_B", "starting_position": 1 },
    { "player_id": 2, "display_name": "Charlie", "slot_index": 2, "color": "#43A047", "faction_id": "FACTION_C", "starting_position": 2 },
    { "player_id": 3, "display_name": "Delta", "slot_index": 3, "color": "#FDD835", "faction_id": "FACTION_D", "starting_position": 3 }
  ],
  "host_controls": {
    "host_player_id": 0,
    "can_edit_slots": true,
    "can_edit_alliances": true,
    "can_edit_rules": true,
    "can_edit_settings": true,
    "can_start_match": true,
    "lock_teams": false,
    "lock_colors": false,
    "lock_factions": false,
    "lock_starting_positions": false
  },
  "alliance_config": {
    "encoding": "matrix",
    "relation_matrix": [
      ["ALLY", "ALLY", "ENEMY", "ENEMY"],
      ["ALLY", "ALLY", "ENEMY", "ENEMY"],
      ["ENEMY", "ENEMY", "ALLY", "ALLY"],
      ["ENEMY", "ENEMY", "ALLY", "ALLY"]
    ]
  },
  "rules": {
    "allow_in_game_diplomacy": true,
    "default_shared_vision": true,
    "default_shared_resources": false,
    "default_shared_unit_control": false
  },
  "game_settings": {
    "map_id": "map_river_crossing_4p",
    "seed": 123456789,
    "tick_rate": 20,
    "input_delay_ticks": 3
  }
}
```

### B) 1v6 (Bitmask Encoding)
```json
{
  "config_type": "lobby",
  "lobby_id": "lobby_1v6_001",
  "max_players": 7,
  "slots": [
    { "slot_index": 0, "state": "human", "occupied_by_player_id": 0 },
    { "slot_index": 1, "state": "human", "occupied_by_player_id": 1 },
    { "slot_index": 2, "state": "human", "occupied_by_player_id": 2 },
    { "slot_index": 3, "state": "human", "occupied_by_player_id": 3 },
    { "slot_index": 4, "state": "human", "occupied_by_player_id": 4 },
    { "slot_index": 5, "state": "human", "occupied_by_player_id": 5 },
    { "slot_index": 6, "state": "human", "occupied_by_player_id": 6 }
  ],
  "players": [
    { "player_id": 0, "display_name": "Boss", "slot_index": 0, "color": "#D32F2F", "faction_id": "FACTION_A", "starting_position": 0, "is_host": true },
    { "player_id": 1, "display_name": "Team1", "slot_index": 1, "color": "#1976D2", "faction_id": "FACTION_B", "starting_position": 1 },
    { "player_id": 2, "display_name": "Team2", "slot_index": 2, "color": "#388E3C", "faction_id": "FACTION_C", "starting_position": 2 },
    { "player_id": 3, "display_name": "Team3", "slot_index": 3, "color": "#FBC02D", "faction_id": "FACTION_D", "starting_position": 3 },
    { "player_id": 4, "display_name": "Team4", "slot_index": 4, "color": "#7B1FA2", "faction_id": "FACTION_E", "starting_position": 4 },
    { "player_id": 5, "display_name": "Team5", "slot_index": 5, "color": "#00796B", "faction_id": "FACTION_F", "starting_position": 5 },
    { "player_id": 6, "display_name": "Team6", "slot_index": 6, "color": "#5D4037", "faction_id": "FACTION_G", "starting_position": 6 }
  ],
  "host_controls": {
    "host_player_id": 0,
    "can_edit_slots": true,
    "can_edit_alliances": true,
    "can_edit_rules": true,
    "can_edit_settings": true,
    "can_start_match": true,
    "lock_teams": false,
    "lock_colors": false,
    "lock_factions": false,
    "lock_starting_positions": false
  },
  "alliance_config": {
    "encoding": "bitmasks",
    "ally_masks": [1, 126, 126, 126, 126, 126, 126]
  },
  "rules": {
    "allow_in_game_diplomacy": true,
    "default_shared_vision": true,
    "default_shared_resources": true,
    "default_shared_unit_control": false
  },
  "game_settings": {
    "map_id": "map_siege_ring_7p",
    "seed": 987654321,
    "tick_rate": 20,
    "input_delay_ticks": 4
  }
}
```

### C) FFA (8 Players, No Allies, Bitmask Encoding)
```json
{
  "config_type": "lobby",
  "lobby_id": "lobby_ffa_001",
  "max_players": 8,
  "slots": [
    { "slot_index": 0, "state": "human", "occupied_by_player_id": 0 },
    { "slot_index": 1, "state": "human", "occupied_by_player_id": 1 },
    { "slot_index": 2, "state": "human", "occupied_by_player_id": 2 },
    { "slot_index": 3, "state": "human", "occupied_by_player_id": 3 },
    { "slot_index": 4, "state": "human", "occupied_by_player_id": 4 },
    { "slot_index": 5, "state": "human", "occupied_by_player_id": 5 },
    { "slot_index": 6, "state": "human", "occupied_by_player_id": 6 },
    { "slot_index": 7, "state": "human", "occupied_by_player_id": 7 }
  ],
  "players": [
    { "player_id": 0, "display_name": "P0", "slot_index": 0, "color": "#E53935", "faction_id": "FACTION_A", "starting_position": 0, "is_host": true },
    { "player_id": 1, "display_name": "P1", "slot_index": 1, "color": "#1E88E5", "faction_id": "FACTION_B", "starting_position": 1 },
    { "player_id": 2, "display_name": "P2", "slot_index": 2, "color": "#43A047", "faction_id": "FACTION_C", "starting_position": 2 },
    { "player_id": 3, "display_name": "P3", "slot_index": 3, "color": "#FDD835", "faction_id": "FACTION_D", "starting_position": 3 },
    { "player_id": 4, "display_name": "P4", "slot_index": 4, "color": "#8E24AA", "faction_id": "FACTION_E", "starting_position": 4 },
    { "player_id": 5, "display_name": "P5", "slot_index": 5, "color": "#00ACC1", "faction_id": "FACTION_F", "starting_position": 5 },
    { "player_id": 6, "display_name": "P6", "slot_index": 6, "color": "#6D4C41", "faction_id": "FACTION_G", "starting_position": 6 },
    { "player_id": 7, "display_name": "P7", "slot_index": 7, "color": "#C0CA33", "faction_id": "FACTION_H", "starting_position": 7 }
  ],
  "host_controls": {
    "host_player_id": 0,
    "can_edit_slots": true,
    "can_edit_alliances": true,
    "can_edit_rules": true,
    "can_edit_settings": true,
    "can_start_match": true,
    "lock_teams": false,
    "lock_colors": false,
    "lock_factions": false,
    "lock_starting_positions": false
  },
  "alliance_config": {
    "encoding": "bitmasks",
    "ally_masks": [1, 2, 4, 8, 16, 32, 64, 128]
  },
  "rules": {
    "allow_in_game_diplomacy": false,
    "default_shared_vision": false,
    "default_shared_resources": false,
    "default_shared_unit_control": false
  },
  "game_settings": {
    "map_id": "map_iron_crater_8p",
    "seed": 42424242,
    "tick_rate": 20,
    "input_delay_ticks": 3
  }
}
```
