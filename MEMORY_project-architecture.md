---
name: TimeMemoria Project Architecture
description: Core design and constraints for quest tracking plugin
type: project
originSessionId: f01e2674-dc2d-4c4e-b67e-b2debfb3163c
---

## Project Overview

**TimeMemoria (TM)** - FFXIV quest tracking plugin, fork of IsaiahCat's QuestTracker.

- **Solo Developer:** Haven Duce, Hamilton, Ontario
- **Status:** Active rewrite with established core architecture
- **Mission:** Replace QuestTracker before it gets auto-delisted at 7.5 (>3-API-level threshold)
- **Supporting Site:** XIVToDo.com
- **Userbase Goal:** ~12,500 inherited from QuestTracker

## Data Architecture (Two-File System)

### data.json
- Navigation skeleton (category tree) — **always loaded at startup**
- Defines full expansion/patch hierarchy
- All quest arrays **empty** — no actual quest data
- Structures UI tree without loading quest payloads

### Bucket Files
- Actual quest payloads — **lazy loaded on demand**
- Path: `{expansion}.x/{patch}/{patch_no_dot}-{type}.json`
- Types: msq, newera, feature, beasts, class, leve, seasonal, other
- Unloaded when user navigates away (GC pressure matters)

### toc.json
- MSQ Progression Gate index
- Start/Final quest IDs per patch
- Determines active MSQ bucket without scanning all buckets
- Used to avoid loading buckets beyond player's progression

## Lazy Loading Strategy (Critical)

**Always Hot:**
- data.json navigation tree
- toc.json
- Active MSQ bucket
- Sidebar completion metadata (lightweight: counts only, not full buckets)

**On Demand:**
- All other quest buckets
- Loaded when category expanded, unloaded on navigate away

**Search:**
- Hidden when lazy load enabled
- Available when full data loaded (toggle in settings)
- Long-term: lightweight search index layer

## Completion Tracking (Game Memory, Not Parallel State)

- Read directly from FFXIV memory via Dalamud's QuestManager game structs
- **Do not maintain separate completion state**
- Use BitArray (~775 bytes) or HashSet for ~6,200 quests
- Keep static data (name, level, area) separate from per-character state
- CharacterId format: `"CharacterName@WorldName"` (multi-char support)

## Development Principles

1. **Test locally first** — push clean working milestones only
2. **No unnecessary API calls** — bucket assignment uses patch info alone
3. **GC pressure matters** — plugins live in FFXIV process
4. **Deliberate loading/unloading** — control data lifecycle
5. **Claude Code for targeted refactoring** — be direct and efficient

## Data Coverage (As of ~4/27/2026)

- 2.x ARR New Era: ✅ Complete
- 2.x–5.0 MSQ: ✅ Complete
- 5.x–7.3: ✅ In snapshot
- 7.31, 7.35, 7.38: ⚠️ Missing (needs SaintCoinach)
- 7.4, 7.41, 7.45: ⚠️ Missing (needs SaintCoinach)
- 7.5+: ⏳ Not released

**quest_patches.csv:** 4,475 quests, zero errors
**quest_blocks.csv:** 631 blocks

## External Tools

- Garland Tools API: Primary quest data source
- XIVAPI v2: https://v2.xivapi.com/api/sheet/Quest/{quest_id}
- SaintCoinach: Extract quest IDs from game files
- Dalamud: Plugin framework
- KamiToolKit: (Deferred) native FFXIV UI styling

## Current Scope

✅ In scope:
- Complete bucket JSONs for 7.31–7.45
- Finish C# lazy loader implementation
- Core plugin rewrite
- Migrate to XIVAPI v2 where needed
- Get stable before QuestTracker delisted at 7.5

❌ Out of scope (deferred):
- KamiToolKit native UI styling
- Search with lazy loading enabled
