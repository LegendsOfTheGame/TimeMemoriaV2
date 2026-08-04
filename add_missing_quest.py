#!/usr/bin/env python3
import json
from pathlib import Path

def main():
    # Load backup to get the quest object
    backup_path = Path(__file__).parent / "TimeMemoria" / "data.json.backup"
    with open(backup_path, 'r', encoding='utf-8') as f:
        backup_data = json.load(f)

    # Find quest 67500
    quest_67500 = None

    def find_quest(obj):
        nonlocal quest_67500
        if isinstance(obj, dict):
            if "Id" in obj and (obj["Id"] == 67500 or (isinstance(obj["Id"], list) and 67500 in obj["Id"])):
                quest_67500 = obj
                return True

            for value in obj.values():
                if find_quest(value):
                    return True

        elif isinstance(obj, list):
            for item in obj:
                if find_quest(item):
                    return True
        return False

    find_quest(backup_data)

    if not quest_67500:
        print("Quest 67500 not found in backup!")
        return

    print(f"Found quest: {quest_67500.get('Title', 'Unknown')}")

    # Load 3.0-Other.json
    bucket_path = Path(__file__).parent / "TimeMemoria" / "Quests" / "3.x" / "3.0-Other.json"

    with open(bucket_path, 'r', encoding='utf-8') as f:
        quests = json.load(f)

    # Add quest
    quests.append(quest_67500)

    # Save
    with open(bucket_path, 'w', encoding='utf-8') as f:
        json.dump(quests, f, indent=2, ensure_ascii=False)

    print(f"Added quest 67500 to 3.0-Other.json ({len(quests)} total quests)")

if __name__ == "__main__":
    main()
