# MIGRATION PLAN: Under the Floor → Beneath the Floor
# Unified Digging System Migration

## OVERVIEW

### Current State:
- Game Name: "Under the Floor"
- Namespace: `UnderTheFloor`
- Two parallel digging systems: DiggingV2 (44 files) and DiggingV3 (26 files)
- 307 files with `namespace UnderTheFloor`
- 245 `using UnderTheFloor` statements

### Target State:
- Game Name: "Beneath the Floor"
- Namespace: `BeneathTheFloor`
- ONE unified digging system: `BeneathTheFloor.Digging` (no V2/V3)
- Clean, simple folder structure

---

## FOLDER STRUCTURE CHANGES

### Before:
```
Assets/Scripts/
├── DiggingV2/           (44 files - DELETE most)
├── DiggingV3/           (26 files - KEEP/RENAME)
├── [other folders with UnderTheFloor namespace]
```

### After:
```
Assets/Scripts/
├── Digging/             (Unified system ~30 files)
│   ├── Core/            (Main system files)
│   ├── Terrain/         (Chunk management)
│   ├── Resources/       (Drops, pickups, loot)
│   ├── Tools/           (Tool integration)
│   ├── Layers/          (Terrain layers)
│   ├── Save/            (Persistence)
│   └── Debug/           (Debug tools)
├── [other folders with BeneathTheFloor namespace]
```

---

## DIGGINGV2 FILES - DISPOSITION

### MOVE TO UNIFIED SYSTEM (14 files):
These provide functionality V3 doesn't have:

| V2 File | New Location | Why Keep |
|---------|--------------|----------|
| `UndergroundResourceType.cs` | Digging/Resources/ | Shared enum used everywhere |
| `DigToolProfile.cs` | Digging/Tools/ | Tool config ScriptableObject |
| `DepthManager.cs` | Digging/Core/ | Depth calculation singleton |
| `DigInventoryBridge.cs` | Digging/Resources/ | Maps resources to inventory |
| `UndergroundResourceTable.cs` | Digging/Resources/ | Loot distribution by depth |
| `ResourceDropConfig.cs` | Digging/Resources/ | Drop visual properties |
| `ResourcePickup.cs` | Digging/Resources/ | Pickup component |
| `ResourcePickupInteractor.cs` | Digging/Resources/ | Proximity pickup |
| `PopAndFreezeResource.cs` | Digging/Resources/ | Physics animation |
| `DigFeedbackController.cs` | Digging/Core/ | Audio/particles/shake |
| `RectShaftGenerator.cs` | Digging/Terrain/ | Shaft wall generation |
| `IPullablePickup.cs` | Digging/Resources/ | Interface |
| `LayerLootSelector.cs` | Digging/Resources/ | Loot rolling logic |
| `DigResult.cs` | Digging/Core/ | Dig result data (merge with V3) |

### DELETE (30 files):
Replaced by V3 equivalents or no longer needed:

| V2 File | Reason |
|---------|--------|
| `DiggingSystemV2.cs` | → DiggingSystem.cs (V3) |
| `UndergroundTerrainManager.cs` | → ChunkManager.cs (V3) |
| `DepthLayeredTerrain.cs` | → ChunkManager.cs (V3) |
| `VoxelDepthLayer.cs` | → VoxelChunk.cs (V3) |
| `VoxelGrid3D.cs` | Legacy, unused |
| `ChunkedVoxelGrid.cs` | Legacy, unused |
| `UndergroundTerrainChunk.cs` | Legacy, unused |
| `VoxelChunk.cs` (V2) | → VoxelChunk.cs (V3) |
| `MarchingCubesGenerator.cs` | → ChunkMesher.cs (V3) |
| `MarchingCubesTables.cs` | → MarchingCubesTables.cs (V3) |
| `AsyncMarchingCubesGenerator.cs` | Not needed in V3 |
| `DiggingV2RuntimeSetup.cs` | V2 specific bootstrap |
| `UndergroundTerrainSaveData.cs` | → TerrainSaveData.cs (V3) |
| `DiggingSaveManager.cs` (V2) | → DiggingSaveManager.cs (V3) |
| `FloatingTerrainRemover.cs` | → FloatingTerrainDetector.cs (V3) |
| `FallingTerrainChunk.cs` (V2) | → FallingTerrainChunk.cs (V3) |
| `MeshIslandDetector.cs` | V2 specific, expensive |
| `DiggingDebugPanel.cs` | → DiggingDebugHUD.cs (V3) |
| `DepthDebugUI.cs` | V2 specific |
| `DepthLayerConfig.cs` | V2 layer config |
| `DepthVerticalMover.cs` | V2 specific |
| `DigShaftWallGenerator.cs` | Deprecated (cylindrical) |
| `GuidedPitController.cs` | V2 specific |
| `ToolDigProfileLink.cs` | V2 specific |
| `PickupPromptUI.cs` | V2 specific |
| `PickupSystemSetup.cs` | V2 specific |
| `ResourcePickupMerger.cs` | V2 specific |
| `DigInputMode.cs` | V2 specific |
| `IDiggableTerrain.cs` (V2) | V3 has its own |

---

## DIGGINGV3 FILES - DISPOSITION

### RENAME (8 files):
Remove V3 suffix:

| V3 File | New Name |
|---------|----------|
| `DiggingSystemV3.cs` | `DiggingSystem.cs` |
| `MarchingCubesTablesV3.cs` | `MarchingCubesTables.cs` |
| `V3FallingTerrainChunk.cs` | `FallingTerrainChunk.cs` |
| `V3TerrainSaveData.cs` | `TerrainSaveData.cs` |
| `DiggingSaveManagerV3.cs` | `DiggingSaveManager.cs` |
| `V3ResourceSpawner.cs` | `ResourceSpawner.cs` |
| `V3FloatingTerrainDetector.cs` | `FloatingTerrainDetector.cs` |
| `DiggingV3DebugHUD.cs` | `DiggingDebugHUD.cs` |

### DELETE (1 file):
| File | Reason |
|------|--------|
| `DiggingSystemSwitch.cs` | No longer needed - only one system |

### KEEP (17 files):
Just namespace change:
- `IDiggableTerrain.cs`
- `DigOperation.cs`
- `ChunkCoord.cs`
- `ColliderUpdateScheduler.cs`
- `TerrainLayerType.cs`
- `IDigToolProvider.cs`
- `SimpleDigToolProvider.cs`
- `ToolSystemDigProvider.cs`
- `ChunkMesher.cs`
- `DigBoundsProvider.cs`
- `WorldBootstrapper.cs`
- `TerrainLayerSettings.cs`
- `TerrainLayerDetector.cs`
- `TerrainLayerShaderUpdater.cs`
- `ToolEffectivenessTracker.cs`
- `VoxelChunk.cs`
- `ChunkManager.cs`

---

## NAMESPACE CHANGES

### All Files:
```csharp
// Before:
namespace UnderTheFloor.DiggingV2 { }
namespace UnderTheFloor.DiggingV3 { }
namespace UnderTheFloor.Player { }
namespace UnderTheFloor.UI { }
// etc.

// After:
namespace BeneathTheFloor.Digging { }
namespace BeneathTheFloor.Player { }
namespace BeneathTheFloor.UI { }
// etc.
```

### Using Statements:
```csharp
// Before:
using UnderTheFloor.DiggingV2;
using UnderTheFloor.DiggingV3;
using UnderTheFloor.Player;

// After:
using BeneathTheFloor.Digging;
using BeneathTheFloor.Player;
```

---

## PROJECT SETTINGS CHANGES

| Setting | Before | After |
|---------|--------|-------|
| Project Name | Under the Floor | Beneath the Floor |
| Default Namespace | UnderTheFloor | BeneathTheFloor |

---

## EXTERNAL FILE UPDATES NEEDED

### High Priority (Core Logic):
1. `MissionManager.cs` - Update V2 event subscriptions → unified system
2. `UpgradeStation.cs` - Remove V2 calls, use unified system
3. `UpgradeSystem.cs` - Update tool profile handling
4. `PlayerToolVisualController.cs` - Update namespace
5. `GameEvents.cs` - Update `UndergroundResourceType` namespace
6. `FirstRoomUpgradeStationUI.cs` - Already updated, verify namespace

### Medium Priority (Systems):
7. `WorldDropManager.cs` - Update namespace
8. `HUDController.cs` - Update references
9. `HeldToolController.cs` - Update references
10. `SaveManager.cs` - Update save system
11. `WorldPickupSaveManager.cs` - Update namespace

### Lower Priority (Debug/Editor):
12. All editor scripts in `Assets/Scripts/Editor/`
13. Debug HUD scripts
14. Test scripts

---

## EXECUTION PHASES

### Phase 1: Create Unified Digging Folder
1. Create `Assets/Scripts/Digging/` with subfolders
2. Move V3 files to Digging/ (rename V3 → unified names)
3. Move needed V2 files to Digging/
4. Update internal namespaces to `BeneathTheFloor.Digging`

### Phase 2: Rename Namespace Project-Wide
1. Replace `UnderTheFloor` → `BeneathTheFloor` in ALL .cs files
2. Replace `DiggingV2` → `Digging` in all using statements
3. Replace `DiggingV3` → `Digging` in all using statements
4. Update class references (DiggingSystemV2 → DiggingSystem, etc.)

### Phase 3: Update Project Settings
1. Change project name in ProjectSettings.asset
2. Update any asmdef files if present

### Phase 4: Delete V2 Folder
1. Delete `Assets/Scripts/DiggingV2/` entirely
2. Delete V2 editor scripts

### Phase 5: Compile and Fix
1. Compile in Unity
2. Fix any remaining errors
3. Test functionality

---

## CLASS NAME CHANGES SUMMARY

| Old Name | New Name |
|----------|----------|
| `DiggingSystemV2` | `DiggingSystem` |
| `DiggingSystemV3` | `DiggingSystem` |
| `DiggingSystemV2.Instance` | `DiggingSystem.Instance` |
| `DiggingSystemV3.Instance` | `DiggingSystem.Instance` |
| `UndergroundTerrainManager` | `ChunkManager` |
| `DigWorldDropSpawner` | `ResourceSpawner` |
| `DiggingSaveManagerV3` | `DiggingSaveManager` |
| `V3TerrainSaveData` | `TerrainSaveData` |
| `V3ResourceSpawner` | `ResourceSpawner` |
| `V3FallingTerrainChunk` | `FallingTerrainChunk` |
| `V3FloatingTerrainDetector` | `FloatingTerrainDetector` |

---

## ESTIMATED IMPACT

- **Files to modify**: ~150+ files
- **Lines of code affected**: ~1000+ changes
- **Time to complete**: Significant (multiple phases)
- **Risk**: High - many interconnected systems

## RECOMMENDATION

Execute this migration in controlled phases with compilation testing between each phase. This ensures we can identify and fix issues incrementally rather than facing a massive broken build.
