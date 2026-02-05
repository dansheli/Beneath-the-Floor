# Game Design Document: Beneath the Floor

## Version 2.0 | February 2026

---

## 1. Game Overview

### 1.1 Title
**Beneath the Floor**

### 1.2 Genre
First-Person Exploration / Mining / Resource Management / Atmospheric Mystery

### 1.3 Platform
PC (Windows) — Unity 2022 LTS, Universal Render Pipeline (URP)

### 1.4 Namespace
All game code lives under the root namespace `BeneathTheFloor`, with sub-namespaces for each system (e.g. `BeneathTheFloor.Digging`, `BeneathTheFloor.Winch`, etc.).

### 1.5 Game Summary
The player inherits their grandfather's house and discovers a mysterious pull from beneath the basement floor. Using tools and a winch system, they dig down through layers of terrain, collecting resources, upgrading equipment, and uncovering an ancient secret — a glowing crystal deep underground. The game progresses through a guided mission system that teaches mechanics one at a time while telling a narrative through readable notes and environmental storytelling.

---

## 2. Scene Flow

The game has 4 scenes in build order:

| # | Scene | File | Purpose |
|---|-------|------|---------|
| 0 | Press Any Key | `PressAnyKeyScene.unity` | Splash/boot screen. Press any key to proceed. |
| 1 | Main Menu | `MainMenuScene.unity` | New Game, Continue, Settings, Quit. Video background. |
| 2 | House Building | `HouseBuilding.unity` | **The main gameplay scene.** House + basement + underground shaft — all in one scene. |
| 3 | Demo End | `DemoEndScene.unity` | End-of-demo screen after the final mission. |

### 2.1 PressAnyKeyScene
- **Controller:** `PressAnyKeyController` — waits for any input, then loads Main Menu.
- Minimal UI, atmospheric.

### 2.2 MainMenuScene
- **Controller:** `MainMenuController` — handles New Game, Continue, Settings, Quit buttons.
- **Video Background:** `MainMenuVideoBackground` — plays a looping video behind the menu.
- **Save/Load Menu:** `SaveLoadMenuUI` — shows save slot list with `SaveSlotUI` entries. Managed by `SavesIndex`.
- **Settings:** `SettingsUI` — quality, resolution, fullscreen, mouse sensitivity, volume.

### 2.3 HouseBuilding (Main Scene)
This is the single gameplay scene containing everything:
- **House exterior and interior** (ground floor with a note on grandpa's table)
- **Basement** (below the house, accessed via stairs/door)
- **Underground shaft** (procedural terrain below the basement floor, accessible via winch)
- **First Room** (a discovered underground room with upgrade station and sell station)
- All machines, NPCs, robots, and interactive objects live here.

### 2.4 DemoEndScene
- **Controller:** `DemoEndController` — shows end-of-demo text, returns to main menu.

---

## 3. Core Gameplay Loop

```
Enter Basement → Attach to Winch → Descend into Shaft → Dig Terrain →
Collect Resources (Dust + Items) → Ascend via Winch →
Sell Resources for Credits → Buy Upgrades → Repeat Deeper
```

### 3.1 Progression Pillars
1. **Dig deeper** — each tool type has a max depth limit
2. **Collect resources** — dust (auto-collected) + mineral items (inventory)
3. **Sell resources** — trade terminal (basement) or sell station (first room)
4. **Upgrade** — tools, energy, winch cable, backpack, headlamp, jetpack efficiency
5. **Discover** — treasure chests, hidden rooms, the crystal, the engine

---

## 4. Player Systems

### 4.1 First Person Controller
**File:** `Assets/Scripts/Player/FirstPersonController.cs`
**Namespace:** `BeneathTheFloor.Player`

- Standard WASD + mouse look FPS controller
- `CharacterController`-based (not Rigidbody)
- Walk: 5 m/s | Sprint: 8 m/s (Shift) | Crouch: 2.5 m/s (Ctrl)
- Jump: Space (1.5m height)
- **Crouch headroom check:** `HasHeadroom()` — SphereCast upward before allowing stand-up, prevents camera clipping through terrain
- Gravity: -9.81 m/s² (overridden when winch reeling or jetpack flying)
- Singleton with scene-transition handling
- `CanMove` flag — disabled during UI, cinematics, etc.
- `UIState.ShouldBlockGameplayInput()` blocks movement + look when any UI panel is open

### 4.2 Camera
- Child of `CameraHolder` on the player
- Local Y adjusted during crouch transitions
- `CameraWallAvoidance` — prevents camera from clipping through walls

### 4.3 Jetpack
**File:** `Assets/Scripts/Player/JetpackController.cs`

- Unlocked by picking up `JetpackPickup` in the underground
- Hold Space (0.5s activation delay to allow normal jumps first)
- Flies upward at max 6 m/s with 15 m/s² acceleration
- Consumes energy (15/s base, upgradeable: 15→12→9→5)
- Blocks energy replenishment while flying (+ 1s cooldown after)
- When jetpack is picked up, winch is permanently locked (`WinchExitTrigger.LockWinchPermanently()`)
- Audio: start, loop, stop sounds

### 4.4 Magnet Pull Ability
**File:** `Assets/Scripts/Player/MagnetPullAbility.cs`

- Pulls nearby resource pickups toward the player
- Radius-based detection

### 4.5 Player Headlamp
**File:** `Assets/Scripts/Player/PlayerHeadlamp.cs` + `PlayerHeadlampSetup.cs`

- Spotlight attached to the player camera
- Toggle on/off
- Upgradeable intensity/range via upgrade station

### 4.6 Player Unstuck Failsafe
**File:** `Assets/Scripts/Player/PlayerUnstuckFailsafe.cs`

- Detects if player is stuck inside terrain
- Teleports to last safe position

### 4.7 FP Arms System
**Files:** `FPArmsAutoSetup.cs`, `FPArmsConflictResolver.cs`, `FPArmsRuntimeInitializer.cs`, `PlayerFPArmsController.cs`

- First-person arm models visible when holding tools
- Conflict resolution for multiple arm setups
- Runtime initialization for proper material/layer setup

---

## 5. Digging System

### 5.1 Architecture Overview
**Namespace:** `BeneathTheFloor.Digging`

The digging system uses a voxel-based marching cubes terrain. The player raycasts from the camera, hits terrain, and carves spherical holes.

### 5.2 Core Components

#### DiggingSystem (`Digging/Core/DiggingSystem.cs`)
- Player-facing controller — handles input (left mouse) and raycasting
- Coordinates with `ChunkManager` for voxel modification
- Parameters: digRadius (0.5m), digStrength (0.8), digsPerSecond (2)
- Max dig distance: 4m
- Continuous digging disabled (individual clicks required)
- **Tool gating:** `requireDigTool = true` — must have valid tool equipped
- **Tool depth limits:** Tool 1: 15m, Tool 2: 22m, Tool 3: 30m, Tool 4: 50m
- Audio: random dig hit sounds, blocked sound
- Fires `OnDigCompleted(DigResult)` event

#### DigOperation (`Digging/Core/DigOperation.cs`)
- Data struct for a single dig attempt (position, radius, strength, result)

#### IDiggableTerrain (`Digging/Core/IDiggableTerrain.cs`)
- Interface for any terrain that can be dug

#### DigFeedbackController (`Digging/Core/DigFeedbackController.cs`)
- Visual/audio feedback for dig hits (particles, sounds, screen shake)

#### DigInventoryBridge (`Digging/Core/DigInventoryBridge.cs`)
- Bridges dig results to the inventory system
- Converts dug resources into inventory items

#### DepthManager (`Digging/Core/DepthManager.cs`)
- Tracks current dig depth
- Reports to GameManager

#### DigBoundsProvider (`Digging/Core/DigBoundsProvider.cs`)
- Defines the 3D bounds where digging is allowed

### 5.3 Terrain Chunks

#### ChunkManager (`Digging/Terrain/ChunkManager.cs`)
- Manages the grid of `VoxelChunk` objects
- Handles chunk loading/unloading based on player position

#### VoxelChunk (`Digging/Terrain/VoxelChunk.cs`)
- Individual terrain chunk with 3D voxel density data
- Modified when player digs

#### ChunkMesher (`Digging/Terrain/ChunkMesher.cs`)
- Converts voxel density data to mesh using marching cubes algorithm

#### MarchingCubesTables (`Digging/Terrain/MarchingCubesTables.cs`)
- Lookup tables for the marching cubes algorithm

#### ColliderUpdateScheduler (`Digging/Terrain/ColliderUpdateScheduler.cs`)
- Spreads expensive MeshCollider updates across frames to avoid hitches

#### FloatingTerrainDetector (`Digging/Terrain/FloatingTerrainDetector.cs`)
- Detects terrain chunks that are no longer connected to surrounding terrain

#### FallingTerrainChunk (`Digging/Terrain/FallingTerrainChunk.cs`)
- Handles physics of disconnected terrain falling

#### WorldBootstrapper (`Digging/Terrain/WorldBootstrapper.cs`)
- Initializes the terrain world on scene load

#### ChunkCoord (`Digging/Terrain/ChunkCoord.cs`)
- Struct for chunk grid coordinates

### 5.4 Terrain Layers

#### TerrainLayerType (`Digging/Layers/TerrainLayerType.cs`)
- Enum of terrain types (soil, clay, stone, hard stone, crystal, etc.)

#### TerrainLayerSettings (`Digging/Layers/TerrainLayerSettings.cs`)
- ScriptableObject defining layer depths, colors, hardness

#### TerrainLayerDetector (`Digging/Layers/TerrainLayerDetector.cs`)
- Determines which layer the player is in based on Y position

#### TerrainLayerShaderUpdater (`Digging/Layers/TerrainLayerShaderUpdater.cs`)
- Updates the triplanar terrain shader parameters based on current depth/layer

#### DepthLayerConfig (`Digging/Layers/DepthLayerConfig.cs`)
- Per-layer configuration (color, hardness, resource drops)

### 5.5 Dig Tools

#### DigToolProfile (`Digging/Tools/DigToolProfile.cs`)
- ScriptableObject: defines dig radius, strength, speed for a tool tier

#### DigInputMode (`Digging/Tools/DigInputMode.cs`)
- Enum: Click vs Hold input modes

#### IDigToolProvider (`Digging/Tools/IDigToolProvider.cs`)
- Interface for providing current tool stats to the digging system

#### ToolSystemDigProvider (`Digging/Tools/ToolSystemDigProvider.cs`)
- Implementation that reads from `HeldToolController` and `UpgradeStation`

#### SimpleDigToolProvider (`Digging/Tools/SimpleDigToolProvider.cs`)
- Fallback provider with hardcoded values (for testing)

#### ToolDigProfileLink (`Digging/Tools/ToolDigProfileLink.cs`)
- Links a tool index + tier to a DigToolProfile asset

#### ToolEffectivenessTracker (`Digging/Tools/ToolEffectivenessTracker.cs`)
- Tracks how effective the current tool is at the current depth (affects dig speed)

### 5.6 Dig Feedback

#### DigBlockedFeedback (`Digging/Feedback/DigBlockedFeedback.cs`)
- Visual feedback when digging is blocked (wrong tool, too deep, no energy)

### 5.7 Dig Resources

#### UndergroundResourceTable (`Digging/Resources/UndergroundResourceTable.cs`)
- ScriptableObject defining what resources spawn at what depths

#### UndergroundResourceType (`Digging/Resources/UndergroundResourceType.cs`)
- Enum of all underground resource types

#### ResourceDropConfig (`Digging/Resources/ResourceDropConfig.cs`)
- Configuration for resource drop rates per depth layer

#### DigWorldDropSpawner (`Digging/Resources/DigWorldDropSpawner.cs`)
- Spawns physical resource pickup objects when terrain is dug

#### ResourcePickup (`Digging/Resources/ResourcePickup.cs`)
- Pickup object in the world that the player can collect

#### ResourcePickupInteractor (`Digging/Resources/ResourcePickupInteractor.cs`)
- Handles player-pickup interaction (press E or auto-pickup)

#### ResourceSpawner (`Digging/Resources/ResourceSpawner.cs`)
- Alternative spawner for fixed resource placements

#### IPullablePickup (`Digging/Resources/IPullablePickup.cs`)
- Interface for pickups that can be pulled by magnet ability

### 5.8 Dig Save/Load

#### DiggingSaveManager (`Digging/Save/DiggingSaveManager.cs`)
- Saves and loads terrain voxel state

#### TerrainSaveData (`Digging/Save/TerrainSaveData.cs`)
- Serializable data for terrain state

### 5.9 Terrain Bounds

#### TerrainBoundsPositioner (`Digging/TerrainBoundsPositioner.cs`)
- Positions the terrain bounds volume relative to the shaft

### 5.10 Legacy

#### GuidedPitController (`Digging/Legacy/GuidedPitController.cs`)
- Defines the pit opening area in the basement floor

#### DiggingSystemSwitch (`Digging/Legacy/DiggingSystemSwitch.cs`)
- Legacy compatibility bridge

#### UndergroundTerrainManager (`Digging/Legacy/UndergroundTerrainManager.cs`)
- Legacy terrain manager

---

## 6. Winch System

### 6.1 Overview
**Namespace:** `BeneathTheFloor.Winch`

The winch is the player's lifeline to the surface. A cable connects from the winch anchor (above the pit) down to the player. The player descends by pressing F (reel in), and the cable constrains how far they can go.

### 6.2 Components

#### WinchAnchor (`Winch/WinchAnchor.cs`)
- **Singleton** — lives on the winch anchor object above the dig pit
- Holds the upgrade config, current tier, cable length
- Manages attach/detach state
- `playerAttachOffset`: where cable connects to player
- Events: `OnPlayerAttached`, `OnPlayerDetached`, `OnTierChanged`
- `ReeledCableLength` — how much cable has been reeled in
- `EffectiveCableLength` — max cable length based on current tier
- `ReelIn(amount, minLength)` — shorten cable
- `SetTier(index)` — change winch tier (from upgrades)

#### WinchUpgradeConfig (`Winch/WinchUpgradeConfig.cs`)
- ScriptableObject with 7 tiers defining max cable length, pull speed, etc.
- Asset: `Assets/GameData/Winch/WinchUpgradeConfig.asset`

#### WinchCable (`Winch/WinchCable.cs`)
- **Verlet rope physics simulation** for visual cable rendering
- 40 nodes, 20 solver iterations, gravity=18, damping=0.15
- Rest length = straight-line distance × 1.06 (6% sag), capped at EffectiveCableLength
- Simple downward raycast ground collision (`KeepAboveGround`)
- Ignores "Ceiling" prefixed objects
- Tension-based color lerp (dark → red)
- LineRenderer-based rendering

#### WinchMotor (`Winch/WinchMotor.cs`)
- **Lives on the player** — handles all cable-player physics
- **Pull mode** (F key held): reels in cable, moves player along cable path toward anchor using `CharacterController.Move()`
- **Constraint mode** (F released): prevents player from exceeding cable length
- **Anchor push-down**: pushes player downward when near the winch anchor (radius 3m, speed 4 m/s)
- **Docked state**: player has reached minimum cable length (at the top)
- Static event: `OnCableLimitReached` — fired when player hits max cable length
- `Instance` singleton for mission system access

#### PlayerWinchAttachment (`Winch/PlayerWinchAttachment.cs`)
- Handles pit detection (enter/exit) and cable attach/detach
- Uses `WinchPitTrigger` and `WinchExitTrigger`

#### WinchPitTrigger (`Winch/WinchPitTrigger.cs`)
- Trigger collider at the pit entrance — marks the player as entering the winch zone

#### WinchExitTrigger (`Winch/WinchExitTrigger.cs`)
- Trigger at the exit — detaches cable, can permanently lock winch (when jetpack is found)
- Static method `LockWinchPermanently()` — disables winch forever after jetpack pickup
- Static method `ResetWinchLock()` — called on New Game

#### WinchDrumAnimator (`Winch/WinchDrumAnimator.cs`)
- Rotates the physical drum model based on cable reel state

#### WinchHUD (`Winch/WinchHUD.cs`)
- UI overlay showing cable length remaining, tension bar
- References `WinchCable` and `WinchMotor`

---

## 7. Tool System

### 7.1 Overview
**Namespace:** `BeneathTheFloor.Tools`

The game has 4 tool types, each with 4 tiers (Base, Tier 1, Tier 2, Tier 3).

### 7.2 Tool Types

| # | Tool | Max Depth | Unlocked By |
|---|------|-----------|-------------|
| 1 | Shovel | 15m | Starting tool |
| 2 | Heavy Spade | 22m | Found/unlocked |
| 3 | Pickaxe | 30m | Found/unlocked |
| 4 | Drill Pike | 50m | Pickup in deep underground |

Each tool has 4 visual tiers (Base → Tier 3) with different 3D models. Tiers affect dig speed and radius but NOT max depth (depth is per-tool-type).

### 7.3 HeldToolController (`Tools/HeldToolController.cs`)
- Manages 4×4 = 16 tool GameObjects (4 types × 4 tiers)
- Shows/hides the correct visual based on current tool + tier
- Overlay camera system to prevent terrain clipping
- `SetToolAndTier(toolIndex, tier)` — called by upgrade system
- `ShowCurrentTool()` / `HideAllTools()`

### 7.4 Tool Profiles
**Assets:** `Assets/GameData/ToolProfiles/`
- `BaseShovel.asset`, `Tier1_Shovel.asset`, `Tier2_Shovel.asset`, `Tier3_Shovel.asset`
- `HeavySpade_Base.asset` through `HeavySpade_Tier3.asset`
- `Tier2_Pickaxe.asset`, `Tier3_IronPickaxe.asset`
- `DrillPike_Base.asset` through `DrillPike_Tier3.asset`

### 7.5 Radar Tool (`Tools/RadarTool.cs`)
- EMF/radar device held in the left hand
- Hold Q to activate
- Needle rotates based on proximity/direction to nearest hidden resource node or treasure chest
- Detection range: 25m, perfect aim angle: 15°, max angle: 90°
- 5-level needle response (1=no detection, 5=perfect aim)
- Screen light for visibility in dark areas
- Unlocked via `RadarPickup` in the underground

### 7.6 Other Tool Scripts
- `RadarPointer.cs` — visual pointer component on the radar
- `RadarBobAnimation.cs` — subtle bobbing animation
- `RadarPickup.cs` — world pickup that gives player the radar
- `RadarTarget.cs` — component on objects the radar can detect
- `DrillPikePickup.cs` — world pickup for the Drill Pike (tool 4)
- `ToolVisual.cs` — manages visual representation of a tool

---

## 8. Resource System

### 8.1 Overview
**Namespace:** `BeneathTheFloor.ResourceSystem`

Two parallel resource economies:
1. **Dust** — auto-accumulated from digging, sold for credits (separate counter, not in inventory)
2. **Items** — physical pickups (ores, crystals, etc.) stored in inventory, sold for credits

### 8.2 DustManager (`ResourceSystem/DustManager.cs`)
- Singleton, `DontDestroyOnLoad`
- Accumulates dust from each dig operation based on depth
- `GetDust()` / `AddDust(amount)` / `SpendDust(amount)`
- Sold at TradeTerminal or FirstRoomSellStation for credits
- Events: `OnDustChanged`, `OnDustSold`

### 8.3 Hidden Node System

#### ResourceSystemConfig (`ResourceSystem/ResourceSystemConfig.cs`)
- ScriptableObject defining node spawn rules, chunk sizes, density

#### HiddenNodeManager (`ResourceSystem/HiddenNodeManager.cs`)
- **Lazy instantiation** — nodes are data-only until revealed by digging
- Spawns nodes inside shaft walls based on depth
- Regular nodes (chunk-based) + bonus nodes in top layer
- Events: `OnNodeRevealed`
- Integrates with shaft bounds from `ModularShaftWallManager`

#### HiddenNode (`ResourceSystem/HiddenNode.cs`)
- Individual hidden resource node embedded in terrain
- Has `CurrentExposure` — revealed gradually as terrain is dug away
- Becomes interactable when exposed enough

#### FixedResourceNode (`ResourceSystem/FixedResourceNode.cs`)
- Pre-placed resource node (not procedural)

#### NodePickup (`ResourceSystem/NodePickup.cs`)
- Physical pickup spawned when a node is fully revealed

#### NodeVisualMarker (`ResourceSystem/NodeVisualMarker.cs`)
- Visual indicator for partially revealed nodes

#### InteractionForwarder (`ResourceSystem/InteractionForwarder.cs`)
- Forwards interaction events from child colliders to parent node

#### ProximityCuller (`ResourceSystem/ProximityCuller.cs`)
- Performance optimization — hides distant node visuals

#### ResourceSaveManager (`ResourceSystem/ResourceSaveManager.cs`)
- Saves/loads resource node states

#### ResourceSystemSetup (`ResourceSystem/ResourceSystemSetup.cs`)
- Runtime initialization of the resource system

### 8.4 DustHUD (`ResourceSystem/DustHUD.cs`)
- UI showing current dust amount

---

## 9. Inventory System

### 9.1 Overview
**Namespace:** `BeneathTheFloor.Inventory`

Grid-based inventory with stacking.

### 9.2 InventorySystem (`Inventory/InventorySystem.cs`)
- Singleton
- **Initial slots:** 5 (upgradeable to 10)
- **Stack size:** starts at 1, upgradeable up to 6
- Upgrade path: Level 0=5 slots/1 stack → Level 1=10 slots → Level 2-6=stack size 2-6
- `AddItem(ItemSO, amount)` → returns remaining if full
- `RemoveItem(ItemSO, amount)`
- `GetAllSlots()` → list of `ItemStack`
- Events: `OnItemAdded`, `OnInventoryChanged`, `OnInventoryFull`
- Resource-to-Item mapping: converts `ResourceType` enum to `ItemSO` assets

### 9.3 ItemSO (`Crafting/ItemSO.cs`)
- Base ScriptableObject for all items: name, icon, description, sell value, max stack, category

### 9.4 MaterialItemSO (`Crafting/MaterialItemSO.cs`)
- Extension of ItemSO for raw materials

### 9.5 ToolItemSO (`Crafting/ToolItemSO.cs`)
- Extension of ItemSO for tools

### 9.6 Generated Resource Items
**Path:** `Assets/Items/GeneratedResources/`

All mined resources as ItemSO assets:
- Dirt, Stone, Clay, Coal, Sandstone, SoftStone, HardSoil, HardSoil_Deep
- CopperFragment, CopperPiece, IronNugget, IronChunk
- Quartz, PurpleQuartz, CrystalDust, CrystalShard, CrystalStone
- CrystalCoreFragment, CoreCrystalChunk, DeepCrystalVein
- LuminousDust, Heatstone, HardStone, DeepBlackStone
- BlueGreyOre, AncientOre, RareMachineParts, Gold

### 9.7 Inventory UI
**Namespace:** `BeneathTheFloor.InventoryUI`

- `InventoryUIManager` — creates and manages the grid of slots
- `InventorySlotUI` — individual slot rendering (icon, count, highlight)
- `InventoryDragController` — drag-and-drop between slots
- `InventoryTooltip` — hover tooltip showing item details
- `InventoryTrashSlot` — trash/delete slot
- `InventoryUI` (`UI/InventoryUI.cs`) — toggle panel open/close (Tab key)

---

## 10. Economy System

### 10.1 Overview
**Namespace:** `BeneathTheFloor.Economy`

Credits are the universal currency. Earned by selling resources (dust and items).

### 10.2 CurrencyManager (`Economy/CurrencyManager.cs`)
- Singleton tracking credit balance
- `AddCredits(amount)` / `SpendCredits(amount)` / `CanAfford(amount)`

### 10.3 CurrencyHUD (`Economy/CurrencyHUD.cs`)
- Displays current credits on screen

### 10.4 Trade Terminal (Basement)
- `TradeTerminal.cs` — interactable machine in the basement
- `TradeTerminalUI.cs` — sell UI showing inventory items with values
- `TradeTerminalClickDebug.cs` — debug helper
- Player walks up, presses E, sells items for credits

### 10.5 First Room Sell Station (Underground)
- `FirstRoomSellStation.cs` — sell station in the discovered underground room
- `FirstRoomSellStationUI.cs` — similar to trade terminal UI but sci-fi styled
- **Requires engine activation** (crystal inserted) to function

### 10.6 Sell Service
- `InventorySellService.cs` — shared logic for selling items/dust from any sell point
- Fires `GameEvents.OnItemsSold` and `GameEvents.OnDustSold`

### 10.7 Bag Value HUD
- `BagValueHUD.cs` — shows estimated value of items in inventory

### 10.8 Estimated Value HUD
- `EstimatedValueHUD.cs` — shows predicted sell value

### 10.9 Economy Setup
- `EconomySetup.cs` — initializes economy on scene load

---

## 11. Energy System

### 11.1 Overview
**Namespace:** `BeneathTheFloor.Energy`

Energy is consumed by digging and jetpack flying. It regenerates over time.

### 11.2 EnergyManager (`Energy/EnergyManager.cs`)
- Singleton
- **Base max:** 100 | **Per upgrade:** +25 max, +2 regen/s | **Max upgrade level:** 7
- **Base regen:** 4/s (only after 1s cooldown since last dig)
- **Dig cost per tool:** Tool 1: 8, Tool 2: 6, Tool 3: 4
- **Energy drinks:** consumable items (R key), cost 20 credits, max 5 carried
- `ConsumeEnergy(amount)` / `AddEnergy(amount)`
- Jetpack blocks energy replenishment while flying
- Events: `OnEnergyChanged`, `OnEnergyDepleted`, `OnLowEnergy`, `OnDrinkCountChanged`

### 11.3 EnergyUI (`Energy/EnergyUI.cs`)
- Energy bar visualization

### 11.4 EnergySource (`Energy/EnergySource.cs`)
- Component for objects that provide energy (generators)

### 11.5 EnergyConsumer (`Energy/EnergyConsumer.cs`)
- Component for objects that consume energy

---

## 12. Upgrade System

### 12.1 Overview
**Namespace:** `BeneathTheFloor.Machines`

Upgrades are purchased with credits at Upgrade Stations.

### 12.2 UpgradeStation (`Machines/UpgradeStation.cs`)
- Interactable machine (implements `IInteractable`)
- Contains `RuntimeUpgrade` list — in-memory upgrade definitions
- Creates default upgrades if `createDefaultUpgrades = true`
- Visual feedback: lights, particles, sounds
- Events: `OnRuntimeUpgradeApplied`
- `GetRuntimeUpgradeLevel(upgradeId)` — query current level

### 12.3 UpgradeStationUI (`Machines/UpgradeStationUI.cs`)
- UI panel showing available upgrades with costs and levels
- Can lock upgrades except winch (for mission 7)

### 12.4 RuntimeUpgrade IDs and Effects

| Upgrade ID | Effect | Levels |
|------------|--------|--------|
| `tool_power` | Increases dig tool tier | Multiple |
| `energy_capacity` | +25 max energy, +2 regen/s per level | 7 |
| `winch_cable` | Increases winch cable max length | 7 tiers |
| `headlamp` | Improves headlamp brightness/range | Multiple |
| `backpack` | L1: 5→10 slots, L2-6: stack size 1→6 | 6 |
| `jetpack_efficiency` | Reduces jetpack energy drain | 3 |

### 12.5 First Room Upgrade Station
- `FirstRoomUpgradeStation.cs` + `FirstRoomUpgradeStationUI.cs`
- Alternative upgrade station in the underground first room

### 12.6 Legacy Upgrade System
- `UpgradeSystem.cs` (`Upgrades/UpgradeSystem.cs`) — older tier-based tool upgrade system
- `UpgradeTree.cs` (`Crafting/UpgradeTree.cs`) — tree-based upgrade definitions

---

## 13. Machine System

### 13.1 Overview
**Namespace:** `BeneathTheFloor.Machines`

Machines are interactable objects that provide services.

### 13.2 Components
- `MachineSetup.cs` — runtime initialization
- `BasementMachineSetup.cs` — places machines in the basement
- `MachinePrefabBuilder.cs` / `AssetMachinePrefabBuilder.cs` — creates machine prefabs
- `MachineVisualFeedback.cs` — lights, particles, animations
- `MachineAudio.cs` (`Audio/MachineAudio.cs`) — machine sound effects

### 13.3 Machine Types
1. **Upgrade Station** — buy upgrades (see §12)
2. **Refinery** — `Refinery.cs` + `RefineryUI.cs` — converts raw materials into refined ones
3. **Energy Generator** — `EnergyGenerator.cs` — generates energy over time
4. **Trade Terminal** — sell resources (see §10.4)
5. **First Room Sell Station** — underground sell point (see §10.5)

---

## 14. Mission System

### 14.1 Overview
**Namespace:** `BeneathTheFloor.Missions`

Linear mission chain that guides the player through the game. Each mission is a `MissionData` ScriptableObject.

### 14.2 MissionManager (`Missions/MissionManager.cs`)
- Singleton
- Holds ordered list of `MissionData` assets
- Auto-starts first mission, chains to next on completion
- Subscribes to ~20 different game events for trigger detection
- Marker system: placed markers (Inspector-linked) or floating fallback markers
- Events: `OnMissionStarted`, `OnMissionCompleted`, `OnAllMissionsCompleted`

### 14.3 MissionData (`Missions/MissionData.cs`)
- ScriptableObject with fields for every possible mission configuration
- Key fields: `missionId`, `missionName`, `objectiveText`, `completionTrigger`

### 14.4 Mission Trigger Types
| Trigger | Description |
|---------|-------------|
| `NoteRead` | Player reads and closes a note |
| `LocationReached` | Player reaches a position |
| `FirstDig` | Player digs (optionally N times) |
| `NodeRevealed` | Hidden resource node exposed |
| `ResourceCollected` | Resource added to inventory (optionally N items) |
| `ItemsSold` | Player sells at terminal |
| `WinchUpgraded` | Player upgrades winch cable |
| `PastPreviousCableLimit` | Player descends past old cable limit |
| `RadarPickedUp` | Player picks up radar |
| `RadarActivated` | Player uses radar for required duration |
| `AnyUpgradePurchased` | Player buys any upgrade |
| `TreasureChestOpened` | Player opens a treasure chest |
| `DepthReached` | Player reaches specific Y depth |
| `RoomEntranceFound` | Player enters a hidden room |
| `CrystalInserted` | Crystal placed in engine |
| `JetpackPickedUp` | Jetpack found |
| `DrillPikePickedUp` | Drill Pike found |

### 14.5 Mission Sequence (13 missions)
**Assets:** `Assets/GameData/Missions/`

| # | File | Name | Trigger |
|---|------|------|---------|
| 1 | `Mission_01_FindGrandpasTable.asset` | Find Grandpa's Table | NoteRead |
| 2 | `Mission_02_StartDigging.asset` | Start Digging | FirstDig |
| 3 | `Mission_03_CollectDust.asset` | Collect Dust | ResourceCollected |
| 4 | `Mission_04_SellResources.asset` | Sell Resources | ItemsSold |
| 5 | `Mission_05_FirstUpgrade.asset` | First Upgrade | AnyUpgradePurchased |
| 6 | `Mission_06_FindFirstTreasure.asset` | Find First Treasure | TreasureChestOpened |
| 7 | `Mission_07_UpgradeWinch.asset` | Upgrade Winch | WinchUpgraded |
| 8 | `Mission_08_KeepExploring.asset` | Keep Exploring | PastPreviousCableLimit / DepthReached |
| 9 | `Mission_09_SecretRooms.asset` | Secret Rooms | RoomEntranceFound |
| 10 | `Mission_10_TheCrystal.asset` | The Crystal | LocationReached |
| 11 | `Mission_11_ConnectCrystal.asset` | Connect Crystal | CrystalInserted |
| 12 | `Mission_12_NewTechnology.asset` | New Technology | JetpackPickedUp |
| 13 | `Mission_13_DrillPike.asset` | Drill Pike | DrillPikePickedUp |

### 14.6 Mission UI
- `MissionUI.cs` — shows objective text, help text, crosshair hints, counters, completion toasts, centered popups
- `MissionMarkerVisual.cs` — diamond + beam marker above targets
- `MissionMarkerPoint.cs` — marker attachment point
- `FloatingObjectiveMarker.cs` — floating diamond marker (fallback)
- `SubtleRingMarker.cs` — subtle ring marker (for less intrusive guidance)
- `RingMarkerTarget.cs` — component that marks objects as ring marker targets

### 14.7 Mission Support
- `DigAreaMarker.cs` — marks dig areas for missions
- `RoomEntranceTrigger.cs` — trigger for room discovery
- `CrystalInteractable.cs` — crystal pickup/insert interaction

---

## 15. Treasure Chests

### 15.1 Overview
**Namespace:** `BeneathTheFloor.TreasureChests`

Buried treasure chests are hidden in the terrain and can be discovered by digging and using the radar.

### 15.2 Components
- `TreasureChestManager.cs` — singleton, manages all chests, radar integration, save/load
- `BuriedTreasureChest.cs` — individual chest with rewards, exposure tracking
- `TreasureChestReward.cs` — reward data (currency, notes, items)
- `TreasureChestRewardUI.cs` — UI popup showing rewards when chest is opened
- `TreasureChestRadarTarget.cs` — makes chests detectable by radar

### 15.3 Reward Assets
**Path:** `Assets/GameData/TreasureChests/`
- `CurrencyReward_Small.asset`, `CurrencyReward_Medium.asset`, `CurrencyReward_Large.asset`
- `NoteReward_Hint1.asset`, `NoteReward_Hint2.asset`

---

## 16. Robot Systems

### 16.1 Digger Robot
**Namespace:** `BeneathTheFloor.Robot`

An autonomous digging robot that follows waypoints and digs terrain.

- `DiggerRobotStateMachine.cs` — FSM: Docked → Digging → Returning → Shutdown → Carried → Recharging
  - Sub-states: MovingToSite, ActiveDig, Sweeping, Reversing
- `DiggerRobotConfig.cs` — ScriptableObject config (speed, dig rate, battery)
- `DiggerRobotBattery.cs` — battery management with drain and recharge
- `DiggerRobotBreadcrumbs.cs` — breadcrumb path for return navigation
- `DiggerRobotCarry.cs` — allows player to pick up and carry the robot
- `DiggerRobotDock.cs` — docking station for recharging
- `DiggerRobotVisual.cs` — visual animation and effects
- `DiggerRobotHUD.cs` — battery/status HUD above robot
- `DiggerRobotMarker.cs` — map/radar marker
- `RobotWaypointPath.cs` — waypoint path definition
- `RobotContainmentZone.cs` — defines area the robot operates in
- `RobotGate.cs` — gate that opens/closes for robot passage

### 16.2 Logistics Robot
**Namespace:** `BeneathTheFloor.Logistics`

A helper robot that collects resources and delivers them.

- `LogisticsRobotController.cs` — FSM: Docked, Idle, FollowPlayer, WorkWithDigger, CollectingPickup, TravelToUnload, Unloading, TravelToRecharge, Recharging, FetchingItem, DeliveringItem, Carried, Shutdown
- `LogisticsRobotConfig.cs` — ScriptableObject config
- `LogisticsBattery.cs` — battery management
- `LogisticsRobotCarry.cs` — carry by player
- `LogisticsArmAnimator.cs` — arm pickup animation
- `LootScanner.cs` — scans for nearby pickups
- `RobotCargoBuffer.cs` — temporary storage while collecting
- `LogisticsRobotDock.cs` — docking/charging station
- `DropOffContainer.cs` + `DropOffContainerUI.cs` — container where robot deposits items
- `LogisticsRobotHUD.cs` — status HUD
- `LogisticsRobotMarker.cs` — map marker
- `LogisticsRobotPopupUI.cs` — interaction popup
- `RobotCommandUI.cs` — command interface for the robot

### 16.3 Adapter Interfaces
- `IPickupAdapter.cs` — adapter for different pickup types
- `IPlayerInventoryAdapter.cs` — adapter for inventory access
- `ITradeTerminalVendor.cs` — adapter for sell functionality
- `NodePickupAdapter.cs`, `ResourcePickupAdapter.cs`, `PlayerInventoryAdapter.cs`, `TradeTerminalVendorAdapter.cs`

---

## 17. Lighting System

### 17.1 Overview
**Namespace:** `BeneathTheFloor.Lighting`

The underground is dark. Multiple lighting systems create atmosphere.

### 17.2 Components
- `BasementLightingManager.cs` — manages basement ambient lighting
- `DiggingLightingRig.cs` — dynamic lighting that follows the player underground
- `UndergroundLightingSystem.cs` — overall underground lighting management
- `CeilingSpotlight.cs` — spotlight mounted on ceiling
- `WallLamp.cs` — lamp attached to shaft walls
- `PlaceableLamp.cs` — player-placeable lamp
- `LampPlacementController.cs` — handles lamp placement input
- `GlowingResource.cs` — resources that emit light
- `CrystalGlow.cs` — crystal objects that glow
- `CoreShardPickup.cs` — pickup for glowing core shards

---

## 18. World & Environment

### 18.1 Shaft System
**Namespace:** `BeneathTheFloor.WorldRooms`

- `ModularShaftConfig.cs` — ScriptableObject defining shaft dimensions and segment types
- `ModularShaftWallManager.cs` — builds and manages the shaft walls procedurally
- `ShaftWallSegmentConfig.cs` — configuration per wall segment type
- `DigShaftCenterMarker.cs` — marks the center of the dig shaft

**Shaft Segment Assets:** `Assets/GameData/Shaft/`
- `ModularShaftConfig.asset` — main config
- `ShaftSegment_4m.asset`, `ShaftSegment_6m.asset`, `ShaftSegment_8m.asset`, `ShaftSegment_10m.asset`

### 18.2 Environment
- `BasementResizer.cs` — adjusts basement geometry
- `BasementDigOpeningSetup.cs` — creates the opening in the basement floor
- `BasementRuntimeSnapshot.cs` — captures basement state at runtime

### 18.3 Underground Room
- `AncientRuinsGenerator.cs` — procedural generation of ancient ruins room

### 18.4 World Items
**Namespace:** `BeneathTheFloor.World`
- `WorldItem.cs` — base class for items in the world
- `WorldDropManager.cs` — manages dropped items in the world
- `BillboardSprite.cs` — sprites that always face the camera
- `EngineActivationInteract.cs` — the ancient engine where the crystal is inserted
- `EngineRadarTarget.cs` — makes the engine detectable by radar

---

## 19. Interaction System

### 19.1 Overview
**Namespace:** `BeneathTheFloor.Interaction`

Raycast-based interaction with objects in the world.

### 19.2 Components
- `InteractionSystem.cs` — raycasts from camera, detects `IInteractable` objects, shows prompts, handles E key
- `IInteractable` interface — implemented by all interactable objects
- `StoryItemPickup.cs` — pickup for story items (notes, keys, etc.)
- `CrackedWall.cs` — breakable wall that reveals hidden areas
- `SceneTransition.cs` — trigger that transitions between scenes (house ↔ basement)

### 19.3 Highlight System
**Namespace:** `BeneathTheFloor.Highlight`
- `HighlightOutline.cs` — adds outline effect to hovered objects
- `InteractionHighlighter.cs` — manages highlight state

---

## 20. Audio System

### 20.1 Overview
**Namespace:** `BeneathTheFloor.Audio` (partial, some in root)

### 20.2 Components
- `AudioManager.cs` — central audio management, sound pools
- `FootstepAudio.cs` — surface-based footstep sounds
- `MachineAudio.cs` — machine operational sounds
- `AmbientAudioZoneManager.cs` — manages ambient audio zones (surface vs underground)
- `PlayerIdleSounds.cs` — subtle sounds when player is idle

---

## 21. Save System

### 21.1 Overview
**Namespace:** `BeneathTheFloor.Save`

JSON-based save system with multiple slots.

### 21.2 Components
- `SaveManager.cs` — singleton, coordinates all save/load, auto-save every 5 minutes
- `GameSaveData.cs` — master save data container
- `SavesIndex.cs` — index of all save slots + active slot
- `SaveSlotInfo.cs` — metadata for a save slot (name, date, playtime)
- `SaveSlotUI.cs` — UI representation of a save slot
- `SaveLoadMenuUI.cs` — save/load menu with slot list
- `WorldPickupSaveManager.cs` — saves/loads world pickup states

### 21.3 What Gets Saved
- Player position, tool index, tool tier
- Inventory contents
- Currency balance
- Dust amount
- Energy level and upgrade levels
- Winch tier
- Terrain voxel state (dig progress)
- Mission progress
- Treasure chest opened states
- Hidden node states
- Resource node states

### 21.4 Keybinds
- F5: Quick save (debug only)
- F9: Quick load (debug only)

---

## 22. UI System

### 22.1 Overview
**Namespace:** `BeneathTheFloor.UI`

All UI is built at runtime using `UIBuilder`.

### 22.2 Core Components
- `UIManager.cs` — manages all UI panels
- `UIBuilder.cs` — utility for building UI elements programmatically
- `UIState.cs` — static class tracking which UIs are open, `ShouldBlockGameplayInput()`
- `UIFXManager.cs` — UI visual effects (flashes, shakes)
- `HUDController.cs` — manages HUD element visibility

### 22.3 HUD Elements
- `DepthHUD_YBased.cs` + `DepthHUDSetup.cs` — shows current depth based on Y position
- `CurrencyHUD.cs` — credit display
- `DustHUD.cs` — dust amount display
- `BagValueHUD.cs` — estimated bag value
- `EstimatedValueHUD.cs` — estimated sell value
- `EnergyUI.cs` — energy bar
- `ConsumablesHUD.cs` — energy drinks count
- `WinchHUD.cs` — cable length/tension
- `ControlsHintUI.cs` — shows control hints

### 22.4 Popup/Notification
- `PickupNotificationSystem.cs` — shows item pickup notifications
- `StoryPopupUI.cs` — large story text popups
- `FloatingWorldText.cs` — floating damage/pickup numbers
- `ObjectiveMarker.cs` — 3D marker above objectives

### 22.5 Menu
- `PauseMenu.cs` — ESC to pause, resume/save/settings/quit
- `SettingsUI.cs` — graphics, audio, controls settings

---

## 23. Game Flow

### 23.1 Namespace
`BeneathTheFloor.GameFlow`

### 23.2 Components
- `PressAnyKeyController.cs` — boot screen
- `MainMenuController.cs` — main menu logic
- `MainMenuVideoBackground.cs` — video player for menu background
- `IntroCinematicController.cs` — intro text sequence ("My grandfather said he could hear it...")
- `DemoEndController.cs` — end-of-demo screen
- `PauseMenuController.cs` — pause menu logic
- `ReadableNote.cs` — note that player can read (grandpa's notes)
- `NoteUIController.cs` — full-screen note reading UI
- `ObjectiveUIController.cs` — objective panel
- `FloatingObjectiveMarker.cs` — floating diamond marker

### 23.3 Intro Cinematic Lines
1. "My grandfather said he could hear it."
2. "A sound... a pull... something calling from beneath the ground."
3. "He started digging. He never reached it."
4. "Now it's my turn."

---

## 24. Story System

### 24.1 Overview
**Namespace:** `BeneathTheFloor.Story`

- `StoryManager.cs` — tracks story progression, items found
- `StoryFXManager.cs` — visual effects for story moments (screen flashes, atmospheric changes)

### 24.2 Story Items
Defined in `GameEvents.cs`:
- `StoryItem` class: itemId, title, description, icon, depthFound, isCollected

---

## 25. NPC System

### 25.1 Overview
**Namespace:** `BeneathTheFloor.NPC`

- `NPCBase.cs` — base class for NPCs with interaction
- `DialogueUI.cs` — dialogue display panel

---

## 26. Crafting System

### 26.1 Overview
**Namespace:** `BeneathTheFloor.Crafting`

- `CraftingManager.cs` — manages available recipes and crafting operations
- `CraftingRecipe.cs` — defines input materials → output item

### 26.2 Item Types
- `ItemSO.cs` — base item ScriptableObject
- `MaterialItemSO.cs` — raw/refined materials
- `ToolItemSO.cs` — tool items

---

## 27. Visual Effects

### 27.1 Components
- `ScreenFlicker.cs` (`Visuals/`) — screen flicker effect for atmosphere
- `MachineVisualFeedback.cs` — machine glow, particles
- `DigFeedbackController.cs` — dig particles, screen shake
- `PlayerMovementFX.cs` — movement-related effects (head bob, landing impact)

---

## 28. Debug System

### 28.1 Components
- `DebugHUDSpawner.cs` — spawns debug HUD overlay
- `PlayerUpgradeDebugHUD.cs` — shows all upgrade levels
- `DebugCreditsUI.cs` — add/remove credits for testing
- `DebugResourceSpawner.cs` — spawn resources manually
- `MissionDebugSkip.cs` — skip current mission
- `DiggingDebugHUD.cs` (`Digging/Debug/`) — digging system debug info

---

## 29. Manager / Core

### 29.1 GameManager (`Managers/GameManager.cs`)
- Singleton, `DontDestroyOnLoad`
- Handles scene loading, pausing, player spawning
- Tracks depth milestones (10, 25, 40, 50)
- Initializes graphics to highest quality + native resolution
- Scene spawn: finds named spawn points or falls back to hardcoded positions
- `NewGame()` — resets all data, loads house scene
- `SaveGame()` — delegates to SaveManager
- `QuitGame()` — saves then quits

### 29.2 GameEvents (`Managers/GameEvents.cs`)
- Static event bus for all game-wide events
- Categories: Wall, Resource, Tool, Story, UI, Inventory, Sell, GameState, Crafting, Machine, Energy, Digging, Visual Feedback

### 29.3 SpawnPointManager / SceneSpawnSetup
- `SpawnPointManager.cs` — manages named spawn points
- `SceneSpawnSetup.cs` — configures spawn points per scene

---

## 30. GameData Assets

### 30.1 Shaft Configuration
`Assets/GameData/Shaft/`
- `ModularShaftConfig.asset` — shaft dimensions, wall materials, segment list
- `ShaftSegment_4m.asset` to `ShaftSegment_10m.asset` — segment height variants

### 30.2 Winch Configuration
`Assets/GameData/Winch/`
- `WinchUpgradeConfig.asset` — 7 winch tiers with cable lengths and speeds

### 30.3 Tool Profiles
`Assets/GameData/ToolProfiles/`
- 4 tool types × 4 tiers = 16 profiles (BaseShovel, Tier1-3_Shovel, HeavySpade_Base-Tier3, etc.)

### 30.4 Mission Data
`Assets/GameData/Missions/`
- 13 mission assets (Mission_01 through Mission_13)

### 30.5 Treasure Chests
`Assets/GameData/TreasureChests/`
- 3 currency rewards + 2 note rewards

### 30.6 Robot Configs
- `DiggerRobotConfig.asset`
- `LogisticsRobotConfig.asset`

### 30.7 Resource Table
- `UndergroundResourceTable.asset` — depth-based resource spawn rules

---

## 31. Controls

| Key | Action |
|-----|--------|
| WASD | Move |
| Mouse | Look |
| Left Click | Dig (when tool equipped, pointing at terrain) |
| E | Interact (pick up, open, activate) |
| F | Winch pull (hold to reel up) |
| Tab | Toggle inventory |
| Shift | Sprint |
| Ctrl | Toggle crouch |
| Space | Jump / Jetpack (hold 0.5s) |
| Q | Hold to use radar |
| R | Use energy drink |
| ESC | Pause menu |
| F5 | Quick save (debug) |
| F9 | Quick load (debug) |

---

## 32. Technical Notes

### 32.1 Assembly Definitions
- `BeneathTheFloor.asmdef` — main runtime assembly
- `BeneathTheFloor.Editor.asmdef` — editor scripts

### 32.2 Rendering
- Universal Render Pipeline (URP)
- Three quality profiles: Performant, Balanced, High Fidelity
- Triplanar terrain shader (`Assets/Shaders/TriplanarSoil.shader`)
- Overlay camera for held tools (prevents terrain clipping)

### 32.3 Singleton Pattern
Most managers use a simple singleton pattern:
```csharp
public static T Instance { get; private set; }
private void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }
```
Some use `DontDestroyOnLoad` for cross-scene persistence (GameManager, SaveManager, DustManager, TreasureChestManager).

### 32.4 Event Architecture
- `GameEvents` static class — legacy `UnityAction` events
- Per-system `Action<T>` events on manager instances
- Static events on specific classes (e.g., `WinchMotor.OnCableLimitReached`, `JetpackPickup.OnJetpackPickedUp`)

### 32.5 Art Assets
- `Assets/Art/` — tools, resources, UI, notes, machinery, treasure chests, first room
- `Assets/MeshyImports/` — AI-generated 3D models (Meshy)
- `Assets/Lathiel/` — EMF/radar model
- `Assets/Abandoned World/` — environmental art pack
- `Assets/Modular Industrial Interior/` — industrial interior pieces
- `Assets/1underground_room/` — underground room art
- `Assets/Atmospheric House/` (referenced in code) — house building assets
