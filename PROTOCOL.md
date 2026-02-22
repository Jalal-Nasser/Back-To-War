# Deterministic Lockstep Protocol (Up to 8 Players)

## Scope
This protocol defines deterministic lockstep networking for an RTS with:
- 2..8 active players
- optional spectators
- reconnect and optional state resync

Canonical config constraints come from `lobby-match.schema.json`:
- `game_settings.tick_rate`
- `game_settings.input_delay_ticks`
- `rules.allow_in_game_diplomacy`
- `rules.default_shared_vision`
- `rules.default_shared_resources`
- `alliance_config` (`matrix` or `bitmasks`)

Message payload schemas are defined in `protocol.messages.json`.

## Deterministic Model

### Tick and Delay
Let:
- `S` = current authoritative server tick
- `D` = `match_config.game_settings.input_delay_ticks`
- `T` = target simulation tick for commands

Rules:
1. Client commands are valid only if `T >= S + D`.
2. Bundle close tick for target `T` is `C = T - D`.
3. Server finalizes `SERVER_TICK_BUNDLE(T)` at tick `C`.
4. Late command rule: if `T < S + D`, reject as stale.
5. Server never uses wall-clock deadlines; only tick comparisons.

### Start Tick
When host starts a match, server computes:
- `start_tick = server_tick + D + startup_buffer_ticks`
- `startup_buffer_ticks` is a deterministic constant (recommended `2`).

### Canonical Ordering Guarantees
1. Tick order: simulation is applied strictly by ascending tick.
2. Stage order: diplomacy -> gameplay -> outcome transitions -> victory (per `STATE_MACHINE.md`).
3. Diplomacy event order: `(apply_tick, event_id)` ascending.
4. `SERVER_TICK_BUNDLE` order: `bundle_tick` contiguous increasing by `+1`.
5. Per-bundle player order: `player_id` ascending.
6. Per-player commands: `command_id` ascending.
7. Multi-unit command expansion: `entity_id` ascending.
8. Combat tie-break for multiple valid targets: `target_entity_id`, then `attacker_entity_id`.

## Transport and Reliability Classes
Reliability here is protocol-level, independent of transport implementation.

- `reliable`: must be delivered exactly once logically (dedupe allowed), in-order per stream.
- `unreliable`: may be dropped/reordered; sender must repeat by tick-based strategy.

Recommended channels:
- Reliable ordered channel: lobby/control/start/resync.
- Unreliable datagram channel: `CLIENT_COMMANDS`, `DESYNC_HASH`.

## Roles and Participation
- Active player: owns a seat in `match_config.players`; may send `CLIENT_COMMANDS`.
- Spectator: read-only stream consumer; may send `DESYNC_HASH` and `RESYNC_REQUEST`.
- Reconnecting player: resumes same `player_id`/seat via `resume_token`.

## Message Catalog

| Message | Direction | Reliability |
|---|---|---|
| `HELLO` | client->server and server->client | reliable |
| `JOIN_LOBBY` | client->server | reliable |
| `LOBBY_STATE` | server->client | reliable |
| `SET_ALLIANCE` | client->server | reliable |
| `READY` | client->server | reliable |
| `START_MATCH` | client->server and server->client | reliable |
| `CLIENT_COMMANDS` | client->server | unreliable (redundant resend) |
| `SERVER_TICK_BUNDLE` | server->client | reliable (ack + resend) |
| `DESYNC_HASH` | client->server | unreliable (periodic) |
| `RESYNC_REQUEST` | client->server | reliable |
| `RESYNC_SNAPSHOT` | server->client | reliable |

## Message Specs

### `HELLO`
Direction: bidirectional
Reliability: reliable

Payload (summary):
- `phase`: `request | response`
- `protocol_version`: `1`
- `connection_nonce`: hex string
- request fields: `requested_role`, optional `resume_token`, `last_applied_tick`
- response fields: `accepted`, optional `reject_code`, `assigned_role`, `session_id`, `server_tick`, optional `player_id`

Validation:
- Protocol version must match exactly.
- `resume_token` (if provided) must map to reconnect-eligible session.
- On reject, include stable `reject_code`.

### `JOIN_LOBBY`
Direction: client->server
Reliability: reliable

Payload (summary):
- `session_id`
- `lobby_id`
- `requested_role`: `player | spectator`
- `display_name`
- optional `requested_slot_index`
- optional `resume_player_id`

Validation:
- Lobby exists and is open.
- Slot constraints from `LobbyConfig`.
- Spectators cannot claim a player slot.

### `LOBBY_STATE`
Direction: server->client
Reliability: reliable

Payload (summary):
- `lobby_id`
- `revision` (monotonic)
- `lobby_config` (must validate against `lobby-match.schema.json#/$defs/LobbyConfig`)
- `ready_player_ids`
- `spectator_count`

Validation:
- `revision` strictly increases.
- `lobby_config` is schema-valid and server-canonical.

### `SET_ALLIANCE`
Direction: client->server
Reliability: reliable

Payload (summary):
- `scope`: `lobby | match`
- `request_id` (client monotonic)
- `issuer_player_id`
- `request_tick`
- target ids: `a_player_id`, `b_player_id`
- `new_state`: `ALLY | ENEMY | NEUTRAL`
- optional flags: `shared_vision`, `shared_resources`, `shared_unit_control`
- `lobby_id` or `match_id` (based on scope)

Validation:
- `a_player_id != b_player_id`, both in range and present.
- Lobby scope: issuer must be authorized by host controls.
- Match scope: `allow_in_game_diplomacy == true`.
- If `new_state != ALLY`, effective shared flags are forced to `false`.
- Match scope accepted requests are converted by server to deterministic `DiplomacyChangeEvent` (`event_id`, `apply_tick`) and emitted in the appropriate `SERVER_TICK_BUNDLE`.

### `READY`
Direction: client->server
Reliability: reliable

Payload (summary):
- `lobby_id`
- `player_id`
- `ready` (bool)
- `revision_seen`

Validation:
- Sender controls `player_id` seat.
- `revision_seen` must not be older than server retention window.

### `START_MATCH`
Direction: bidirectional
Reliability: reliable

Payload (summary):
- `phase`: `request | announce`
- request: `lobby_id`, `requester_player_id`, `lobby_revision_seen`
- announce: `lobby_id`, `match_id`, `start_tick`, `match_config`, `active_player_ids`, `spectator_count`

Validation:
- Request only from authorized starter.
- All required ready/slot constraints must pass.
- Announced `match_config` validates against `lobby-match.schema.json#/$defs/MatchConfig`.

### `CLIENT_COMMANDS`
Direction: client->server
Reliability: unreliable (repeated until acked)

Payload (summary):
- `match_id`
- `sender_player_id`
- `client_input_seq` (monotonic)
- `ack_bundle_tick` (highest contiguous applied server bundle)
- `bundles[]`:
  - `target_tick`
  - `commands[]` (`SimCommand`)

Validation:
- Sender must be active player (not spectator).
- For each bundle, `target_tick >= S + D`.
- Reject duplicates by `(sender_player_id, target_tick, command_id)`.
- `entity_ids` must be unique and sorted ascending.
- `SET_DIPLOMACY` command allowed only when `allow_in_game_diplomacy == true`.

### `SERVER_TICK_BUNDLE`
Direction: server->client
Reliability: reliable (ack + resend)

Payload (summary):
- `match_id`
- `bundle_tick`
- `server_bundle_seq` (monotonic)
- `expected_prev_bundle_tick`
- `player_inputs[]`:
  - `player_id`
  - `source`: `PLAYER | AI | NOOP`
  - `accepted_client_input_seq`
  - `commands[]`
- `system_events[]` (includes `DIPLOMACY_CHANGE` with `event_id`, `apply_tick`)
- `last_accepted_client_input_seq_by_player[]`
- `bundle_hash`

Validation:
- `bundle_tick` contiguous for each match stream.
- Exactly one `player_inputs` entry per active player, sorted by `player_id`.
- Per-player commands sorted by `command_id`.
- `system_events` sorted by deterministic keys (`event_id` for same tick).

### `DESYNC_HASH`
Direction: client->server
Reliability: unreliable (periodic)

Payload (summary):
- `match_id`
- `sender_role`
- optional `sender_player_id`
- `tick`
- `sim_hash`
- `last_applied_bundle_tick`

Validation:
- Hash format and tick bounds must be valid.
- If role is `player`, `sender_player_id` is required.

### `RESYNC_REQUEST`
Direction: client->server
Reliability: reliable

Payload (summary):
- `match_id`
- `sender_role`
- optional `sender_player_id`
- `reason`: `HASH_MISMATCH | BUNDLE_GAP | RECONNECT | MANUAL`
- `last_applied_tick`
- optional `requested_snapshot_tick`

Validation:
- Requestor must belong to match (player or spectator session).
- If role is `player`, `sender_player_id` is required.

### `RESYNC_SNAPSHOT` (optional)
Direction: server->client
Reliability: reliable

Payload (summary):
- `match_id`
- `snapshot_id`
- `snapshot_tick`
- `chunk_index`, `chunk_count`, `is_last_chunk`
- `encoding`
- `data_base64`
- `state_hash`
- `post_snapshot_bundle_start_tick`

Validation:
- Chunks must assemble deterministically by `(snapshot_id, chunk_index)`.
- `chunk_count` and hash must match declared metadata.

## Input Buffering and Resend Strategy

### Client -> Server (`CLIENT_COMMANDS`)
- Client sends commands for future ticks (`>= S + D`).
- Each send includes unacked command bundles from the last `client_resend_window_ticks` (recommended `8`).
- Deduplication key on server: `(sender_player_id, target_tick, command_id)`.

### Server -> Client (`SERVER_TICK_BUNDLE`)
- Client acks highest contiguous applied bundle via `ack_bundle_tick` in `CLIENT_COMMANDS`.
- Server tracks per-connection acked bundle tick.
- Server resends unacked bundles every `server_resend_interval_ticks` (recommended `2`).
- All resend timing uses ticks, never wall-clock timers.

## Reconnect and Spectator Flow
1. Client sends `HELLO(request)` with optional `resume_token` and `last_applied_tick`.
2. Server replies `HELLO(response)` with accept/reject.
3. Reconnecting client sends `RESYNC_REQUEST(reason=RECONNECT)`.
4. Server sends `RESYNC_SNAPSHOT` chunks (optional) then continues `SERVER_TICK_BUNDLE` stream.
5. Client resumes simulation at `post_snapshot_bundle_start_tick`.

## Alliance and Diplomacy Rules in Protocol
- Lobby alliance setup uses `SET_ALLIANCE(scope=lobby)` and is reflected in `LOBBY_STATE`.
- Match alliance model is taken from `match_config.alliance_config` (matrix or bitmasks).
- If shared flags are omitted for an ally pair, server may derive defaults from:
  - `default_shared_vision`
  - `default_shared_resources`
- In-match alliance changes require `allow_in_game_diplomacy == true`.
- Accepted in-match changes are server-materialized as deterministic diplomacy events with assigned `event_id` and `apply_tick`.

## Server Loop Pseudocode
```text
constants:
  D = match_config.game_settings.input_delay_ticks
  RESEND_INTERVAL_TICKS = 2
  CLIENT_HORIZON_TICKS = 64

state:
  server_tick
  next_event_id
  command_buffer[player_id][target_tick] -> map(command_id -> SimCommand)
  diplomacy_queue[apply_tick] -> list(DiplomacyChangeEvent)
  bundles[bundle_tick] -> ServerTickBundle
  conn_ack_bundle_tick[connection_id] -> int (init -1)

for each server tick S:
  inbox = pop_inbound_messages_for_tick(S)
  sort inbox by (connection_id, ingress_seq)

  for m in inbox:
    validate against protocol.messages.json
    route m.type:
      HELLO/JOIN_LOBBY/READY/START_MATCH/SET_ALLIANCE(lobby): apply lobby state changes

      SET_ALLIANCE(match):
        validate allow_in_game_diplomacy and permissions
        event_id = next_event_id; next_event_id += 1
        apply_tick = S + max(input_delay_ticks, diplomacy_delay_ticks)
        enqueue diplomacy_queue[apply_tick] with deterministic fields

      CLIENT_COMMANDS:
        update conn_ack_bundle_tick from m.ack_bundle_tick
        validate sender_player_id is active player
        for each bundle in m.bundles:
          if bundle.target_tick < S + D: reject stale
          if bundle.target_tick > S + D + CLIENT_HORIZON_TICKS: reject too-far-future
          for each cmd in bundle.commands:
            dedupe key = (sender_player_id, bundle.target_tick, cmd.command_id)
            if new key: store in command_buffer

      DESYNC_HASH:
        record hash by (sender, tick)
        if mismatch policy triggers, mark connection for resync

      RESYNC_REQUEST:
        queue snapshot stream for that connection

  T = S + D
  build authoritative bundle for tick T:
    player_inputs = []
    for player_id in active_player_ids ascending:
      cmds = command_buffer[player_id][T] sorted by command_id
      source = resolve_source(player_id)  // PLAYER, AI, or NOOP by deterministic match policy
      player_inputs.push({player_id, source, accepted_client_input_seq, cmds})

    system_events = diplomacy_queue[T] sorted by event_id
    bundle = make_bundle(T, player_inputs, system_events)
    bundle.bundle_hash = hash(bundle)
    bundles[T] = bundle
    send bundle to all participants/spectators

  if S >= match_start_tick:
    execute Stage 1..4 using bundles[S]

  for each connection:
    resend any bundles U where U > conn_ack_bundle_tick[connection]
      if (S - last_send_tick[connection][U]) >= RESEND_INTERVAL_TICKS

  persist deterministic checkpoint
```

## Client Loop Pseudocode
```text
constants:
  D = match_config.game_settings.input_delay_ticks
  HASH_INTERVAL_TICKS = 32
  CLIENT_RESEND_WINDOW_TICKS = 8

state:
  next_sim_tick
  highest_contiguous_bundle_tick_applied = -1
  received_bundles[bundle_tick]
  outbound_command_cache[target_tick] -> commands
  local_client_input_seq

on each client update step:
  inbox = recv_messages()
  sort inbox by (server_bundle_seq when present, arrival_seq)

  for m in inbox:
    validate against protocol.messages.json
    switch m.type:
      LOBBY_STATE / START_MATCH(announce): update local session state
      SERVER_TICK_BUNDLE:
        store by bundle_tick if not duplicate
      RESYNC_SNAPSHOT:
        assemble chunks by (snapshot_id, chunk_index)
        when complete and hash valid: install snapshot at snapshot_tick
      HELLO(response): accept/reject connection

  while received_bundles contains next_sim_tick:
    b = received_bundles[next_sim_tick]
    apply deterministic simulation for tick next_sim_tick using b
    highest_contiguous_bundle_tick_applied = next_sim_tick

    if next_sim_tick % HASH_INTERVAL_TICKS == 0:
      send DESYNC_HASH(match_id, next_sim_tick, sim_hash, highest_contiguous_bundle_tick_applied)

    next_sim_tick += 1

  target_tick = next_sim_tick + D
  new_commands = collect_local_player_commands_for_tick(target_tick)
  canonicalize new_commands (sort entity_ids, stable command_id assignment)
  cache outbound_command_cache[target_tick] = new_commands

  payload_bundles = choose bundles from [target_tick-CLIENT_RESEND_WINDOW_TICKS .. target_tick]
    where bundle tick >= next_sim_tick + D and not expired

  send CLIENT_COMMANDS(
    client_input_seq = local_client_input_seq,
    ack_bundle_tick = highest_contiguous_bundle_tick_applied,
    bundles = payload_bundles
  )
  local_client_input_seq += 1

  if bundle gap policy is triggered by tick gap (not time):
    send RESYNC_REQUEST(reason=BUNDLE_GAP, last_applied_tick=highest_contiguous_bundle_tick_applied)
```

## Determinism Requirements Checklist
- Use integer tick math only.
- Never branch on wall-clock durations.
- Validate every message against `protocol.messages.json` before applying.
- Apply cross-field invariants from `lobby-match.schema.json` and server rules.
- Keep canonical sort orders for players, commands, events, and bundles.
