#!/usr/bin/env python3
import json
import csv
from pathlib import Path
from collections import defaultdict

def main():
    # Read the categorized CSV
    csv_path = Path(__file__).parent / "quest_patches_categorized.csv"

    # Load data.json to get full quest objects
    data_path = Path(__file__).parent / "TimeMemoria" / "data.json"
    with open(data_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    # Create a map of quest ID -> quest object from data.json
    quest_objects = {}

    def extract_quest_objects(obj):
        if isinstance(obj, dict):
            if "Quests" in obj and isinstance(obj["Quests"], list):
                for quest in obj["Quests"]:
                    if isinstance(quest, dict) and "Id" in quest:
                        quest_id = quest["Id"][0] if isinstance(quest["Id"], list) else quest["Id"]
                        quest_objects[quest_id] = quest

            for value in obj.values():
                extract_quest_objects(value)
        elif isinstance(obj, list):
            for item in obj:
                extract_quest_objects(item)

    extract_quest_objects(data)
    print(f"Found {len(quest_objects)} quest objects in data.json")

    # Read CSV and organize by patch and bucket type
    buckets = defaultdict(list)

    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            quest_id = int(row['ID#'])
            patch = row['Patch#']
            bucket_type = row['BucketType']

            if patch == "ERROR" or bucket_type == "UNKNOWN":
                print(f"Skipping quest {quest_id} - patch={patch}, type={bucket_type}")
                continue

            # Truncate patch to first decimal (7.25 -> 7.2)
            try:
                patch_float = float(patch)
                major = int(patch_float)
                minor = int((patch_float - major) * 10)
                truncated_patch = f"{major}.{minor}"
            except:
                print(f"Skipping quest {quest_id} - invalid patch: {patch}")
                continue

            bucket_key = f"{truncated_patch}-{bucket_type}"

            if quest_id in quest_objects:
                buckets[bucket_key].append(quest_objects[quest_id])
            else:
                print(f"Warning: Quest {quest_id} not found in data.json")

    # Create bucket files
    quests_dir = Path(__file__).parent / "TimeMemoria" / "Quests"

    for bucket_key in sorted(buckets.keys()):
        quests = buckets[bucket_key]

        # Determine subdirectory based on patch (2.0 -> 2.x, 3.5 -> 3.x, etc.)
        parts = bucket_key.split('-')
        patch_part = parts[0]  # e.g., "2.0"
        major = patch_part.split('.')[0]
        expansion_dir = quests_dir / f"{major}.x"

        expansion_dir.mkdir(parents=True, exist_ok=True)

        # Create bucket file
        bucket_file = expansion_dir / f"{bucket_key}.json"
        with open(bucket_file, 'w', encoding='utf-8') as f:
            json.dump(quests, f, indent=2, ensure_ascii=False)

        print(f"Created {bucket_key}.json with {len(quests)} quests")

    print("\nDone! Now you need to manually empty all Quests arrays in data.json")

if __name__ == "__main__":
    main()
