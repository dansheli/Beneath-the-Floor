# Beneath The Floor – Visuals, Models & Player V1 – Diagnostic Report

**Date:** December 15, 2025
**Project:** Beneath The Floor
**Purpose:** Full visual/content audit before implementing Visuals V1

---

## 1. High-Level Overview

- **Visual Completion:** ~15% - Project is functionally complete but visually bare-bones
- **3D Content vs Placeholders:** Nearly 100% placeholders - machines, tools, and resources are all runtime-generated primitives (cubes, cylinders, spheres)
- **Player Representation:** Camera-only first-person view with a procedurally generated "shovel" (cylinder + cube)
- **Art Style Coherence:** Currently no coherent style - all primitives with basic flat colors
- **Asset Packs Available:** Two unused low-poly asset packs exist (`Factory Tools`, `EKstudio LowPoly Factory Machine Pack Demo`) that could provide immediate visual upgrades
- **Render Pipeline:** URP (Universal Render Pipeline) properly configured with multiple quality tiers

---

## 2. Existing Player Setup

### Player-Related Prefabs/Models Found

| Asset | Path | Status |
|-------|------|--------|
| Player Prefabs folder | `Assets/Prefabs/Player/` | **Empty** |
| FirstPersonController.cs | `Assets/Scripts/Player/FirstPersonController.cs` | Active - main player controller |
| PlayerSetup.cs | `Assets/Scripts/Player/PlayerSetup.cs` | Active - initializes player |
| HeldToolController.cs | `Assets/Scripts/Tools/HeldToolController.cs` | Active - manages held tool visuals |

### Active Player Controller Analysis

The current player in `BasementScene` consists of:

```
Player (GameObject)
├── CharacterController
├── FirstPersonController
├── InteractionSystem
├── PlayerMovementFX
├── FootstepAudio
├── HeldToolController
├── InteractionHighlighter
├── CameraHolder/
│   └── Main Camera
└── GroundCheck
```

**Visual State:**
- **No visible body or hands** - pure camera-only first person
- **No Animator or animation controller** attached to player
- **Tool visual is procedurally generated** at runtime (cylinder handle + cube head)
- Tool is parented to a "ToolHolder" created under Main Camera

**Tool Generation Code** (`HeldToolController.cs:322-368`):
```csharp
// Creates simple primitive shovel at runtime
GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
handle.transform.localScale = new Vector3(0.04f, 0.25f, 0.04f);
GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
head.transform.localScale = new Vector3(0.12f, 0.02f, 0.15f);
```

### Conclusion

**We need to build a new visual player from scratch.** The current player has:
- No model/mesh
- No skeletal rig
- No animations
- Procedural placeholder tool only

However, the `HeldToolController` architecture is solid and can easily accept real tool prefabs via the `toolPrefabs` list.

---

## 3. Tools & Pickaxes – Current State

### Tool-Like Assets Found

| Asset | Path | Type | In Use |
|-------|------|------|--------|
| Shovel.fbx | `Assets/Factory Tools/Models/Shovel.fbx` | Low-poly model | **No** |
| Hammer.fbx | `Assets/Factory Tools/Models/Hammer.fbx` | Low-poly model | No |
| Axe.fbx | `Assets/Factory Tools/Models/Axe.fbx` | Low-poly model | No |
| PipeWrench.fbx | `Assets/Factory Tools/Models/PipeWrench.fbx` | Low-poly model | No |
| Wrench.fbx | `Assets/Factory Tools/Models/Wrench.fbx` | Low-poly model | No |
| Screwdriver.fbx | `Assets/Factory Tools/Models/Screwdriver.fbx` | Low-poly model | No |
| WoodenWorkbench.fbx | `Assets/Factory Tools/Models/WoodenWorkbench.fbx` | Low-poly model | No |
| DesktopToolStand.prefab | `Assets/Factory Tools/Prefabs/DesktopToolStand.prefab` | Prefab | No |

### Current Runtime Tool System

The `HeldToolController` creates three tool tiers at runtime:

| Tool Name | Tier | Handle Color | Head Color | Status |
|-----------|------|--------------|------------|--------|
| Wooden Shovel | 1 | Brown (0.6, 0.4, 0.2) | Gray (0.5, 0.5, 0.5) | Placeholder primitive |
| Iron Shovel | 2 | Dark Brown | Light Gray | Placeholder primitive |
| Steel Shovel | 3 | Darker Brown | Bright Gray | Placeholder primitive |

### Pickaxe Assessment

**No pickaxe models exist in the project.** The current tools are all "shovels" despite the upgrade system referring to "pickaxes" in the code.

**Recommendation:**
- The `Factory Tools` asset pack has a **Shovel.fbx** that could immediately replace the placeholder
- **Pickaxe models need to be created or sourced** - not available in current assets
- Consider creating 3 tiers: Wooden Pickaxe, Iron Pickaxe, Steel/Crystal Pickaxe

---

## 4. Machines & Stations

### Workbench (Upgrade Station)

| Property | Value |
|----------|-------|
| Scene Object | `Basement/Machines/UpgradeStation` |
| Script | `UpgradeStation.cs` (fully functional) |
| Visual | Runtime primitives (Base cube, Pedestal cylinder, Console cube) |
| Interaction | Working - opens upgrade UI |

**Current Visual Structure:**
```
UpgradeStation
├── Base (Cube)
├── Pedestal (Cylinder)
└── Console (Cube)
```

**Candidate Replacement:** `EKstudio/LowPoly Factory Machine Pack Demo/Models/MachineMesh/Machn_2.fbx` or similar

### Sell Station / Trade Terminal

| Property | Value |
|----------|-------|
| Scene Object | `Basement/Machines/TradeTerminal` |
| Script | `TradeTerminal.cs` (fully functional) |
| Visual | Runtime primitives (TerminalBody cube, Screen cube) |
| Interaction | Working - opens sell UI |

**Current Visual Structure:**
```
TradeTerminal
├── TerminalBody (Cube)
└── Screen (Cube)
```

### Refinery

| Property | Value |
|----------|-------|
| Scene Object | `Basement/Machines/Refinery` |
| Script | `Refinery.cs` (functional) |
| Visual | Runtime primitives (Body cube, Chimney cylinder) |

### Energy Generator

| Property | Value |
|----------|-------|
| Scene Object | `Basement/Machines/EnergyGenerator` |
| Script | `EnergyGenerator.cs` (functional) |
| Visual | Runtime primitives (Body cube, Generator cube) |

### Inactive Machine (Story Machine)

| Property | Value |
|----------|-------|
| Scene Object | `Basement/OldMachinery` |
| Script | None - purely visual placeholder |
| Visual | Runtime primitives (MachineBody, MachineTop, Pipe) |
| Purpose | Placeholder for story-related mysterious machine |

**Current Visual Structure:**
```
OldMachinery
├── MachineBody (Cube)
├── MachineTop (Capsule)
└── Pipe (Cylinder)
```

### Machine V1 Recommendations

| Machine | Current V1 Candidate | Notes |
|---------|---------------------|-------|
| Workbench/UpgradeStation | Use `WoodenWorkbench.fbx` from Factory Tools | Good match |
| Trade Terminal | Create from Machine_1-7.fbx in Industrial_Machine_Models | Needs screen added |
| Inactive Machine | Use `Machine_4.fbx` or `Machine_7.fbx` | Larger, more mysterious |
| Refinery | Use `Machine_3.fbx` or Machn_2.fbx | Industrial look |
| Energy Generator | Use `Machine_5.fbx` | Generator-like |

---

## 5. Resource / Ore Prefabs

### Resource Spawning System

Resources are spawned by `DigWorldDropSpawner.cs` which creates **runtime spheres** with color coding:

```csharp
_runtimeDefaultPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
_runtimeDefaultPrefab.transform.localScale = Vector3.one * defaultSphereRadius * 2f;
```

### Resource Types and Colors

| Resource Type | Color | Scale | Layer | Visual Status |
|--------------|-------|-------|-------|---------------|
| Dirt | Brown (0.55, 0.40, 0.25) | 0.8 | 1 | Colored sphere |
| SoftStone | Light Gray | 0.9 | 1 | Colored sphere |
| Clay | Orange-Brown | 0.85 | 1 | Colored sphere |
| IronNugget | Rusty (0.70, 0.55, 0.40) | 1.0 | 1 | Colored sphere |
| CopperFragment | Copper (0.80, 0.50, 0.30) | 0.95 | 1 | Colored sphere |
| Coal | Black (0.20, 0.20, 0.20) | 0.9 | 2 | Colored sphere |
| Stone | Gray | 1.0 | 2 | Colored sphere |
| CrystalShard | Purple (0.70, 0.50, 0.90) | 1.0 | 4 | Colored sphere |
| AncientOre | Green-Gray | 1.2 | 3 | Colored sphere |
| CrystalCoreFragment | Pink-Purple | 1.3 | 5 | Colored sphere |

### Resource Prefab Folders

| Path | Contents |
|------|----------|
| `Assets/Prefabs/Items/` | **Empty** |
| `Assets/GameData/` | Contains `ResourceDropConfig.asset` (colors only, no prefabs) |

### Assessment

**All resources are placeholder spheres.** The color system is well-designed and provides visual distinction, but actual 3D models are needed for:

**Priority Resources (need unique models ASAP):**
1. **IronNugget/IronChunk** - Core progression resource
2. **Coal** - Fuel/common resource
3. **CopperFragment/CopperPiece** - Secondary metal
4. **CrystalShard** - Rare/valuable
5. **AncientOre** - Special/story resource

**Lower Priority (can use generic rock variations):**
- Dirt, SoftStone, Clay, Stone, Sandstone
- Various dust types

---

## 6. Basement / Hub – Visual State

### Scene Information

| Property | Value |
|----------|-------|
| Scene Name | BasementScene |
| Scene Path | `Assets/Scenes/BasementScene.unity` |
| Build Index | 1 |

### Composition

**Structure:**
```
Basement (root container)
├── BasementFloor (MeshFilter + BoxCollider - disabled renderer)
├── Basement_Wall_North/South/East/West (4 cube walls)
├── BasementCeiling (cube)
├── BasementFloor_Ring/ (4 floor pieces around dig hole)
├── OldMachinery/ (story machine placeholder)
├── StairsUp/ (10 step cubes with SceneTransition)
└── Machines/
    ├── Refinery
    ├── UpgradeStation
    ├── EnergyGenerator
    └── TradeTerminal
```

### Visual Quality Assessment

| Element | Current State | Quality |
|---------|--------------|---------|
| Walls | Basic cubes with `Basement_Wall.mat` | Placeholder |
| Floor | Disabled mesh, ring pieces around hole | Placeholder |
| Ceiling | Basic cube with `Basement_Ceiling.mat` | Placeholder |
| Stairs | 10 stacked cubes | Placeholder |
| Machines | Runtime primitives | Placeholder |
| Props/Clutter | None | Missing |
| Cables/Pipes | None | Missing |
| Decals/Posters | None | Missing |

### Lighting Setup

| Light | Type | Position | Purpose |
|-------|------|----------|---------|
| Directional Light | Directional | Scene root | Global illumination |
| BasementLight_1 | Point | Basement area | Local lighting |
| BasementLight_2 | Point | Basement area | Local lighting |
| BasementLight_Stairs | Point | Near stairs | Stair illumination |
| DepthLightingController | Empty | - | Runtime depth lighting |

### Atmosphere Assessment

**Current Mood:** None - completely flat and default
- No color temperature variation
- No contrast between work areas and dark corners
- No fog or atmospheric effects
- No visual focal points

### What Basement V2 Needs Most

1. **Replace primitive walls/floor/ceiling** with modular low-poly pieces with more detail
2. **Add warm point lights** above each machine station
3. **Add 2-3 large "hero" props** (old generator, broken machinery, storage tanks)
4. **Add small clutter props** (tools on ground, crates, barrels, cables)
5. **Implement slight fog** for depth and atmosphere
6. **Add emissive materials** to machine screens and indicators
7. **Create visual hierarchy** - brighter work areas, darker storage corners
8. **Add ceiling details** - exposed pipes, hanging lights, vents
9. **Use the existing asset packs** (`Factory Tools`, `EKstudio`) for immediate props
10. **Add environmental storytelling** - old posters, scratched walls, debris

---

## 7. Rendering, Lighting & Post-Processing

### Render Pipeline

| Property | Value |
|----------|-------|
| Pipeline | **URP (Universal Render Pipeline)** |
| Quality Tiers | Performant, Balanced, HighFidelity |
| Renderer Assets | `URP-Performant.asset`, `URP-Balanced.asset`, `URP-HighFidelity.asset` |

### Post-Processing

| Component | Status |
|-----------|--------|
| Post-Processing Volumes | **Not found in scene** |
| Global Volume | Not configured |
| Camera Post Effects | UniversalAdditionalCameraData present but no overrides |

### Current Lighting Issues

1. **Too flat** - Directional light + even point lights = no contrast
2. **No color variation** - All lights appear neutral white
3. **No shadows depth** - Underground area should feel dark with pockets of light
4. **Missing ambient occlusion** - Corners don't feel grounded
5. **No bloom** - Emissive elements (screens, indicators) don't glow

### Recommended Lighting/Post-Processing Adjustments

1. **Add Global Post-Processing Volume** with:
   - Slight warm color grading (underground = warm lamp light)
   - Subtle bloom (intensity 0.3-0.5) for machine screens
   - Light vignette (intensity 0.2) for focus
   - Ambient Occlusion (if performance allows)

2. **Reduce Directional Light intensity** for underground - main light should be artificial

3. **Add colored point lights** per machine:
   - UpgradeStation: Cyan/Blue
   - TradeTerminal: Green
   - Refinery: Orange/Red
   - EnergyGenerator: Yellow

4. **Implement depth-based lighting** - darker as you go deeper (DepthLightingController exists but may need configuration)

5. **Add subtle fog** - Distance fog with warm brown tint for underground atmosphere

6. **Use light cookies** on point lights for more interesting shadow patterns

7. **Enable soft shadows** on main lights for better grounding

---

## 8. Art Style Assessment

### Current Asset Analysis

| Asset Category | Style | Notes |
|---------------|-------|-------|
| Basement geometry | Primitive boxes | No style yet |
| Machine geometry | Primitive shapes | No style yet |
| Tool geometry | Primitive cylinder+cube | No style yet |
| Resources | Colored spheres | No style yet |
| Factory Tools pack | **Low-poly stylized** | Flat colors, clean geometry |
| EKstudio pack | **Low-poly stylized** | Color palette textures, clean geometry |
| Industrial_Machine_Models | **Semi-realistic** | More detailed, PBR materials |

### Material Usage

| Material | Path | Style |
|----------|------|-------|
| Basement_Wall.mat | `Assets/Materials/` | URP Lit, solid color |
| Basement_Floor.mat | `Assets/Materials/` | URP Lit, solid color |
| Diggable_Dirt.mat | `Assets/Materials/` | URP Lit, brown color |
| M_ColorPalette.mat | `Assets/EKstudio/.../Material/` | Texture atlas, stylized |
| M_Pack1.mat, M_Pack2.mat | `Assets/EKstudio/.../Material/` | Texture atlas, stylized |
| BaseColor.mat | `Assets/Factory Tools/Materials/` | Flat color, stylized |

### Style Conflicts

**Potential Conflict:** The `Industrial_Machine_Models` pack is more realistic/detailed than the `EKstudio` and `Factory Tools` packs which are clearly low-poly stylized.

### Recommendation

**Commit to: Low-poly, stylized with flat/gradient colors**

Reasons:
1. The majority of available assets (Factory Tools, EKstudio) follow this style
2. Low-poly is performant and scales well
3. Easier to create consistent new content
4. Fits the "underground workshop" aesthetic
5. The Industrial_Machine_Models can still be used but may need material adjustment

**Style Guidelines:**
- Flat or simple gradient colors (avoid complex PBR textures)
- Clean, faceted geometry (no smoothing where possible)
- Color-coded elements for gameplay clarity (e.g., interactables glow)
- Warm color palette for basement (browns, oranges, warm grays)
- Cool accent colors for technology (cyan, green indicators)

---

## 9. Gaps & Missing Content

| Category | Current State Summary | Missing / Weak Elements | Priority |
|----------|----------------------|------------------------|----------|
| **Player** | Camera-only, no visible body, procedural shovel | Visible hands/arms, proper tool models, idle/walk/dig animations | **High** |
| **Tools** | Procedural cylinder+cube, 3 tiers defined | Actual pickaxe models (3 tiers), tool attachment system for real models | **High** |
| **Machines** | All functional but primitive visuals | Real 3D models for all 4 machines, emissive screens, particle effects | **High** |
| **Resources** | Colored spheres only | Unique models for 5+ core resources (Iron, Coal, Copper, Crystal, Ancient) | **High** |
| **Basement V2** | Primitive boxes, no props/atmosphere | Wall/floor detail, props, clutter, cables, environmental storytelling | **Medium** |
| **Lighting/Style** | Flat lighting, no post-processing | Color grading, bloom, fog, machine-specific colored lights, shadows | **Medium** |

---

## 10. Recommended Next Steps (Creation Plan)

### 1. Player Prefab V1 (Visual)

**Phase 1: Tool-First Approach** (Recommended start)
1. Keep camera-only player for now
2. Replace procedural tool with real model:
   - Create/source pickaxe model
   - Set up in `Assets/Prefabs/Tools/`
   - Configure in `HeldToolController.toolPrefabs` list
3. Add simple dig animation (tool swing) via Animation component

**Phase 2: Visible Hands** (Later)
1. Add first-person arms model (low-poly, stylized)
2. Rig arms with bones for tool holding
3. Create idle/swing animations
4. Parent tool to hand bone

**Tool Attachment Architecture:**
```
Main Camera
└── ToolHolder (existing)
    └── [Tool Prefab] ← Replace runtime primitive with prefab reference
        ├── Handle (mesh)
        └── Head (mesh)
```

### 2. Pickaxe Prefabs (Tier 1-3)

**Folder Structure:**
```
Assets/
├── Prefabs/
│   └── Tools/
│       ├── Pickaxe_Tier1_Wooden.prefab
│       ├── Pickaxe_Tier2_Iron.prefab
│       └── Pickaxe_Tier3_Steel.prefab
├── Models/
│   └── Tools/
│       ├── Pickaxe_Tier1.fbx
│       ├── Pickaxe_Tier2.fbx
│       └── Pickaxe_Tier3.fbx
└── Materials/
    └── Tools/
        ├── M_Pickaxe_Wood.mat
        ├── M_Pickaxe_Iron.mat
        └── M_Pickaxe_Steel.mat
```

**Naming Convention:** `[ItemType]_Tier[N]_[Material].prefab`

**Pivot Placement:** Base of handle (where hand grips)

**Scale:** Normalize to ~1 unit handle length, test in-game for comfortable first-person view

**Colliders:** None needed for held tools (visual only)

**Tier Differentiation:**
| Tier | Material | Head Shape | Special |
|------|----------|-----------|---------|
| 1 | Wood handle, stone head | Simple, chunky | None |
| 2 | Wood handle, iron head | Sharper, metal shine | Slight metallic material |
| 3 | Metal handle, steel head | Sleek, refined | Emissive edge glow |

### 3. Resource Prefabs

**Folder Structure:**
```
Assets/
├── Prefabs/
│   └── Resources/
│       ├── Common/
│       │   ├── Resource_Dirt.prefab
│       │   ├── Resource_Stone.prefab
│       │   └── Resource_Coal.prefab
│       ├── Metals/
│       │   ├── Resource_IronNugget.prefab
│       │   ├── Resource_IronChunk.prefab
│       │   ├── Resource_CopperFragment.prefab
│       │   └── Resource_CopperPiece.prefab
│       └── Rare/
│           ├── Resource_CrystalShard.prefab
│           ├── Resource_CrystalDust.prefab
│           └── Resource_AncientOre.prefab
├── Models/
│   └── Resources/
│       ├── Ore_Generic_Small.fbx
│       ├── Ore_Generic_Medium.fbx
│       ├── Crystal_Shard.fbx
│       └── Ore_Ancient.fbx
```

**Design Language:**
| Category | Shape | Color Family | Emissive |
|----------|-------|--------------|----------|
| Common (dirt, stone) | Rounded rocks | Browns, grays | No |
| Metals (iron, copper) | Angular chunks with metallic faces | Orange-brown, copper | Subtle |
| Rare (crystal, ancient) | Faceted crystals, geometric | Purple, teal, gold | Yes |

**Integration:**
1. Update `ResourceDropConfig.asset` to reference new prefabs
2. Each prefab needs `ResourcePickup` component (added at runtime currently)
3. Add simple rotation animation for dropped items

### 4. Basement V2 Visual Upgrade

**Ordered Action Plan:**

1. **Lighting First** (1 hour)
   - Add Global Volume with color grading + bloom
   - Reduce directional light to 0.3 intensity
   - Add colored point lights per machine

2. **Replace Machine Visuals** (2-3 hours)
   - Swap UpgradeStation → WoodenWorkbench.fbx + screen
   - Swap other machines with Industrial_Machine_Models
   - Add emissive materials to screens

3. **Add Hero Props** (1-2 hours)
   - Place 2-3 large machines/generators from asset packs
   - Position in corners for visual interest

4. **Add Clutter** (1 hour)
   - Scatter tools from Factory Tools pack
   - Add barrels, crates from EKstudio pack
   - Place tool racks on walls

5. **Environmental Detail** (1 hour)
   - Add ceiling pipes (cylinders)
   - Create hanging light fixtures
   - Add floor cables/debris

6. **Atmosphere Polish** (30 min)
   - Enable fog (warm brown, low density)
   - Fine-tune light intensities
   - Add dust particle system (optional)

---

## Summary

The project has solid gameplay systems but is visually at the "gray box" stage. The good news:
- Two unused asset packs provide immediate low-poly content
- Architecture supports easy prefab swapping
- URP is properly configured
- All systems are functional and just need visual polish

**Immediate Wins Available:**
1. Swap procedural tools → Factory Tools Shovel.fbx
2. Swap machine primitives → Industrial_Machine_Models
3. Add post-processing volume
4. Scatter props from asset packs

**Requires New Content:**
1. Pickaxe models (3 tiers)
2. Resource ore models (5-10 unique)
3. First-person arms (if desired)

---

*Report generated for Beneath The Floor – Visuals, Models & Player V1*
