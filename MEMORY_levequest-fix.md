---
name: Levequest Opening Levels Fix
description: Fixed JSON deserialization error by populating all levequest Level fields with opening levels
type: project
originSessionId: 8c8f8a68-001d-446f-8149-5d59e314ef4e
---

## Issue Fixed

**Error**: `Newtonsoft.Json.JsonSerializationException: Error converting value {null} to type 'System.Int32'. Path 'Categories[0].Categories[0].Quests[0].Level'`

**Root Cause**: All 1,774 levequest entries had `"Level": null` in JSON, but the C# `Quest.Level` property was non-nullable `int`.

## Solution Completed

Populated all levequest Level fields with wiki-provided opening levels for 32 NPCs across all expansions:

- **ARR**: 844 quests across 5 region files (Coerthas, Gridania, Limsa Lominsa, Mor Dhona, Ul'dah)
- **HW**: 360 quests (Eloin - Level 50)
- **SB**: 165 quests (Keltraeng - Level 60)  
- **ShB**: 165 quests (Eirikur - Level 70)
- **EW**: 120 quests (Grigge - Level 80)
- **DT**: 120 quests (Malihali - Level 90)

**Total Updated**: 1,774 quests

## NPC Mapping (ARR)
- Level 1: Gontrant, Muriaule, T'mokkri, Wyrkholsk, Eustace, Graceful Song
- Level 10: Tierney, Swygskyf, Totonowa
- Level 15: Qina Lyehga, Orwen, Poponagu
- Level 20: Cedrepierre, Nyell, Ourawann, Eugene, Kikiri
- Level 25: Esmond
- Level 30: Merthelin, H'amneko, Nahctahr, C'lafumyn, Blue Herring
- Level 35: Aileen, Cimeaurant, Haisie
- Level 40: Rurubana, Voilinaut, Lodille
- Level 45: K'leytai, Eidhart

## Commit
- **Commit**: 3dceb97 - "fix: populate levequest opening levels from wiki data"
- **Files Modified**: 10 JSON files
- **Insertions**: 14,570 lines

## Verification
All 1,774 levequests now have valid Level values. No null values remain in any levequest JSON file.
