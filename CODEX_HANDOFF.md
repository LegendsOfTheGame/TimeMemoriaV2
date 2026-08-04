# TimeMemoria v2 - Complete Handoff Document

**Project Date**: June 7, 2026  
**Developer**: Haven Duce  
**Repository**: https://github.com/LegendsOfTheGame/TimeMemoriaV2.git  
**Status**: Active development on API 14 (390 modified files in working tree)  

---

## CRITICAL CONTEXT - READ THIS FIRST

### What This Project Is
**TimeMemoria (TM)** is a FFXIV Dalamud plugin for quest progression and pacing tracking. It is **strictly quest-pacing focused** — NOT a performance analysis tool. It replaces QuestTracker before it auto-delists at FFXIV patch 7.5 (API level threshold).

**Solo Developer**: Haven Duce (Hamilton, Ontario)  
**Userbase Goal**: ~12,500 inherited from QuestTracker  
**Supporting Site**: XIVToDo.com  

### The Prime Directive (Non-Negotiable)
- ✅ **ALLOWED**: Quest completion tracking, playtime aggregates, descriptive pacing metrics, patch/maintenance status
- ❌ **PROHIBITED ABSOLUTELY**: DPS, HPS, combat logs, duty results, ACT/FFLogs data, automation, chat commands, alerts, timers, overlays, performance metrics, rankings

See `prime-constraints.md` (in memory) for complete ethical boundaries.

---

## Project State

### Git Repository
- **Remote**: origin https://github.com/LegendsOfTheGame/TimeMemoriaV2.git
- **Current Branch**: main
- **Uncommitted Changes**: 390 modified files (mostly quest JSON buckets)

### Recent Commits
```
a04e30a fix: use IsLevequestComplete() instead of IsQuestComplete()
ed93b18 fix: load levequests from game data (Lumina), eliminate false completion flags
e3cc915 perf: eliminate 10-second loading delay for levequests
0f77278 fix: enable levequest selection in UI
5dab95c fix: correct levequest ARR filenames to match expected path convention
3dceb97 fix: populate levequest opening levels from wiki data
7928ba1 Updated to 14.7.1.0
```

### Current Plugin Version
- **Version String**: 14.2.0.1
- **Format**: AA.B.C.D
  - AA = Dalamud API version (14 = FFXIV 7.4–7.49)
  - B = Expansion band (2 = ARR, 3 = HW, etc.)
  - C = Patch within that band (0–5)
  - D = Number of quest buckets complete for that band/patch
- **Build Target**: net10.0-windows, x64, Dalamud.NET.Sdk/15.0.0

---

## Architecture Overview

### Two-File Data System (Critical Design)

#### data.json
- **Purpose**: Navigation skeleton (category tree), always loaded at startup
- **Contains**: Full expansion/patch hierarchy with EMPTY quest arrays
- **Why**: Structures UI tree without loading large quest payloads
- **Location**: TimeMemoria/Quests/data.json

#### Bucket Files (Lazy-Loaded)
- **Purpose**: Actual quest payloads, loaded on demand
- **Path Pattern**: `{expansion}.x/{patch}/{patch_no_dot}-{type}.json`
- **Types**: msq, newera, feature, beasts, class, leve, seasonal, other
- **Examples**:
  - 2.0-msq.json (Main Scenario Quests for ARR 2.0)
  - 3.0-feature.json (Feature quests for Heavensward 3.0)
  - levequests/arr-limsa-lominsa.json (Regional levequests)
- **Unloading**: Unloaded when user navigates away (GC pressure critical for plugin lifecycle)

#### toc.json
- **Purpose**: MSQ Progression Gate index
- **Contains**: Start/Final quest IDs per patch
- **Why**: Determines active MSQ bucket without scanning all buckets
- **Usage**: Used to avoid loading buckets beyond player's progression

### Lazy Loading Strategy (Must Preserve)

**Always Hot:**
- data.json (navigation tree)
- toc.json (progression gates)
- Active MSQ bucket (player's current story)
- Sidebar completion metadata (lightweight counts only, not full buckets)

**On Demand:**
- All other quest buckets
- Loaded when category expanded
- Unloaded on navigate away

**Search:**
- Hidden when lazy load enabled
- Available when full data loaded (toggle in settings)
- Long-term: lightweight search index layer

### Completion Tracking (Game Memory Integration)

- **Source**: FFXIV memory via Dalamud's QuestManager game structs
- **Storage**: BitArray (~775 bytes) or HashSet for ~6,200 quests
- **Architecture**: Keep static data (name, level, area) separate from per-character state
- **Character ID Format**: `"CharacterName@WorldName"` (multi-character support)
- **Key Principle**: Do NOT maintain separate completion state — read directly from game

---

## Core C# Classes

### Configuration.cs
Manages user settings and plugin persistence.

**Key Members**:
- `CompletedBuckets`: Dictionary tracking which quest buckets have been fully completed
- `PauseTracking`: Boolean to pause/resume quest tracking
- `Character`: Current character context

### QuestData.cs
Data model for quest information.

**Properties**:
- `Id`: Quest ID (int)
- `Name`: Quest name (string)
- `Level`: Required level (int)
- `Categories`: Hierarchical structure (List<QuestData>)
- `IsQuest`: Boolean distinguishing categories from quests
- `BucketPath`: Path to lazy-load bucket file (string?)
- **Levequest Extensions** (added):
  - `NpcId`: NPC offering the leve (uint?)
  - `Zone`: Zone location (string?)
  - `LeveTypes`: Leve category types (List<string>?)

### QuestDataManager.cs
Core loading and processing engine.

**Key Methods**:
- `LoadBucketIfNeeded()`: Detects bucket type and delegates to appropriate loader
- `LoadMSQBucketFromDisk()`: Loads MSQ quest buckets
- `LoadBucketFromDisk()`: Loads generic quest buckets
- `LoadLevequestBucketFromDisk()`: Loads levequest buckets (preserves NPC hierarchy)
- `AddLevequestStubs()`: Creates Levequests category hierarchy with lazy-load stubs
- `CountAllQuests()`: Recursively counts quests in hierarchical structure
- `UpdateQuestData()`: Updates completion state from game memory

**Critical Pattern**: 
```csharp
if (category.BucketPath != null && !Configuration.CompletedBuckets.ContainsKey(category.BucketPath))
{
    LoadBucketIfNeeded(category);
}
```

### PlaytimeStatsService.cs
Manages pacing metrics (session and lifetime).

**Key Concept**: Purely descriptive — never implies skill, efficiency, or performance judgment.

### MainWindow.cs
UI implementation (WPF, Dalamud ImGui integration).

---

## Data Coverage Status

| Expansion | Patches | MSQ | NewEra | Feature | Other |
|-----------|---------|-----|--------|---------|-------|
| ARR (2.x) | 2.0–2.55 | ✅ | ✅ | ⚠️ | ⚠️ |
| HW (3.x) | 3.0–3.56 | ✅ | — | — | — |
| SB (4.x) | 4.0–4.56 | ✅ | — | — | — |
| ShB (5.x) | 5.0–5.55 | ✅ | — | — | — |
| EW (6.x) | 6.0–6.55 | ✅ | — | — | — |
| DT (7.x) | 7.0–7.3 | ✅ | — | — | — |
| DT+ | 7.31, 7.35, 7.38 | ⚠️ Missing | — | — | — |

**Levequests**: ✅ Complete (1,774 total across 32 NPCs)
- ARR: 5 regional files (Coerthas, Gridania, Limsa Lominsa, Mor Dhona, Ul'dah)
- HW/SB/ShB/EW/DT: 1 file per expansion

**Quest Counts**:
- quest_patches.csv: 4,475 quests, zero errors
- quest_blocks.csv: 631 blocks

---

## Recent Major Work (Last 30 Days)

### Levequest Integration (COMPLETED)
- ✅ Integrated 1,774 levequests into quest tracking system
- ✅ Fixed JSON deserialization (all Level fields populated from wiki data)
- ✅ Eliminated 10-second loading delay (lazy-loading optimization)
- ✅ Fixed false completion flags (using IsLevequestComplete() from Lumina)
- ✅ Enabled UI selection for levequests
- ✅ Preserved NPC hierarchy in UI

**Commits**:
- 3dceb97: populate levequest opening levels from wiki data
- 0f77278: enable levequest selection in UI
- e3cc915: eliminate 10-second loading delay
- ed93b18: load levequests from game data (Lumina)
- a04e30a: use IsLevequestComplete() instead of IsQuestComplete()

### ARR 2.0 Sidequest Migration (IN PROGRESS)
- Currently hardcoded in data.json: Hildibrand, Zodiac Weapons, Delivery Moogles, regional sidequests
- Next step: Move to lazy-loaded bucket files (2.0-hildibrand.json, 2.0-zodiac.json, 2.0-sidequest.json)
- Naming convention: `{patch}-{type}.json`

---

## Build & Setup

### Prerequisites
- Windows 10/11
- .NET 10.0+ SDK
- Visual Studio 2022 or Rider
- Dalamud development environment

### Build
```bash
dotnet build
# Or in Visual Studio: Build > Build Solution
```

### Configuration
- Settings stored in: `%APPDATA%\XIVLauncher\pluginConfig\TimeMemoria\config.json`
- Quest data: `TimeMemoria/Quests/` directory
- No external API keys required (uses local Dalamud APIs)

### Dependencies
- Dalamud.NET.Sdk/15.0.0 (plugin framework)
- Newtonsoft.Json (JSON parsing)
- Lumina (FFXIV game data library)
- Standard .NET base class libraries

---

## User Learning Context

**Important**: The original developer (Haven Duce) learns code like learning a new language. When making changes, provide:
- Comprehensive explanations (not shorthand)
- WHY code is written a certain way
- Syntax and pattern breakdowns
- Analogies when helpful
- Definition of terms (assume zero prior knowledge)

The goal is understanding, not just working code.

---

## Development Principles

1. **Test locally first** — push clean working milestones only
2. **No unnecessary API calls** — bucket assignment uses patch info alone
3. **GC pressure matters** — plugins live in FFXIV process memory
4. **Deliberate loading/unloading** — control data lifecycle explicitly
5. **Preserve lazy loading** — do not load all quest data at startup
6. **Follow Dalamud API docs** — use dalamud.dev as reference
7. **Document constraints** — update CLAUDE.md if constraints change

---

## Dalamud AI Policy (For Repo Submission)

If submitting to official plugin repository (goatcorp/DalamudPluginsD17):

### Disclosure Levels
1. **None** — No AI (no disclosure)
2. **Hint** — Autocomplete only (no disclosure)
3. **Assist** — AI acts on specific tasks, human completes work ← **Current Level**
4. **Pair** — Active collaboration, roughly equal contribution
5. **Copilot** — AI implements, human plans/reviews
6. **Auto** — AI autonomous (avoid)

### Requirements
- Must personally test plugin before submission
- Never say "I'm not sure, the AI did it"
- Verify AI output (Dalamud APIs are frequently wrong)
- Be receptive to feedback
- Disclose AI involvement beyond autocomplete via AI-DECLARATION.md

### Enforcement
- Entirely AI-generated = auto-reject (2nd violation = ban)
- Undisclosed AI use = ban
- AI mistakes with clear human intent = fixable, can resubmit

---

## External Resources

### Quest Data Sources
- **Garland Tools API**: Primary quest data source
- **XIVAPI v2**: https://v2.xivapi.com/api/sheet/Quest/{quest_id}
- **SaintCoinach**: Extract quest IDs from game files (for patches 7.31+)
- **FFXIVCollect**: Wiki data for levequest opening levels

### Dalamud Documentation
- Official: https://dalamud.dev
- SDK Repo: https://github.com/goatcorp/Dalamud.NET.Sdk
- Plugin Examples: https://github.com/goatcorp/SamplePlugin

### Related Projects
- **QuestTracker** (predecessor): https://github.com/isaiahcat/QuestTracker
- **BetterPlaytime** (inspiration): https://github.com/Infiziert90/BetterPlaytime
- **LeveHelper** (inspiration): https://github.com/Haselnussbomber/LeveHelper

---

## Memory Files (Claude Code Context)

These files were saved for context continuity and document key decisions:

### 1. project-architecture.md
- Complete architecture overview
- Two-file system explanation
- Data coverage status
- Lazy loading strategy
- MSQ progression gate logic

### 2. levequest-integration.md
- Completed levequest integration (1,774 quests)
- Architecture decisions
- NPC hierarchy preservation
- Verification steps

### 3. levequest-fix.md
- JSON deserialization fix
- All 1,774 Level fields populated
- NPC mapping for ARR (32 total)

### 4. arr-2-0-migration.md
- Current hardcoded sidequests in data.json
- Next migration steps
- Bucket naming convention (x.y-{type}.json)

### 5. dalamud-ai-policy.md
- Official plugin repo submission requirements
- AI disclosure levels and enforcement
- Testing and verification requirements

### 6. user-learning-style.md
- Developer learns code like a language
- Needs comprehensive explanations
- Wants to understand WHY, not just get code

### 7. prime-constraints.md
- Non-negotiable ethical boundaries
- Prohibited features (combat metrics, automation, etc.)
- Permitted features (quest tracking, pacing stats only)
- Versioning scheme for API migration

---

## Known Issues & Gotchas

1. **390 Modified Files**: Quest JSON buckets have uncommitted changes. Decide whether to stash, commit, or continue from this state.

2. **Missing Patches 7.31, 7.35, 7.38, 7.4, 7.41, 7.45**: Requires SaintCoinach extraction. These are not blocking for current functionality.

3. **ARR 2.0 Sidequests**: Currently hardcoded in data.json (Hildibrand, Zodiac Weapons, Delivery Moogles, regional quests). Plan to migrate to lazy-loaded buckets.

4. **Search Feature**: Disabled when lazy loading enabled. Enable by loading full data set (toggle in settings). Long-term: build lightweight search index.

5. **API 15 Migration**: When FFXIV 7.5 launches, version changes from 14.x.x.x to 15.x.x.x. Only the AA digit changes; all other logic remains same.

---

## Next Steps (Not Urgent)

1. **Commit the 390 modified files** — decide whether to keep as-is or organize further
2. **ARR 2.0 Sidequest Migration** — move hardcoded quests from data.json to bucket files
3. **Complete 7.31–7.45 Data** — use SaintCoinach to extract missing patches
4. **API 15 Migration** — when FFXIV 7.5 launches (version bump, no logic changes)
5. **Plugin Repo Submission** — when ready, submit to goatcorp/DalamudPluginsD17 with Assist-level AI disclosure

---

## Summary for Context Transfer

**What to tell Codex**:

1. **This is a FFXIV quest tracking plugin**, not a performance tool. Ethical boundaries are strict (see Prime Directive).

2. **Architecture is two-file**: data.json (navigation) + lazy-loaded bucket files (quest content). This is intentional to reduce memory pressure on the plugin.

3. **390 uncommitted files** are mostly quest JSON buckets that have been validated. Decide how to handle (stash, commit, or continue).

4. **Developer learns code like a language** — explanations should be comprehensive, not shorthand.

5. **Key constraints**:
   - Do not expand scope without explicit request
   - Never touch combat/performance metrics
   - Always test locally before pushing
   - GC pressure matters (it's a plugin in the game process)
   - Preserve lazy loading (do not load all quests at startup)

6. **Dalamud AI Policy** — if submitting to official repo, must disclose AI involvement (currently "Assist" level) and test thoroughly.

7. **Resources**:
   - Repository: https://github.com/LegendsOfTheGame/TimeMemoriaV2.git
   - Docs: https://dalamud.dev
   - Supporting site: XIVToDo.com

---

## Contact & Questions

**Original Developer**: Haven Duce  
**Repository Issues**: https://github.com/LegendsOfTheGame/TimeMemoriaV2/issues  
**Dalamud Discord**: https://discord.gg/3NMcUV5 (for Dalamud API questions)  

---

**Generated**: June 7, 2026  
**For Transfer To**: Codex or compatible IDE/tool  
