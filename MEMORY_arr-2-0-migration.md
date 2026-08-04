---
name: arr-2-0-sidequest-migration
description: Plan and current state of moving ARR 2.0 sidequests from data.json to lazy-loaded buckets
metadata: 
  node_type: memory
  type: project
  originSessionId: b5ff6b84-e7e2-4e6c-a33f-bf031e96ac34
---

## Current State (2026-06-04)

### Hardcoded in data.json (Central Hub)

All of these are currently embedded inline in data.json and need to be moved to lazy-loaded bucket files:

**Level 50 Content (2.0 era):**
- Hildibrand Adventures (21 quests, lines 189-212)
- Further Hildibrand Adventures (8 quests, level 60, lines 214-224) 
- Even Further Hildibrand Adventures (9 quests, level 70, lines 227-238)
- Somehow Further Hildibrand Adventures (10 quests, level 90, lines 241-253)
- Manderville Weapons (8 quests, level 90, lines 256-266)
- Inconceivably Further Hildibrand Adventures (3 quests, level 100, lines 269-274)

- Zodiac Weapons (13 quests, level 50, lines 283-297)
- Delivery Moogle Quests (25 quests, level 50, lines 550-575)
- Lominsan Sidequests (30+ quests, mixed levels 1-60, lines 623-653)
- And more regional sidequests (Gridania, Ul'dah, etc.)

### Bucket File Naming Convention

When moving to lazy-loaded files, use:
- `2.0-hildibrand.json`
- `2.0-zodiac.json`
- `2.0-sidequest.json`
- `2.0-{type}.json` format (matching x.y-{type} pattern used elsewhere)

### Next Steps

When ready to migrate:
1. Extract each category into separate `2.0-{type}.json` bucket files
2. Update data.json structure to add `BucketPath` instead of inline `Quests`
3. Update QuestDataManager to lazy-load these new sidequest buckets
4. Follow existing lazy-load patterns (see [[levequest-integration]])
