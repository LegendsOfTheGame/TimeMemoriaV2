#!/usr/bin/env python3
import json
from pathlib import Path

def empty_quests(obj):
    """Recursively empty all Quests arrays"""
    if isinstance(obj, dict):
        if "Quests" in obj and isinstance(obj["Quests"], list):
            obj["Quests"] = []

        for value in obj.values():
            empty_quests(value)

    elif isinstance(obj, list):
        for item in obj:
            empty_quests(item)

def main():
    data_path = Path(__file__).parent / "TimeMemoria" / "data.json"

    with open(data_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    empty_quests(data)

    with open(data_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=4, ensure_ascii=False)

    print("All Quests arrays emptied in data.json")

if __name__ == "__main__":
    main()
