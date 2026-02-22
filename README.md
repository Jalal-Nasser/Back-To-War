# Back To War

**Back To War** is a multiplayer Real-Time Strategy (RTS) game built in Unity (C#). The project is engineered around a robust **server-authoritative deterministic lockstep** networking model, supporting up to 8 players.

It features classic RTS mechanics combined with dynamic, scalable diplomacy and objective-based victory conditions.

---

## 🏗️ Architecture & Core Systems

### 1. Deterministic Lockstep Simulation
- **Server-Authoritative:** All commands are processed on explicit simulation ticks. The server builds and broadcasts authoritative tick bundles.
- **Fixed-Point Math:** The game uses quantized representations (e.g., milli-tiles for world positions) and integer math instead of floating-point to guarantee deterministic simulations across all clients.
- **Input Delay:** Commands are stamped for future ticks (`local_sim_tick + input_delay_ticks`) to hide network latency smoothly.
- **Predictable Outcomes:** Strict tie-breaking rules and canonical sorting for commands, entities, and actions ensure sync.

*Read more in [PROTOCOL.md](Docs/PROTOCOL.md) and [DETERMINISM_RULES.md](Docs/DETERMINISM_RULES.md)*

### 2. Command Model
- Completely decoupled from Unity-specific types (uses serialized C# structs).
- Supports standard RTS commands (`Move`, `Attack`, `Gather`, `Build`, `Train`, `Patrol`, etc.)
- Supports multi-unit selection constraints and Shift-queueing actions out of the box.

*Read more in [COMMAND_MODEL.md](Docs/COMMAND_MODEL.md)*

### 3. Advanced Diplomacy & Victory
- Supports dynamic formats like 2v2, 1v6, or Free-For-All (FFA).
- Optional **mid-game diplomacy**, allowing players to change alliances seamlessly.
- **Relationship Flags:** Alliances can finely tune shared vision, shared resource pools, and shared unit control.
- Multiple deterministic Victory engines: Last Surviving Player, Last Surviving Alliance, and custom Objective-based wins.
- Graceful handling of disconnects with AI takeover, automated surrender, or state freezing.

*Read more in [ARCHITECTURE.md](Docs/ARCHITECTURE.md)*

### 4. Input & Controls
- **Mouse-First UX:** Implements familiar RTS control schemes (left-click select, drag box select, right-click context commands).
- **Context-Aware Commands:** Right-clicking automatically resolves context (e.g., Attack an enemy, Gather a resource node, Garrison a structure).
- Built-in support for control groups (`Ctrl + 1-9`) and camera panning mechanics.

*Read more in [INPUT_AND_CONTROLS.md](Docs/INPUT_AND_CONTROLS.md)*

---

## 🛠️ Project Structure
- `Assets/` / `Packages/` / `ProjectSettings/`: Standard Unity project directories.
- `Docs/`: Contains all technical specifications and architecture design documents defining the lockstep simulation, input rules, and game state.
  - `PROTOCOL.md`
  - `ARCHITECTURE.md`
  - `COMMAND_MODEL.md`
  - `INPUT_AND_CONTROLS.md`
  - `STATE_MACHINE.md`
  - `DETERMINISM_RULES.md`
  - `PATHFINDING_AND_MOVEMENT.md`
  - `PERFORMANCE_BUDGET.md`
- `protocol.messages.json` & `lobby-match.schema.json`: Strict JSON schemas defining network message payloads and match configurations.

## 🚀 Getting Started
This project requires **Unity** to open and run. Ensure your Unity version matches the project settings.

1. Clone the repository.
2. Open the project in Unity via the Unity Hub.
3. Allow packages to resolve.
4. Check the `Docs/` folder for any specific implementation guidelines regarding custom components and the lockstep manager.
