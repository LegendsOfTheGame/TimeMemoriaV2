# Time Memoria v2

A FFXIV Dalamud plugin for quest progression and pacing — a reflective notebook for your story journey, not a scoreboard.

> ⚠️ This plugin **cannot** be used for ACT/FFLogs-style performance analysis.
> It does not track, read, or evaluate combat performance in any way.

For suggestions or issues, feel free to open an issue or discussion on this repository.

---

## Overview

Time Memoria v2 is a quest-centric Dalamud plugin. It helps you understand your quest
progression and pacing over time — how long you've spent, where you are in the story,
and what's happening in the world of FFXIV right now.

All primary interaction occurs within the plugin UI. A future readonly summary command is planned but not yet implemented.
Suggestions can be submitted via [Issues](https://github.com/LegendsOfTheGame/TimeMemoriaV2/issues).



---

## Features

- **Quest Browser** — A readonly, filterable journal of available and completed quests,
  organized by expansion, patch, and category (MSQ, New Era, Feature, Beasts, Class/Job,
  Seasonal, Levequests, and more).
- **News & Events** — A world-state panel showing current FFXIV maintenance windows,
  patch status, and seasonal events, alongside your global pacing lines.
- **Pacing Stats** — Descriptive session and lifetime minutes-per-quest averages.
  Observational only — no rankings, no thresholds, no judgments.

---

## Module State (v14.2.0.1)

| Module | Status |
|---|---|
| Quest UI (QuestTracker evolution) | ✅ Active |
| News & Events (XIV ToDo + pacing) | ✅ Active |
| PlaytimeStats Pacing (session/lifetime) | ✅ Active |
| Levequests & Guildhests tracking | 🔜 Planned |
| Ocean Fishing helper | 🔜 Future |

---

## Versioning

Time Memoria uses the format `AA.B.C.D`:
- **AA** — Dalamud API version
- **B** — Expansion band (2 = ARR, 3 = HW, …)
- **C** — Patch within that band (0–5)
- **D** — Number of quest buckets complete for that band/patch

Current version: **14.2.0.1** — Dalamud API 14, ARR 2.0, MSQ bucket complete.

---

## Credits

- **isaiahcat** — [QuestTracker](https://github.com/isaiahcat/QuestTracker), the structural
  foundation and inspiration for Time Memoria
- **Infiziert90** — [BetterPlaytime](https://github.com/Infiziert90/BetterPlaytime),
  inspiration for the time-per-quest pacing concept
- **Haselnussbomber** — [LeveHelper](https://github.com/Haselnussbomber/LeveHelper),
  inspiration for the planned Levequests & Guildhests tracking

---

## Compliance

Time Memoria v2 is strictly a quest progression and pacing tool.

It does **not**:
- Read or display DPS, HPS, deaths, wipes, or duty results
- Integrate with ACT, FFLogs, or any combat log format
- Send toasts, overlays, or notifications
- Issue chat commands (a future readonly summary command is planned; see Overview)
- Automate any in-game interaction

All saved data is limited to quest IDs, completion counts, timestamps, and pacing
aggregates. Nothing it stores can be repurposed as a combat log.
