---
name: TimeMemoria Prime Constraints
description: Non-negotiable architectural boundaries for quest-pacing plugin
type: project
originSessionId: f01e2674-dc2d-4c4e-b67e-b2debfb3163c
---

## The Prime Directive (Non-Negotiable)

TimeMemoria is **strictly quest-pacing focused**. It is NOT a performance analysis tool.

### Prohibited Absolutely
- Combat logs, DPS, HPS, damage statistics
- Duty results, wipes, boss names, raid tier info
- ACT/FFLogs-style data or export
- Performance metrics or optimization framing
- Automation or UI input injection
- Chat commands, /echo, toasts, overlays, background gameplay automation
- Alerts, timers, reminders, notifications
- Rankings, thresholds, "good/bad" language
- Any data suitable for parsing as combat events

### Permitted ONLY
- Elapsed playtime (session + lifetime)
- Passive quest completion metadata
- Descriptive, observational pacing metrics only
- XIV ToDo world-state (maintenance, patch status, seasonal events)
- No player-specific or character-specific world events

## UI-Only Constraint

**ALL** interaction happens strictly within plugin UI windows. Period.
- Quest Window (read-only quest browser)
- News/Events Window (XIV ToDo + global pacing lines)
- Configuration Window (settings only)

Nothing outside these windows.

## Pacing Standard (Ethical)

If pacing metrics exist, they MUST be:
- **Descriptive only** (show what happened, not what's good/bad)
- **Observational only** (never imply skill, efficiency, or quality)
- **Contextual only** (MSQ pacing accounts for cutscene-heavy story, that's it)

NO:
- Optimization language
- Performance implications
- Comparative framing
- Skill judgments

## MSQ vs Non-MSQ Pacing

- OFF by default (critical)
- Framed ONLY as cutscene-driven narrative context
- Never as optimization or progress comparison
- MSQ = quests in 1-msq.json only

## Scope Boundary: No Expansion Without Explicit Request

Do not expand scope beyond what is documented and explicitly requested.
Do not invent new modules, systems, or data structures.
Do not generalize from other plugins unless aligned with these docs.

## Documentation First

For API15 migration:
1. Uploaded docs (Prime Directive, Foundation, Versioning, Project Structure) = authoritative baseline
2. dalamud.dev (official Dalamud docs) = next priority
3. Never let general web content override specific TimeMemoria constraints
4. When uncertain, ask before assuming or inventing

## Versioning Impact for API15

Current: **14.2.0.1**
- 14 = Dalamud API 14 (FFXIV 7.4–7.49)
- 2 = ARR expansion focus
- 0 = ARR 2.0 patch band
- 1 = MSQ bucket only

After API15 migration: **15.2.0.1**
- 15 = Dalamud API 15 (FFXIV 7.5+)
- Rest unchanged (ARR focus, 2.0, MSQ bucket)

The AA digit changes; nothing else.
