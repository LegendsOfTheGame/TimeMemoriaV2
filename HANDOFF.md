# TimeMemoria V2 — Session Handoff

**Project:** TimeMemoria V2 (locally renamed "Legends Tracker") — a Dalamud plugin for FFXIV  
**Repo:** `D:\Github\TimeMemoriaV2`  
**Plugin project:** `TimeMemoria\TimeMemoria.csproj`

---

## What This Project Does

Tracks FFXIV quest completion across MSQ, Chronicles of a New Era (raid story chains), beast tribes, and sidequests. Data is split across:

- **`TimeMemoria/data.json`** — embedded assembly resource; hierarchical category tree. Inline `Quests` arrays are being progressively cleared and replaced with bucket files.
- **`TimeMemoria/Quests/toc.json`** — per-patch metadata: `Start`/`Final` IDs for MSQ, plus per-chain `{Chain}Start`/`{Chain}Final` role entries for gating.
- **Bucket files** — `TimeMemoria/Quests/{expansion}/{patch}/{patchnum}-{category}.json` — flat JSON arrays loaded on-demand via `LoadBucketDirect()`. The category token for Chronicles chains is `NewEra`.

---

## Quest Schema

```json
{ "Title": "...", "Id": [uint], "Area": "...", "Level": int, "Chain": "string" }
```

The `Chain` field groups quests by raid series inside a shared bucket file. The UI renders chain-grouped section headers in the NewEra quest list.

---

## CRITICAL NOTES FOR NEXT SESSION

- **Recommended model: Haiku** — next session is purely mechanical file edits, no web lookups needed
- **data.json started at 6701 lines, now ~5480 lines** — the skeleton shells remain permanently
- **The Quests tab ONLY reads from bucket files** — inline data.json quests are invisible in the main UI. Only the legacy Overview tab reads inline quests.
- **Always append with Edit, never rewrite whole files** — use Write only for empty/new files
- **Always trust the user's patch labels** — do not rely on HANDOFF patch assignments, the user will specify the correct patch per quest
- **7.4 is not yet programmed** — toc.json has a 7.4 block but MSQ Start/Final are blank. This needs filling before 7.5 releases (~21 days from session date 2026-04-13)

---

## Chronicles of a New Era — Migration Status

### COMPLETED ✅ (ALL DONE)

| Expansion | Chain | Bucket Files | data.json | toc.json |
|---|---|---|---|---|
| ARR 2.x | Primals | ✅ | ✅ | ✅ PrimalStart/Final |
| ARR 2.x | Binding Coil of Bahamut | ✅ | ✅ | ✅ BahamutStart/Final |
| ARR 2.x | Crystal Tower | ✅ | ✅ | ✅ CrystalTowerStart/Final |
| HW 3.x | Alexander | ✅ | ✅ | ✅ AlexanderStart/Final |
| HW 3.x | Warring Triad | ✅ | ✅ | ✅ WarringTriadStart/Final |
| HW 3.x | Shadow of Mhach | ✅ | ✅ | ✅ ShadowOfMhachStart/Final |
| SB 4.x | Omega | ✅ | ✅ | ✅ OmegaStart/Final |
| SB 4.x | Return to Ivalice | ✅ | ✅ | ✅ IvaliceStart/Final |
| SB 4.x | Four Lords | ✅ | ✅ | ✅ FourLordsStart/Final |
| ShB 5.x | Eden | ✅ | ✅ | ✅ EdenStart/Final |
| ShB 5.x | YoRHa: Dark Apocalypse | ✅ | ✅ | ✅ YoRHaStart/Final |
| ShB 5.x | Sorrow of Werlyt | ✅ | ✅ | ✅ WerlytStart/Final |
| EW 6.1 | Omega (Beyond the Rift) | ✅ | ✅ | ✅ OmegaStart/Final |
| EW 6.x | Pandæmonium | ✅ | ✅ | ✅ PandaemoniumStart/Final |
| EW 6.x | Myths of the Realm | ✅ | ✅ | ✅ MythsStart/Final |
| DT 7.x | Arcadion | ✅ | ✅ | ✅ ArcadionStart/Final |
| DT 7.x | Echoes of Vana'diel | ✅ | ✅ | ✅ VanadielStart/Final |

### Arcadion bucket breakdown
- `7.x/7.0/70-NewEra.json` — 6 quests (70496–70501)
- `7.x/7.2/72-NewEra.json` — 7 quests (70825–70830, 70971 "Feral Fandom")
- `7.x/7.4/74-NewEra.json` — 5 quests (70972–70976) ← NEW, added this session

### Echoes of Vana'diel bucket breakdown
- `7.x/7.1/71-NewEra.json` — Pandæmonium epilogue (70788) + 4 Vana'diel quests (70769–70772)
- `7.x/7.3/73-NewEra.json` — 4 quests (70862–70865) ← second batch is 7.3, NOT 7.2

---

## Next Session — Levequests

User will provide Levequest data. Key facts:
- **Levequests are NOT in data.json** — this is entirely new data to be added
- Bucket file category token is `Leve` → files named `{patchnum}-Leve.json`
- toc.json roles: `LeveStart` / `LeveFinal` per patch (stubs already exist with empty Ids)
- Quest schema is the same: `{ "Title": "...", "Id": [uint], "Area": "...", "Level": int, "Chain": "string" }`
- User will provide quests with patch labels — trust those labels, do not assume
- data.json will need a `"Leve Quests"` category shell added if not already present

### Workflow (same 3-step pattern)
1. Write/append quests to `{patchnum}-Leve.json` bucket files
2. Ensure data.json has the shell category entry with `"Quests": []`
3. Add `LeveStart`/`LeveFinal` to toc.json per patch

---

## UI Changes Made This Session

- **Level column** (36px fixed) — appears left of Title in quest list rows, shows `quest.Level` dimmed
- **Area column** (140px fixed, optional) — appears right of Title, toggled via Settings
- **Configuration.cs** — added `public bool ShowQuestArea { get; set; } = true;`
- **Settings → Quest Browser** — "Show area column in quest list" checkbox added

---

## Build Command

```
dotnet build D:\Github\TimeMemoriaV2\TimeMemoria\TimeMemoria.csproj
```

If build cache issues: `dotnet clean` first.

---

## Urgent Before 7.5 (releases ~2026-05-04)

- [ ] Fill in `7.4` MSQ `Start`/`Final` in toc.json (currently blank)
- [ ] Add 7.4 MSQ quests to data.json / bucket files
- [ ] Add 7.3 MSQ `Final` to toc.json (currently missing)
- [ ] Begin 7.5 data prep
