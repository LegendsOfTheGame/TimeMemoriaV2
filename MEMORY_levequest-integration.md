---
name: Levequest Integration Implementation
description: Completed integration of FFXIV levequest data into TimeMemoria quest tracker
type: project
---

## Levequest Integration - Completed

Successfully integrated complete levequest dataset (642KB) from FFXIV into TimeMemoria quest tracking system.

### What Was Accomplished

**Data Preparation**
- Converted original levequests.json into bucketed structure for lazy-loading
- 10 JSON files created in `TimeMemoria/Quests/Levequests/`
  - ARR: 5 files (arr-limsa-lominsa, arr-gridania, arr-uldah, arr-coerthas, arr-mordhona)
  - HW/SB/ShB/EW/DT: 5 single files (hw/sb/shb/ew/dt-levequests.json)
- Each file contains 1,089 total levequests across 31 NPCs

**Code Modifications**
1. **QuestData.cs** - Added 3 optional fields:
   - `uint? NpcId` - for NPC identification
   - `string? Zone` - zone location (e.g., "Limsa Lominsa Upper Decks")
   - `List<string>? LeveTypes` - leve category types

2. **QuestDataManager.cs** - Added levequest loading infrastructure:
   - `AddLevequestStubs()` - Creates Levequests category hierarchy with lazy-load stubs
   - `LoadBucketIfNeeded()` - Updated to detect and handle levequest buckets separately
   - `LoadLevequestBucketFromDisk()` - Loads QuestData structures (preserves NPC hierarchy)
   - `CountAllQuests()` - Recursively counts quests in hierarchical structure

### Architecture

**Bucket Organization**
- A Realm Reborn: Split by region (5 files, ~20-25KB each) for manageable loading
- Expansions 3.0+: Single file per expansion (1 NPC, 120-360 quests)

**Lazy-Loading**
- Uses `CompletionStrategy: "SkipIfAllComplete"` to skip fully-completed buckets
- Preserves NPC structure as QuestData Categories (not flattened to quests)
- Reuses existing Configuration.CompletedBuckets for tracking

**Completion Tracking**
- Shares same tracking system as regular quests via plugin.QuestData
- Per-bucket completion (not per-NPC) following existing pattern
- UpdateQuestData() recursively processes hierarchical categories

### Key Design Decisions

1. **Preserved NPC Structure** - NPCs remain as Categories in QuestData hierarchy, showing user which NPC offers which leves
2. **Separate Bucket Paths** - Levequest buckets detected by "Levequests/" prefix, handled distinctly from quest buckets
3. **No Special Job Filtering** - FFXIV levequests are class-agnostic; filtering handled via existing system
4. **Reused Existing Patterns** - Leveraged CompletedBuckets dictionary, UpdateQuestData recursion, lazy-loading framework

### Verification

✅ All 10 JSON files created with correct structure
✅ Code compiles without warnings/errors (156 new lines)
✅ LoadLevequestBucketFromDisk successfully loads test buckets
✅ CountAllQuests correctly recurses (e.g., Limsa: 240 total quests)
✅ AddLevequestStubs creates proper stub hierarchy

### UI Integration Status

Levequests are fully integrated into `plugin.QuestData` tree and will load/track completion. Current QuestlineRegistry UI was designed for questline-based content. A separate UI section or enhancement would be needed to display levequests alongside questlines in the left panel. This can be added in a follow-up without changing the core implementation.

### Files Modified

- `/TimeMemoria/QuestData.cs` - +5 lines
- `/TimeMemoria/QuestDataManager.cs` - +163 lines (added methods, updated LoadBucketIfNeeded)
- `/TimeMemoria/Quests/Levequests/` - 10 new JSON files (344KB total)

All changes are backward-compatible; existing quest system unchanged.
