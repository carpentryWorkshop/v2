# CNC Machine Setup Guide

Complete step-by-step guide to set up the CNC machine system in Unity.

---

## Prerequisites

Before starting, ensure all scripts are in place:

**In `Assets/Scripts/Machines/`:**
- `CNCMachine.cs`
- `CNCCutter.cs`
- `CuttingPath.cs`
- `CNCInputHandler.cs`
- `CNCAutoController.cs`
- `MecheRotator.cs`
- `ShapePathGenerator.cs`
- `WoodPiece.cs`
- `WoodSpawner.cs`

**In `Assets/Scripts/UI/`:**
- `CNCControlPanel.cs`
- `CNCScreenDisplay.cs`
- `CNC3DButton.cs`

---

## Phase 1: Control Panel Buttons

### Step 1.1: Add 3D Button Models

1. In the Hierarchy, locate your CNC machine's control panel area
2. Add 3 separate 3D models as children of the control panel:
   - Name the first one exactly: `start`
   - Name the second one exactly: `stop`
   - Name the third one exactly: `switch`

### Step 1.2: Configure Button Colliders

For each button (`start`, `stop`, `switch`):

1. Select the button GameObject
2. Add Component → `Box Collider`
3. Enable `Is Trigger` checkbox
4. Adjust collider size to fit the button mesh

### Step 1.3: Add CNC3DButton Component

For each button (`start`, `stop`, `switch`):

1. Select the button GameObject
2. Add Component → `CNC3DButton`
3. Configure settings:
   - `Press Depth`: `0.005` (adjust based on button size)
   - `Press Duration`: `0.1`
   - `Audio Source`: (optional) assign an AudioSource
   - `Click Sound`: (optional) assign an AudioClip

### Checkpoint 1

1. Enter Play Mode
2. Click on each button (start, stop, switch)
3. **Expected**: Each button animates downward when clicked, then returns up
4. **If not working**: Check that colliders have `Is Trigger` enabled

---

## Phase 2: Wire Control Panel to Machine

### Step 2.1: Locate CNCMachine Component

1. Find the root CNC machine GameObject in Hierarchy
2. Verify it has the `CNCMachine` component attached
3. If not, Add Component → `CNCMachine`

### Step 2.2: Configure CNCControlPanel

1. Select the control panel parent GameObject
2. Add Component → `CNCControlPanel`
3. Assign references in Inspector:
   - `Machine`: drag the CNCMachine GameObject
   - `Start Button`: drag the `start` GameObject
   - `Stop Button`: drag the `stop` GameObject
   - `Mode Button`: drag the `switch` GameObject

### Step 2.3: (Optional) Mode Indicators

If you have indicator lights or text for mode display:

1. In `CNCControlPanel` component:
   - `Manual Mode Indicator`: assign the manual mode indicator GameObject
   - `Auto Mode Indicator`: assign the auto mode indicator GameObject

### Checkpoint 2

1. Enter Play Mode
2. Click the `start` button
3. **Expected**: Machine starts (check Console for "CNC Machine Started" or similar)
4. Click the `stop` button
5. **Expected**: Machine stops
6. Click the `switch` button
7. **Expected**: Mode toggles between Manual and Auto (check Console)
8. **If not working**: Verify all references are assigned in CNCControlPanel

---

## Phase 3: CNC Cutter 3-Axis Movement

### Step 3.1: Understand the Hierarchy

Your CNC cutter should have this nested structure:

```
cncCutter (X-axis movement)
└── spindleHolder (Z-axis movement)
    └── spindleFinal (Y-axis/depth movement)
        └── meche (drill bit mesh)
```

### Step 3.2: Configure CNCCutter Component

1. Select the `cncCutter` GameObject
2. Locate the `CNCCutter` component (should already exist)
3. Assign transform references:
   - `Spindle Holder Transform`: drag `spindleHolder` GameObject
   - `Spindle Final Transform`: drag `spindleFinal` GameObject

### Step 3.3: Configure CuttingPath Bounds

1. On the same GameObject, locate `CuttingPath` component
2. Set X-axis bounds:
   - `Min Width`: minimum X position (e.g., `-0.1`)
   - `Max Width`: maximum X position (e.g., `0.1`)
3. Set Z-axis bounds:
   - `Min Length`: minimum Z position (e.g., `-0.15`)
   - `Max Length`: maximum Z position (e.g., `0.15`)
4. Set Y-axis bounds:
   - `Min Height`: minimum Y position / max depth (e.g., `-0.02`)
   - `Max Height`: maximum Y position / raised (e.g., `0.05`)

### Step 3.4: Add CNCInputHandler

1. Select the CNC machine root GameObject
2. Add Component → `CNCInputHandler`
3. Assign references:
   - `Machine`: drag the CNCMachine component
   - `Cutter`: drag the CNCCutter component
4. Configure speed:
   - `Move Speed`: `0.05` (adjust for desired speed)

### Checkpoint 3

1. Enter Play Mode
2. Click `start` button to start the machine
3. Press keyboard keys:
   - `I` → meche moves forward (+Z)
   - `K` → meche moves backward (-Z)
   - `J` → meche moves left (-X)
   - `L` → meche moves right (+X)
   - `W` → meche moves up (+Y)
   - `X` → meche moves down (-Y)
4. **Expected**: Meche moves in all 6 directions, stops at bounds
5. **If not working**: 
   - Check CNCInputHandler references are assigned
   - Check CNCCutter transform references are assigned
   - Verify machine is running (started)

---

## Phase 4: Meche Rotation

### Step 4.1: Add MecheRotator Component

1. Navigate to the `meche` GameObject in Hierarchy:
   `cncCutter → spindleHolder → spindleFinal → meche`
2. Select the `meche` GameObject
3. Add Component → `MecheRotator`
4. Configure settings:
   - `Machine`: drag the CNCMachine component
   - `Rotation Speed`: `1500` (degrees per second, adjust as needed)
   - `Rotation Axis`: `(0, 1, 0)` for Y-axis spin (vertical)

### Checkpoint 4

1. Enter Play Mode
2. Observe the meche - it should NOT be spinning (machine stopped)
3. Click `start` button
4. **Expected**: Meche starts spinning around its Y-axis
5. Click `stop` button
6. **Expected**: Meche stops spinning
7. **If not working**: Check that `Machine` reference is assigned in MecheRotator

---

## Phase 5: Wood Reset System

### Step 5.1: Prepare Wood Prefab

1. Locate your wood model: `Assets/my ressources/3D models/wooden-plywood-plank/source/woodenPlank.blend`
2. Drag it into the scene as a child of the CNC machine (position it on the work surface)
3. Create a prefab:
   - Drag the wood GameObject to `Assets/Prefabs/` folder (create folder if needed)
   - Name it `WoodPlank` or similar

### Step 5.2: Add WoodPiece Component

1. Select the wood GameObject in the scene (not the prefab)
2. Add Component → `WoodPiece`
3. Assign references:
   - `Wood Prefab`: drag the wood prefab from Project window
   - `Spawn Point`: (optional) drag a Transform for spawn position, or leave empty to use current position

### Step 5.3: Add WoodSpawner Component

1. Select the CNC machine root GameObject
2. Add Component → `WoodSpawner`
3. Assign references:
   - `Machine`: drag the CNCMachine component
   - `Wood Piece`: drag the WoodPiece component from the wood GameObject

### Checkpoint 5

1. Enter Play Mode
2. Move the wood or modify it somehow (to see the reset)
3. Click `start` button
4. **Expected**: Wood resets to its original state/position
5. Click `start` again
6. **Expected**: Wood resets again (fresh surface each start)
7. **If not working**: 
   - Check WoodSpawner references are assigned
   - Check WoodPiece has a valid prefab reference

---

## Phase 6: Auto Mode & Shape Selection

### Step 6.1: Add CNCAutoController

1. Select the CNC machine root GameObject
2. Add Component → `CNCAutoController`
3. Assign references:
   - `Machine`: drag the CNCMachine component
   - `Cutter`: drag the CNCCutter component
   - `Cutting Path`: drag the CuttingPath component
4. Configure settings:
   - `Move Speed`: `0.03` (speed during auto engraving)
   - `Engrave Depth`: `-0.01` (Y position when engraving)
   - `Travel Height`: `0.03` (Y position when moving between points)

### Step 6.2: Configure CNCScreenDisplay (Optional UI)

If you have a screen UI for shape selection:

1. Select the screen GameObject
2. Add Component → `CNCScreenDisplay`
3. Assign references:
   - `Machine`: drag the CNCMachine component
   - `Auto Controller`: drag the CNCAutoController component
4. For UI buttons (optional):
   - Create UI buttons for Rectangle, Circle, Triangle, Star
   - Assign them to the `Shape Buttons` array

### Step 6.3: Keyboard Shape Selection

Even without UI, shapes can be selected via keyboard in Auto mode:

| Key | Shape |
|-----|-------|
| `1` | Rectangle |
| `2` | Circle |
| `3` | Triangle |
| `4` | Star |
| `Tab` | Start engraving selected shape |

### Checkpoint 6

1. Enter Play Mode
2. Click `start` button to start machine
3. Click `switch` button to enter Auto mode
4. Press `1` to select Rectangle
5. Press `Tab` to start auto engraving
6. **Expected**: Meche automatically moves to engrave a rectangle shape
7. Try other shapes with `2`, `3`, `4` keys
8. **If not working**:
   - Verify mode is set to Auto (check Console or indicators)
   - Check CNCAutoController references are assigned
   - Ensure CuttingPath bounds are set correctly

---

## Final Verification

Run through this complete test sequence:

### Test Manual Mode

1. [ ] Enter Play Mode
2. [ ] Press `start` → machine runs, meche spins, wood resets
3. [ ] Verify mode is Manual (default)
4. [ ] Press `I/K/J/L` → meche moves on X/Z plane
5. [ ] Press `W/X` → meche moves up/down (Y axis)
6. [ ] Press `stop` → machine stops, meche stops spinning

### Test Auto Mode

7. [ ] Press `start` → machine runs
8. [ ] Press `switch` → mode changes to Auto
9. [ ] Press `1` → Rectangle selected
10. [ ] Press `Tab` → meche automatically engraves rectangle
11. [ ] Wait for completion
12. [ ] Press `2` then `Tab` → Circle engraved
13. [ ] Press `switch` → mode changes back to Manual
14. [ ] Press `I/J/K/L/W/X` → manual control works again

### Test Wood Reset

15. [ ] Press `stop` to stop machine
16. [ ] Press `start` → wood resets to fresh state
17. [ ] Engrave something
18. [ ] Press `stop` then `start` → wood is fresh again

---

## Troubleshooting

### Buttons don't respond to clicks
- Ensure each button has a `Collider` with `Is Trigger` enabled
- Check that `CNC3DButton` component is attached
- Verify your input system can raycast to triggers

### Meche doesn't move
- Check `CNCInputHandler` has Machine and Cutter references
- Verify machine is running (call Start first)
- Check `CNCCutter` has SpindleHolder and SpindleFinal transforms assigned

### Meche moves but ignores bounds
- Verify `CuttingPath` component has correct Min/Max values
- Check that MinHeight < MaxHeight, MinWidth < MaxWidth, MinLength < MaxLength

### Auto mode doesn't engrave
- Ensure mode is set to Auto (use switch button)
- Check `CNCAutoController` has all references assigned
- Verify a shape is selected before pressing Tab

### Wood doesn't reset
- Check `WoodSpawner` has Machine and WoodPiece references
- Verify `WoodPiece` has a valid prefab assigned
- Ensure you're pressing Start (not just stopping/starting)

---

## Quick Reference: Keyboard Controls

| Key | Action | Mode |
|-----|--------|------|
| `I` | Move forward (+Z) | Manual |
| `K` | Move backward (-Z) | Manual |
| `J` | Move left (-X) | Manual |
| `L` | Move right (+X) | Manual |
| `W` | Move up (+Y) | Manual |
| `X` | Move down (-Y) | Manual |
| `1` | Select Rectangle | Auto |
| `2` | Select Circle | Auto |
| `3` | Select Triangle | Auto |
| `4` | Select Star | Auto |
| `Tab` | Start engraving | Auto |

---

## Component Summary

| GameObject | Components |
|------------|------------|
| CNC Machine Root | `CNCMachine`, `CNCInputHandler`, `CNCAutoController`, `WoodSpawner` |
| cncCutter | `CNCCutter`, `CuttingPath` |
| meche | `MecheRotator` |
| Wood | `WoodPiece` |
| Control Panel | `CNCControlPanel` |
| start button | `CNC3DButton`, `BoxCollider` (trigger) |
| stop button | `CNC3DButton`, `BoxCollider` (trigger) |
| switch button | `CNC3DButton`, `BoxCollider` (trigger) |
| Screen (optional) | `CNCScreenDisplay` |
