# AGENT.md — AI Agent Context for CarpentryWorkshopVR

This file gives AI coding agents (e.g. OpenCode, Copilot, Cursor) the context they need to work effectively in this repository. Read this before writing or editing any code.

---

## Project Identity

**CarpentryWorkshopVR** is a Unity 6 VR simulation of a carpentry workshop. Players operate machines (CNC router, robotic arm, conveyor), use hand tools (hammer, drill, saw), and complete assembly tasks while responding to safety events.

| Property | Value |
|---|---|
| Engine | Unity 6 (6000.3.5f1) |
| Render Pipeline | URP 17.3.0 |
| Target Platform | VR (OpenXR / Meta Quest) |
| Input System | Unity New Input System (`com.unity.inputsystem` 1.17.0) |
| Language | C# |
| Color Space | Linear |
| Main Scene | `Assets/Scenes/CarpentryWorkshop.unity` |

---

## Architecture Overview

The project is in early development (Phase 1 of 3). The architecture follows a component-based Unity pattern. Scripts are organized by domain:

```
Assets/Scripts/
├── Player/           # VR player movement, grab, interaction
├── Tools/            # Tool usage logic (hammer, drill, saw)
├── Machines/         # CNC, conveyor, robotic arm logic
├── UI/               # Control panel UI + machine panels
├── GameState/        # Task progression, scoring, feedback
└── Utilities/        # Editor helpers, mesh utilities
```

> Only `Player/` and `Utilities/` exist so far. All other folders are planned.

---

## Existing Scripts

### `Assets/Scripts/Player/PlayerController.cs`
- **Type:** `MonoBehaviour`, requires `CharacterController`
- **Purpose:** Desktop-mode fly camera for editor testing. WASD + Q/E movement, mouse look with pitch clamping, cursor lock toggle.
- **Status:** Placeholder — will be replaced or wrapped by a VR XR Rig controller.
- **Known issue:** Uses legacy `Input` class, inconsistent with project's New Input System setting. Do not extend this pattern — use `InputSystem` actions from `Assets/Settings/InputSystem_Actions.inputactions` instead.

### `Assets/Scripts/Utilities/JoinMeshes.cs`
- **Type:** `MonoBehaviour`
- **Purpose:** On `Start()`, combines all `MeshFilter` components in children into one merged mesh, then deactivates child objects. Used for draw call reduction on static geometry.
- **Status:** Utility — no changes needed.

---

## Planned Script Systems

Each system below maps to a planned folder and set of scripts. When implementing, create scripts in the correct folder and follow the naming conventions shown.

### 1. VR Player Movement & Interaction (`Scripts/Player/`)
VR locomotion and hand interaction using XR SDK. Replaces `PlayerController.cs` for VR builds.

Key scripts to create:
- `VRPlayerController.cs` — XR Rig movement (teleport / continuous)
- `HandInteractor.cs` — grab detection, ray interactor, haptic feedback
- `GrabSystem.cs` — handles pickup, hold, release of interactable objects

**Dependencies:** Unity XR Interaction Toolkit (`com.unity.xr.interaction.toolkit`) — not yet in manifest, must be added.

---

### 2. Tool Usage Logic (`Scripts/Tools/`)
Defines behavior when tools are used.

Key scripts to create:
- `HammerTool.cs` — nail driving, surface detection, finger-hit safety reaction
- `DrillTool.cs` — rotation, contact detection
- `SawTool.cs` — cutting plane, material splitting
- `ToolBase.cs` — abstract base class shared by all tools

**Pattern:** Tools should use events (`UnityEvent` or C# `Action`) to notify game state and feedback systems. Avoid direct coupling between tool scripts and UI.

---

### 3. Game States & Task Progression (`Scripts/GameState/`)
Linear task flow: the player completes steps in order. Each step has entry conditions, a goal, and an exit trigger.

Key scripts to create:
- `TaskManager.cs` — singleton, holds current task state and step index
- `Task.cs` — serializable data class describing a single task (name, description, goal type)
- `TaskStep.cs` — individual step within a task, with completion condition
- `GameStateEvents.cs` — static event bus for state changes

**Pattern:** Use a scriptable object per task (`Task.asset`) so designers can configure steps without code changes.

---

### 4. Machine Control Panels (`Scripts/UI/` and `Scripts/Machines/`)
World-space UI panels attached to machines.

Key scripts to create:
- `ControlPanel.cs` — base class; manages button press events, display state
- `CNCControlPanel.cs` — extends `ControlPanel`; exposes speed, axis, start/stop controls
- `ConveyorControlPanel.cs` — belt speed, direction toggle
- `PanelButton.cs` — physical VR-pressable button with press/release events

**Pattern:** Panels communicate with their machine via interface (`ICNCMachine`, `IConveyor`), not direct MonoBehaviour references.

---

### 5. CNC Cutting System (`Scripts/Machines/`)
Controls the CNC router: positioning, cutting pass, result mesh generation.

Key scripts to create:
- `CNCMachine.cs` — state machine (Idle → Positioning → Cutting → Done)
- `CNCCutter.cs` — moves the tool head along a path, triggers cut events
- `WoodCutResult.cs` — spawns or deforms result mesh after a cut
- `CuttingPath.cs` — scriptable object defining cut geometry (line, curve, pocket)

**Pattern:** The CNC uses a state machine enum. External systems (panel, task manager) call `CNCMachine.StartCut()` and listen to `CNCMachine.OnCutComplete`.

---

### 6. Conveyor & Object Transfer (`Scripts/Machines/`)
Moves objects between machines automatically or on trigger.

Key scripts to create:
- `ConveyorBelt.cs` — applies velocity to objects on the belt surface via `OnTriggerStay`
- `TransferPoint.cs` — destination marker; fires event when an object arrives
- `ObjectSpawner.cs` — spawns wood blanks at the start of the conveyor

---

### 7. Scoring, Feedback & Consequence System (`Scripts/GameState/`)
Tracks score, delivers feedback (audio, visual, haptic), applies penalties.

Key scripts to create:
- `ScoreManager.cs` — singleton, tracks score and error count
- `FeedbackManager.cs` — plays sounds, particle effects, and haptic pulses
- `SafetyEvent.cs` — data class representing a safety violation (type, severity, penalty)
- `ConsequenceSystem.cs` — listens for safety events, applies slowdowns or retries

**Pattern:** Use a centralized event bus. Tools and machines raise events; `FeedbackManager` and `ConsequenceSystem` respond independently.

---

## 3D Models (Assets/Models/)

| Model | File | Notes |
|---|---|---|
| CNC Machine | `CNC2.fbx` | Main CNC router — needs control panel attachment points |
| Control Panel | `ControlPanel.fbx` | Currently a mesh only — no scripts yet |
| Conveyor | `Conveyor.fbx` | Belt conveyor — needs `ConveyorBelt.cs` |
| Robotic Arm | `RoboticArm.fbx` | Phase 2 — joints need rigging for animation |
| Screen | `Screen.obj` | Display surface for panel UI |
| Table | `Table.fbx` | Workbench |

---

## Third-Party Assets

Do not modify anything under `Assets/ThirdParty/`. These are imported packages.

| Pack | Path | Contents |
|---|---|---|
| CoolWorks Carpentry Tools | `ThirdParty/CoolWorks_Studio/Carpentry_Tools/` | Hand tools: axe, chisel, drill, saw, plane, rasp (FBX + PBR textures) |
| Factory Tools | `ThirdParty/FactoryTools/` | Hammer, nail, wrench, screwdriver, shelf, workbench (FBX) |
| Free Industrial Models | `ThirdParty/FreeIndustrialModels/` | Pallet, pallet jack, wood planks (FBX + PBR textures) |
| LeartesStudios Coffee Shop | `ThirdParty/LeartesStudios/` | HDRP demo scenes — reference for interior lighting only, not for use in URP build |
| Urban American Assets | `ThirdParty/UrbanAmericanAssets/` | Paint booth scene — reference only |

---

## Render Pipeline Notes

- Pipeline: **URP 17.3.0**
- All materials must use URP-compatible shaders (`Universal Render Pipeline/Lit`, `Universal Render Pipeline/Simple Lit`, etc.)
- The `LeartesStudios` assets use HDRP shaders — they will appear pink in this project. Do not use them in the main scene.
- Render pipeline assets are in `Assets/Settings/`. The active asset is `PC_RPAsset.asset`.

---

## Input System Notes

- The project uses the **New Input System** (`com.unity.inputsystem` 1.17.0).
- The input action map is at `Assets/Settings/InputSystem_Actions.inputactions`.
- **Do not use** `UnityEngine.Input` (legacy). Use `InputAction`, `InputActionReference`, or generated C# classes from the `.inputactions` asset.
- `PlayerController.cs` currently uses legacy input — this is a known inconsistency and will be replaced by the VR controller.

---

## Code Conventions

### Naming
- Classes: `PascalCase` — `CNCMachine`, `HandInteractor`
- Methods: `PascalCase` — `StartCut()`, `OnGrabBegin()`
- Private fields: `camelCase` with underscore prefix — `_currentState`, `_speed`
- Public serialized fields: `camelCase` — `moveSpeed`, `cuttingDepth`
- Constants: `UPPER_SNAKE_CASE` — `MAX_CUT_DEPTH`
- Events: `On` prefix — `OnCutComplete`, `OnTaskCompleted`

### Structure
- One class per file
- Scripts go in the correct domain folder (see Architecture Overview above)
- Use `[Header("...")]` and `[Tooltip("...")]` on all serialized fields
- Use `[SerializeField] private` instead of `public` for inspector-exposed fields
- Use `RequireComponent` where a dependency is mandatory

### Events
- Prefer C# `Action` / `Func` for internal communication between scripts
- Use `UnityEvent` for inspector-wired connections (e.g. button press → machine start)
- Use a static event bus (`GameStateEvents.cs`) for global cross-system notifications

### Performance (VR Critical)
- Target: 72 fps minimum (90 fps preferred) on Meta Quest
- Avoid `FindObjectOfType` at runtime — cache references in `Awake()`
- Avoid `Update()` polling where events can be used instead
- Keep draw calls low — use GPU instancing on repeated objects (nails, planks)
- Use LODs on all high-poly models
- Profile with Unity Profiler before and after any new system

---

## Git Conventions

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full workflow. Summary:

- Branch prefix: `feature/`, `fix/`, `chore/`, `refactor/`
- Commit format: `feat:`, `fix:`, `chore:`, `refactor:` + short description
- Never push to `main` directly
- Always commit `.meta` files
- Close Unity before git commands

---

## Current Status (Phase 1)

| System | Status |
|---|---|
| Player movement (desktop) | Done — `PlayerController.cs` |
| VR XR Rig | Not started |
| Grab / interaction | Not started |
| CNC control panel | In progress — `feature/addControlPanelScripts` branch |
| CNC cutting logic | Not started |
| Conveyor system | Not started |
| Task progression | Not started |
| Scoring & feedback | Not started |
| Safety system | Not started |
| Workshop environment | Blocked — models imported, scene layout in progress |
| Tool models | Imported (third-party) |
| Lighting & materials | Basic only |
| Animations | Not started |
| Sound | Not started |
