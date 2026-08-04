#!/usr/bin/env python3
import json
import requests
from pathlib import Path
import sys

def extract_quests_recursive(obj, quests_list):
    """Recursively extract all quests from data.json structure"""
    if isinstance(obj, dict):
        # Check if this is a quest object
        if "Id" in obj and "Title" in obj:
            quests_list.append({
                "id": obj["Id"][0] if isinstance(obj["Id"], list) else obj["Id"],
                "name": obj["Title"]
            })

        # Recurse into all values
        for value in obj.values():
            extract_quests_recursive(value, quests_list)

    elif isinstance(obj, list):
        for item in obj:
            extract_quests_recursive(item, quests_list)

def get_quest_patch(quest_id):
    """Look up patch info from Garland Tools"""
    try:
        url = f"https://www.garlandtools.org/db/doc/quest/en/2/{quest_id}.json"
        response = requests.get(url, timeout=5)
        if response.status_code == 200:
            data = response.json()
            if data and "quest" in data and "patch" in data["quest"]:
                patch = data["quest"]["patch"]
                return patch
        return "UNKNOWN"
    except Exception as e:
        return "ERROR"

def main():
    # Fix Unicode encoding on Windows
    sys.stdout.reconfigure(encoding='utf-8')

    # Read data.json
    data_path = Path(__file__).parent / "TimeMemoria" / "data.json"

    with open(data_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    # Extract all quests
    quests = []
    extract_quests_recursive(data, quests)

    # Remove duplicates (keep first occurrence)
    seen_ids = set()
    unique_quests = []
    for quest in quests:
        if quest["id"] not in seen_ids:
            seen_ids.add(quest["id"])
            unique_quests.append(quest)

    print(f"Found {len(unique_quests)} unique quests")
    print("ID#, Patch#, Name")
    print("-" * 80)
    sys.stdout.flush()

    # Look up patch for each quest and output
    for i, quest in enumerate(unique_quests, 1):
        patch = get_quest_patch(quest["id"])
        print(f"{quest['id']}, {patch}, {quest['name']}")
        sys.stdout.flush()

        # Progress indicator
        if i % 10 == 0:
            print(f"# Progress: {i}/{len(unique_quests)}", file=sys.stderr, flush=True)

if __name__ == "__main__":
    main()
