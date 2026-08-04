# TimeMemoria Memory Files Index

These files were extracted from Claude Code's persistent memory system and included in the repository for easy reference during the transition to Codex or another IDE.

## Files

### CODEX_HANDOFF.md (START HERE)
**The main handoff document.** Read this first for complete context transfer including:
- Project overview and critical directives
- Architecture summary
- Build instructions
- Current state (390 modified files)
- Development principles
- All constraints and policy requirements

---

### MEMORY_prime-constraints.md
**Non-negotiable architectural and ethical boundaries.**
- What IS allowed: quest tracking, pacing metrics, descriptive only
- What is NOT allowed: DPS, combat logs, automation, overlays, alerts, rankings
- UI constraint: all interaction strictly within plugin windows
- Versioning scheme for API migrations

**Use when**: Making scope decisions, adding features, considering UI changes

---

### MEMORY_project-architecture.md
**Core design and data structure documentation.**
- Two-file system (data.json + lazy-loaded buckets)
- Lazy loading strategy
- Completion tracking (from game memory)
- Data coverage status (quest patches, levequest data)
- External tools and resources

**Use when**: Understanding how data flows, why architecture exists, how to add new quest types

---

### MEMORY_levequest-integration.md
**Completed levequest integration (1,774 quests).**
- How levequests were integrated without breaking existing system
- Bucket organization (ARR split by region, expansions single file)
- Lazy loading with NPC hierarchy preservation
- Code modifications in QuestData.cs and QuestDataManager.cs
- Verification steps

**Use when**: Adding similar bulk data, understanding lazy loading patterns, extending quest types

---

### MEMORY_levequest-fix.md
**Fix for levequest JSON deserialization.**
- Problem: All 1,774 levequests had null Level fields
- Solution: Populated with wiki-provided opening levels
- NPC mapping for all 32 NPCs across 6 expansions
- Commit reference: 3dceb97

**Use when**: Debugging JSON issues, understanding data validation

---

### MEMORY_arr-2-0-migration.md
**Next planned task: move ARR 2.0 sidequests from data.json to lazy-loaded buckets.**
- Current state: ~200+ sidequests hardcoded in data.json
- Next: Extract to 2.0-hildibrand.json, 2.0-zodiac.json, etc.
- Naming convention: x.y-{type}.json
- Steps to implement

**Use when**: Working on ARR 2.0 sidequest migration

---

### MEMORY_dalamud-ai-policy.md
**Official Dalamud plugin repository submission requirements.**
- AI disclosure levels (None, Hint, Assist, Pair, Copilot, Auto)
- Current status: "Assist" level (human-led, AI acts on specific tasks)
- Testing and verification requirements
- Enforcement: undisclosed AI use = ban, all AI-generated = auto-reject

**Use when**: Planning plugin submission, understanding disclosure requirements

---

### MEMORY_user-learning-style.md
**Developer's learning approach.**
- Learns code like learning a new language
- Needs comprehensive explanations, not shorthand
- Wants to understand WHY, not just get working code
- Teaching mode: explain concepts, show syntax, use analogies

**Use when**: Writing explanations, documenting code changes, planning education/teaching

---

## Quick Reference

| Need | File |
|------|------|
| Overall project context | CODEX_HANDOFF.md |
| What we can/cannot do | MEMORY_prime-constraints.md |
| How data is organized | MEMORY_project-architecture.md |
| How levequests work | MEMORY_levequest-integration.md |
| Bug fixing levequests | MEMORY_levequest-fix.md |
| Next work task | MEMORY_arr-2-0-migration.md |
| Submission requirements | MEMORY_dalamud-ai-policy.md |
| How to explain things | MEMORY_user-learning-style.md |

---

## How to Use These Files

1. **First session**: Read CODEX_HANDOFF.md completely
2. **Architecture questions**: Refer to MEMORY_project-architecture.md + MEMORY_prime-constraints.md
3. **Feature work**: Check MEMORY_prime-constraints.md to ensure scope is allowed
4. **Data work**: Read MEMORY_levequest-integration.md to understand patterns
5. **Next task**: MEMORY_arr-2-0-migration.md documents the planned work
6. **Submissions**: MEMORY_dalamud-ai-policy.md for repository requirements

---

## Important Notes

- **390 uncommitted files** in working tree (mostly quest JSON buckets)
- **Repository**: https://github.com/LegendsOfTheGame/TimeMemoriaV2.git
- **Current version**: 14.2.0.1 (Dalamud API 14, FFXIV 7.4–7.49)
- **Next migration**: Bump to 15.x.x.x when FFXIV 7.5 launches (no logic changes)
- **GC pressure matters**: This is a plugin in the game process memory, not a standalone app

---

Generated: June 7, 2026  
For transfer to: Codex or compatible IDE
