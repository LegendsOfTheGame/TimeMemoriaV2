# Quick Start for Codex (or Replacement Tool)

Copy and paste this text into your new tool to start with full context.

---

## What You Need to Know

1. **This is a FFXIV quest tracking plugin** (not a performance tool)
2. **Architecture**: data.json (navigation skeleton) + lazy-loaded bucket files (quest content)
3. **390 uncommitted files** in the working tree (mostly quest JSON — validated, not broken)
4. **Developer learns code like learning a language** — explain WHY, not just HOW
5. **Strict ethical boundaries** — no DPS, combat logs, automation, overlays, alerts, etc.

---

## The Main Handoff Document

**Read this first**: [CODEX_HANDOFF.md](CODEX_HANDOFF.md)

Contains:
- Complete project overview
- Architecture explanation
- Build instructions
- Current development state
- All constraints and policies
- Links to resources

---

## Memory Files (Context Library)

All saved from Claude Code's persistent memory system. Reference as needed:

- **MEMORY_prime-constraints.md** — What you CAN and CANNOT do
- **MEMORY_project-architecture.md** — How the two-file system works
- **MEMORY_levequest-integration.md** — Example of how to add bulk data
- **MEMORY_levequest-fix.md** — Debugging example
- **MEMORY_arr-2-0-migration.md** — Next planned task
- **MEMORY_dalamud-ai-policy.md** — Plugin submission requirements
- **MEMORY_user-learning-style.md** — How to explain things to this developer

See [MEMORY_INDEX.md](MEMORY_INDEX.md) for quick lookup.

---

## Critical Context

### The Prime Directive

TimeMemoria is **strictly quest-pacing focused**. Never:
- Add DPS, HPS, or combat metrics
- Read combat logs or duty results
- Create overlays, alerts, or notifications
- Automate any gameplay
- Add chat commands or input injection

The plugin lives in FFXIV's process memory. Anything you add could get the plugin banned.

### Architecture

**data.json**
- Navigation skeleton (always loaded)
- All quest arrays empty (no data)
- Defines expansion/patch hierarchy

**Bucket Files**
- Actual quest content (lazy-loaded on demand)
- Path: `{expansion}.x/{patch}/{patch_no_dot}-{type}.json`
- Unloaded when user navigates away (GC pressure matters)

**Why?** Plugins can't afford to load 6,200 quests at startup. The two-file system keeps memory pressure low.

### Current State

- **Version**: 14.2.0.1 (Dalamud API 14, FFXIV 7.4–7.49)
- **Data**: ARR 2.x complete + levequests (1,774 total)
- **Uncommitted**: 390 files (quest JSONs validated, decide: stash, commit, or continue)
- **Next**: API 15 migration (version becomes 15.x.x.x) when FFXIV 7.5 releases

---

## Repository

```bash
git clone https://github.com/LegendsOfTheGame/TimeMemoriaV2.git
cd TimeMemoriaV2
dotnet build
```

---

## Key Files to Know

**C# Core**:
- `TimeMemoria/Configuration.cs` — User settings and plugin config
- `TimeMemoria/QuestData.cs` — Quest data model
- `TimeMemoria/QuestDataManager.cs` — Loading and processing engine
- `TimeMemoria/MainWindow.cs` — UI
- `TimeMemoria/PlaytimeStatsService.cs` — Pacing metrics

**Data**:
- `TimeMemoria/Quests/data.json` — Navigation skeleton
- `TimeMemoria/Quests/toc.json` — MSQ progression gates
- `TimeMemoria/Quests/Levequests/` — 10 bucketed levequest files
- `TimeMemoria/Quests/{x}.x/{patch}/` — Quest buckets by expansion

---

## Common Tasks

### Adding a new quest type
See MEMORY_levequest-integration.md for the pattern. The plugin uses:
- QuestData class (model)
- Bucket files (data)
- LoadBucketIfNeeded() (lazy loader)
- UpdateQuestData() (completion tracking)

### Debugging JSON deserialization
See MEMORY_levequest-fix.md. Check:
1. All fields are non-nullable or marked optional (`?`)
2. JSON values match C# types
3. Run test locally before committing

### Understanding scope
Always check MEMORY_prime-constraints.md first. If uncertain, DON'T add it without asking.

---

## Development Rules

1. **Test locally first** — push clean milestones only
2. **GC pressure matters** — keep plugin memory-conscious
3. **Explain thoroughly** — user learns code like a language
4. **Preserve lazy loading** — don't load all quests at startup
5. **Document constraints** — if things change, update docs
6. **Never assume scope** — ask before inventing features

---

## External Resources

- **Dalamud Docs**: https://dalamud.dev
- **Quest Data**: https://v2.xivapi.com/api/sheet/Quest/{quest_id}
- **Plugin Repo**: https://github.com/goatcorp/DalamudPluginsD17
- **Discord**: https://discord.gg/3NMcUV5 (Dalamud support)

---

## Questions?

1. Read CODEX_HANDOFF.md
2. Check MEMORY_INDEX.md for topic-specific files
3. Review relevant commit messages (last 5 commits document recent work)
4. Check dalamud.dev for Dalamud API questions

---

**Last Updated**: June 7, 2026  
**For**: Codex or compatible IDE/tool  
**Developer**: Haven Duce (LegendsOfTheGame)
