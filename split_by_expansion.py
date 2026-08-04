#!/usr/bin/env python3
import csv
from pathlib import Path
from collections import defaultdict

# Read the sorted CSV
input_file = Path(__file__).parent / "quest_patches_sorted.csv"
output_dir = Path(__file__).parent / "quests_by_expansion"
output_dir.mkdir(exist_ok=True)

# Group quests by expansion
expansions = defaultdict(list)

with open(input_file, 'r', encoding='utf-8') as f:
    reader = csv.DictReader(f)
    for row in reader:
        patch = row['Patch#']

        # Determine expansion from patch
        if patch == "ERROR":
            expansion = "ERROR"
        else:
            try:
                major = int(float(patch))
                expansion = f"{major}.x"
            except:
                expansion = "UNKNOWN"

        expansions[expansion].append(row)

# Write separate files for each expansion
for expansion in sorted(expansions.keys()):
    output_file = output_dir / f"{expansion}_quests.csv"
    quests = expansions[expansion]

    with open(output_file, 'w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=['ID#', 'Patch#', 'Name'])
        writer.writeheader()
        writer.writerows(quests)

    print(f"{expansion}: {len(quests)} quests -> {output_file}")

print("\nDone!")
