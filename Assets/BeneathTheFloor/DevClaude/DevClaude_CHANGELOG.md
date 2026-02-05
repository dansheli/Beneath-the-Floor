# DevClaude Changelog

All notable changes made by Claude (AI assistant) are documented here.

Format: `[Date] Session Topic - Brief description`

---

## 2024-12-13 - Upgrade System V1: Credit-Based Upgrades

### Problem
- Upgrades used resources (IronIngot, Coal, etc.) as cost, NOT player money (Credits)
- `CurrencyManager.Spend()` was never called by any upgrade system
- Economic loop was broken: Money → Upgrade link was missing

### Solution
Refactored UpgradeStation to use Credits (money) via CurrencyManager as the primary cost.

### Modified - RuntimeUpgrade class (in UpgradeStation.cs)

**New Fields:**
```csharp
[Header("Credit Cost (Primary)")]
public int baseCreditCost = 100;
public float creditCostMultiplier = 1.5f;
```

**New Method:**
```csharp
public int GetNextLevelCreditCost()
// Returns: baseCreditCost * (creditCostMultiplier ^ currentLevel)
```

### Modified - UpgradeStation.cs

**CanPurchaseRuntimeUpgrade():**
- Now checks `CurrencyManager.Instance.CanAfford(creditCost)` FIRST
- Resource costs are checked as secondary/optional requirements
- Returns false if credits are insufficient

**PurchaseRuntimeUpgrade():**
- Calls `CurrencyManager.Instance.Spend(creditCost)` before applying upgrade
- Aborts if Spend fails
- Logs credit spending for debugging

**GetUpgradeCostText():**
- Now shows credit cost first with green/red color based on affordability
- Shows resource costs below (if any)

**Default Upgrades Updated:**
| Upgrade | Base Credit Cost | Multiplier | Notes |
|---------|-----------------|------------|-------|
| Dig Speed | 50 | 1.5x | Levels: 50, 75, 113 |
| Inventory Size | 75 | 1.5x | Levels: 75, 113, 169 |
| Light Radius | 40 | 1.5x | Levels: 40, 60 |
| Move Speed | 100 | 1.5x | Levels: 100, 150 |
| Energy Capacity | 80 | 1.5x | Levels: 80, 120 |

All default upgrades now use credits only (baseCosts = empty list).

### Modified - UpgradeStationUI.cs

**Added:**
- `using BeneathTheFloor.Economy;`
- Subscribes to `CurrencyManager.OnCurrencyChanged` in ShowUI()
- Unsubscribes in HideUI()
- New `OnCurrencyChanged(int)` method that refreshes upgrade list and details

**Result:**
- UI updates button colors in real-time when credits change
- Cost display shows current credits vs. required credits

### Economic Loop Now Complete
```
Dig → Loot → Inventory → Sell → Money → Upgrade → Better Stats
                                   ↑         ↓
                       CurrencyManager.Add() / Spend()
```

### Expected Console Logs
```
[UpgradeStation] Attempting to purchase upgrade: Light Radius
[UpgradeStation] Spent 40 credits for Light Radius
[UpgradeStation] Applied Light Radius: 15
[UpgradeStation] Upgraded Light Radius to level 1
```

### TODOs for Future
- [ ] Add hybrid cost support (Credits + Resources) for advanced upgrades
- [ ] Connect Dig Speed upgrade to DiggingSystemV2 (currently just logs)
- [ ] Create DigToolProfile assets for tool tier switching

---

## 2024-12-13 - ShaftLadderSetup Ramp Positioning Fix V2

### Problem
- Ramp was visible but player still couldn't climb to basement floor
- Baked values didn't work for all shaft configurations
- Ramp position/rotation calculation was incorrect

### Solution
Complete rewrite of dynamic ramp positioning with proper geometry.

### Modified - ShaftLadderSetup.cs

**New Default Values (optimized for walkability):**
- `rampAngle` = 45° (was 55° - easier to walk up)
- `rampThickness` = 0.6f (was 0.4f - thicker for better collision)
- `rampTopOverhang` = 1.5f (was 0.5f - extends onto basement floor)
- `useBakedRampTransform` = false (use dynamic calculation)
- `ladderWidth` multiplied by 1.5x for wider ramp

**Rewritten CreateRampCollider() dynamic calculation:**
- Now uses `GetShaftCenter()` for proper shaft-relative positioning
- Each ladder side (N/S/E/W) has correct position AND rotation
- North: ramp at +Z wall, angles toward -Z, rotation (angle, 0, 0)
- South: ramp at -Z wall, angles toward +Z, rotation (-angle, 0, 0)
- East: ramp at +X wall, angles toward -X, rotation (0, 0, -angle)
- West: ramp at -X wall, angles toward +X, rotation (0, 0, angle)
- Ramp positioned so top reaches basement floor, bottom at shaft bottom

### Key Geometry Fix
The ramp is now positioned at: `wallPosition - (horizontalExtent * 0.5f) - 0.3f`
This ensures the ramp starts near the wall and extends INTO the shaft toward the player.

### Expected Console Log
```
[ShaftLadderSetup] Ramp CALCULATED: length=X.XXm, horizontalExtent=X.XXm, angle=45°, pos=(X,Y,Z), topY=-3, bottomY=-X
```

### How to Test
1. Delete existing ShaftLadder_Root from scene
2. Make sure `Regenerate On Start` is checked
3. Make sure `Use Baked Ramp Transform` is UNCHECKED
4. Enter Play mode
5. Walk toward the ramp from shaft bottom - should be able to walk up

---

## 2024-12-13 - ShaftLadderSetup Freeze Mode

### Problem
- User manually adjusted ladder position in scene to look correct
- Every time Play Mode started, ladder would regenerate and lose manual adjustments

### Solution
Added "frozen mode" to preserve manually positioned ladders.

### Modified - ShaftLadderSetup.cs

**New Field:**
- `regenerateOnStart` (bool, default: true) - When false, ladder is "frozen" and won't regenerate

**Modified Awake():**
- Now searches for existing `ShaftLadder_Root` child
- If found, sets `_ladderRoot` reference and `_isBuilt = true`

**Modified Start():**
- If `regenerateOnStart == false` AND `_ladderRoot != null`:
  - Logs "FROZEN MODE" message
  - Skips rebuild entirely
  - Preserves exact position/rotation/scale of existing ladder

**New Context Menu Items:**
- `Freeze Ladder (Disable Regeneration)` - Sets regenerateOnStart = false
- `Unfreeze Ladder (Enable Regeneration)` - Sets regenerateOnStart = true

### How to Use Frozen Mode
1. Position ladder manually in Scene view (or let it auto-build once)
2. Adjust `ShaftLadder_Root` and children as needed
3. On ShaftLadderSetup component, uncheck `Regenerate On Start` (or use context menu)
4. Save scene
5. On Play, ladder stays exactly where you placed it

### Expected Console Log (Frozen Mode)
```
[ShaftLadderSetup] Awake on 'ShaftLadderSetup' at position (X, Y, Z)
[ShaftLadderSetup] Found existing ShaftLadder_Root at (X, Y, Z), will reuse it.
[ShaftLadderSetup] Start: FROZEN MODE - using existing ladder at (X, Y, Z), no rebuild.
```

### Notes
- Context menu "Rebuild Ladder" still works even in frozen mode (manual override)
- Frozen mode only affects auto-regeneration on Start
- To return to auto-generation, check `Regenerate On Start` or use "Unfreeze" context menu

---

## 2024-12-13 - ShaftLadderSetup Ramp Collider Fix

### Problem
- Ladder was VISIBLE but player could NOT walk up it
- Ramp collider positioning/rotation was broken
- Ladder was trying to span 220m (full shaft depth) even if pit was only 2m deep

### Solution - Complete Ramp Rewrite
Rewrote `CreateRampCollider()` in `ShaftLadderSetup.cs` with simpler, cleaner logic:

### Modified - ShaftLadderSetup.cs

**New Fields:**
- `maxLadderHeight = 10f` - Clamps ladder to reasonable height
- `useRaycastForBottom = true` - Detects actual ground instead of theoretical depth

**New Methods:**
- `DetermineActualBottomY()` - Uses Physics.Raycast to find real ground level
- `ToggleRampVisibility()` - Context menu to toggle debug view

**Ramp Changes:**
- Now creates actual `PrimitiveType.Cube` with both collider AND mesh
- Simplified positioning: calculates center position and rotation directly
- Each ladder side (N/S/E/W) has explicit direction and yaw values
- Default angle changed from 55° to 45° (better for CharacterController)
- `showRampCollider` now defaults to `true` for easier debugging
- Ramp visible as semi-transparent yellow when debug enabled

**Debug Improvements:**
- Added `Awake()` logging
- Added `Start()` logging
- Detailed logs for raycast results
- Logs show ramp length, horizontal extent, angle, and center position

### Expected Console Logs
```
[ShaftLadderSetup] Awake on 'ShaftLadderSetup' at position (X, Y, Z)
[ShaftLadderSetup] Start - will build ladder in 0.3s
[ShaftLadderSetup] Raycast hit ground at Y=-5.00 (object: UndergroundTerrain)
[ShaftLadderSetup] Building ladder: topY=-3.00, bottomY=-5.00, height=2.00m
[ShaftLadderSetup] Ramp created: length=3.33m, horizontalExtent=2.00m, angle=45°
[ShaftLadderSetup] Ladder built on North side from Y=-3.00 to Y=-5.00
```

### How to Test
1. Enter Play Mode in BasementScene
2. Look for yellow semi-transparent ramp leaning into shaft
3. Walk from shaft bottom toward the ramp
4. Player should smoothly walk up to basement floor

### Troubleshooting
- If ramp too steep: reduce `rampAngle` to 30-40°
- If ramp not visible: enable `showRampCollider` in inspector
- If ramp in wrong place: check `ladderSide` setting

---

## 2024-12-13 - ShaftLadderSetup Auto-Creation Fix

### Problem
- ShaftLadderSetup.cs existed and had auto-build in Start()
- BUT no ShaftLadderSetup GameObject existed in BasementScene
- Result: ladder never appeared in play mode

### Solution
- Added `SetupShaftLadder()` to `DiggingV2RuntimeSetup.cs`
- Ladder now auto-creates when DiggingV2 system initializes
- No manual scene wiring required

### Modified - DiggingV2RuntimeSetup.cs
- Added step 8 in `SetupDiggingSystem()`: calls `SetupShaftLadder()`
- Added new method `SetupShaftLadder()`:
  - Checks for existing ShaftLadderSetup (avoids duplicates)
  - Verifies UndergroundTerrainManager exists first
  - Creates "ShaftLadderSetup" GameObject with component
  - Component's Start() then auto-builds the ladder with 0.3s delay

### Expected Console Logs
```
[Setup] SetupShaftLadder starting...
[Setup] Created ShaftLadderSetup - ladder will auto-build on Start()
[ShaftLadderSetup] Auto-detected from TerrainManager: halfW=X, halfL=X, depth=X, floorY=X
[ShaftLadderSetup] Ladder built on North side from Y=X to Y=X
```

### Ladder Configuration
- Side: North (default, configurable)
- Ramp angle: 45 degrees (changed from 55 in later fix)
- Visual: Rails + rungs
- Collider: Walkable ramp (visible yellow when debug enabled)
- Root object name: `ShaftLadder_Root`

---

## 2024-12-13 - DevClaude Meta-Framework

### Added
- `Assets/BeneathTheFloor/DevClaude/` folder structure
- `DevClaude_README.md` - Framework overview
- `DevClaude_CHANGELOG.md` - This file
- `DevClaude_CONVENTIONS.md` - Coding standards

---

## 2024-12-13 - Shaft Ladder Setup

### Added
- `Assets/Scripts/Environment/ShaftLadderSetup.cs` (NEW FILE)
  - Visual ladder with rails and rungs
  - Invisible ramp collider for walking up (not climbing)
  - Auto-detects shaft dimensions from UndergroundTerrainManager
  - Supports 4 sides: North, South, East, West
  - Configurable ramp angle (default 30 degrees)
  - Material customization for rails/rungs/ramp

### Design Notes
- Player walks up invisible ramp (no climbing mechanic needed)
- Visual ladder is purely decorative
- Ramp uses Physics material with zero friction
- Works with rectangular shaft system

---

## 2024-12-13 - Inventory Core V1 & Modular Sell System

### Modified - GameEvents.cs
- Added `OnInventoryFull` (System.Action, legacy parameterless)
- Added `OnInventoryFullWithDetails` (System.Action<ItemSO, int>) - includes item and remaining amount
- Added `OnItemsSold` (System.Action<int, int>) - totalCredits, totalItemCount

### Modified - InventorySystem.cs
- Marked legacy structures with `[Obsolete]` attributes
- Added V1 Public API region with:
  - `TryAddItem(ItemSO item, int amount, out int remainingAmount)` - returns true if any added
  - `TryRemoveItem(ItemSO item, int amount)` - returns true if all removed
  - `GetAllStacks()` - returns IReadOnlyList<ItemStack>

### Modified - DigInventoryBridge.cs
- Updated `AwardResource()` to use new `TryAddItem` API
- Properly handles partial additions with remaining amount
- Fires `OnInventoryFullWithDetails` when inventory can't hold all items

### Added - InventorySellService.cs (NEW FILE)
- Centralized singleton service for selling items
- Location: `Assets/Scripts/Economy/InventorySellService.cs`
- Methods:
  - `SellAllSellableItems(InventorySystem)` - sells all items with sellPrice > 0
  - `SellItem(InventorySystem, ItemSO, int)` - sells specific item
  - `GetTotalSellableValue(InventorySystem)` - preview total value
  - `CountSellableItems(InventorySystem)` - count sellable items
- Fires `OnItemsSold` event after sales

### Modified - TradeTerminal.cs
- Added `SellAllForPlayer()` method that delegates to InventorySellService
- Added `EnsureTradeTerminalUI()` - auto-creates UI if missing
- Added `EnsureInventorySellService()` - auto-creates service if missing

### Modified - TradeTerminalUI.cs
- Added `sellAllButton` serialized field
- Added `OnSellAllClicked()` handler
- Added `CreateDefaultUIHierarchy()` - creates full UI programmatically
- Added helper methods:
  - `CreateDefaultHeader()`
  - `CreateDefaultItemListPanel()`
  - `CreateDefaultDetailsPanel()`
  - `CreateDefaultFooter()`
- Fixed `FindItemListContent()` to check for TradeTerminalPanel child

---

## 2024-12-13 - TradeTerminalUI Bug Fixes

### Issue 1: itemListContent not found
- **Error**: `[TradeTerminalUI] Could not find itemListContent in hierarchy!`
- **Fix**: Modified `FindItemListContent()` to also search TradeTerminalPanel child

### Issue 2: TradeTerminalUI.Instance is null
- **Error**: `[TradeTerminal] TradeTerminalUI.Instance is null!`
- **Fix**: Added `EnsureTradeTerminalUI()` in `OpenTerminal()` that finds or creates UI

### Issue 3: UI hierarchy completely missing
- **Fix**: Added `CreateDefaultUIHierarchy()` that creates complete UI structure:
  - Header with title and close button
  - Item list with scroll view
  - Details panel
  - Footer with Sell All button and currency display

---

## Earlier Sessions (Pre-Framework)

### DiggingV2 to Inventory Bridge
- Created `DigInventoryBridge.cs` for connecting dig rewards to inventory
- Handles resource type mapping
- Supports item multiplication and bonuses

### Resource Pickup System
- `ResourcePickupMerger.cs` - Merges nearby pickups
- `ResourcePickupInteractor.cs` - Player pickup interaction
- `PickupPromptUI.cs` - UI prompt for pickups

### Depth HUD
- Integrated depth display with DepthManager
- Shows current depth level to player

### Rectangular Shaft System
- `RectShaftGenerator.cs` - Generates rectangular shaft
- `UndergroundTerrainManager.cs` - Manages underground terrain
- Configurable shaft dimensions

---

## Notes for Future Sessions

### Files That Should NOT Be Overwritten Without Review
- `InventorySystem.cs` - Contains V1 API, check for breaking changes
- `GameEvents.cs` - Central event hub, only add new events
- `InventorySellService.cs` - Singleton pattern, don't duplicate
- `TradeTerminal.cs` - Has auto-creation logic
- `TradeTerminalUI.cs` - Has self-healing UI creation
- `DiggingV2RuntimeSetup.cs` - Master runtime bootstrap, sets up all DiggingV2 systems
- `ShaftLadderSetup.cs` - Ladder system, auto-builds from Start()

### Patterns Established
- Singleton pattern: `Instance` property with null check
- V1 API naming: `TryXxx` methods with out parameters
- Auto-creation: Services create themselves if missing
- Self-healing UI: UI rebuilds itself if hierarchy missing

---
*Updated: 2024-12-13*
