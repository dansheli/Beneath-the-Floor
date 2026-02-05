# DevClaude - AI Development Memory & Safety Framework

This folder contains documentation created and maintained by Claude (AI assistant) to ensure:
1. **Persistent Memory** - Track all changes across chat sessions
2. **Safe Editing** - Never silently overwrite or duplicate systems
3. **Human Visibility** - Easy to see what was changed and why

## Contents

| File | Purpose |
|------|---------|
| `DevClaude_README.md` | This file - overview and instructions |
| `DevClaude_CHANGELOG.md` | Chronological log of all changes made by Claude |
| `DevClaude_CONVENTIONS.md` | Coding standards and patterns used in this project |

## How to Use

### For Claude (AI)
1. **Before editing any file**: Check CHANGELOG to see if it was previously modified
2. **After completing work**: Update CHANGELOG with what was done
3. **When creating new systems**: Document in CONVENTIONS if it establishes a pattern
4. **Never**: Silently overwrite existing systems without checking history

### For Human Developer
1. Review CHANGELOG to see all AI-made changes
2. Check CONVENTIONS for patterns to follow
3. Add notes in these files if you manually changed AI-created code

## Key Systems Created/Modified by Claude

### Economy System
- `InventorySellService.cs` - Centralized sell logic (singleton)
- `TradeTerminal.cs` - Trade terminal with auto-UI creation
- `TradeTerminalUI.cs` - Self-healing UI hierarchy
- `CurrencyManager.cs` - Currency tracking

### Inventory System (V1 API)
- `InventorySystem.cs` - Refactored with TryAddItem/TryRemoveItem/GetAllStacks
- `DigInventoryBridge.cs` - Bridge between digging and inventory

### Environment
- `ShaftLadderSetup.cs` - Visual ladder + invisible ramp for shaft climbing

### Events
- `GameEvents.cs` - Added OnInventoryFullWithDetails, OnItemsSold

## Version History

See `DevClaude_CHANGELOG.md` for complete history.

---
*This framework was created to maintain continuity across AI chat sessions.*
