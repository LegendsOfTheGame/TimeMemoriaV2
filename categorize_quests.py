#!/usr/bin/env python3
import json
from pathlib import Path
import csv

def extract_quests_with_categories(obj, quests_map, current_category=""):
    """Recursively extract quests and their categories from data.json"""
    if isinstance(obj, dict):
        # Check if this is a category object (has Title and either Categories or Quests)
        has_title = "Title" in obj
        has_categories = "Categories" in obj
        has_quests = "Quests" in obj

        if has_title and (has_categories or has_quests):
            title = obj.get("Title", "")
            new_category = current_category + " > " + title if current_category else title

            # Process quests directly in this category
            if has_quests and isinstance(obj["Quests"], list):
                for quest in obj["Quests"]:
                    if isinstance(quest, dict) and "Id" in quest and "Title" in quest:
                        quest_id = quest["Id"][0] if isinstance(quest["Id"], list) else quest["Id"]
                        bucket_type = categorize(new_category, quest.get("Title", ""))
                        quests_map[quest_id] = {
                            "name": quest["Title"],
                            "full_category": new_category,
                            "bucket_type": bucket_type
                        }

            # Recurse into subcategories
            if has_categories and isinstance(obj["Categories"], list):
                for subcategory in obj["Categories"]:
                    extract_quests_with_categories(subcategory, quests_map, new_category)
        else:
            # If not a category, still recurse into values
            for value in obj.values():
                extract_quests_with_categories(value, quests_map, current_category)

    elif isinstance(obj, list):
        for item in obj:
            extract_quests_with_categories(item, quests_map, current_category)

def categorize(category_path, quest_name):
    """Map quest to bucket type based on category"""
    cat_lower = category_path.lower()

    # Chronicles of a New Era
    if "chronicles of a new era" in cat_lower:
        return "Feature"

    # Hildibrand
    if "hildibrand" in cat_lower:
        return "Other"

    # Weapon Enhancement (Zodiac, Anima, Eureka, Resistance, Phantom)
    if "weapon enhancement" in cat_lower or "zodiac" in cat_lower or "anima" in cat_lower or "eureka" in cat_lower or "resistance" in cat_lower or "phantom" in cat_lower:
        return "Other"

    # Records of Unusual Endeavors (various)
    if "records of unusual endeavors" in cat_lower:
        return "Other"

    # Side Story Quests
    if "side story" in cat_lower or "delivery moogle" in cat_lower or "scholasticate" in cat_lower:
        return "Other"

    # Regional sidequests (Limsan, Gridania, Ul'dah, etc.)
    if any(region in cat_lower for region in ["lominsan", "gridania", "ul'dah", "limsa lominsa", "middle la noscea", "lower la noscea"]):
        return "Other"

    # Chronicles of Light
    if "chronicles of light" in cat_lower or "tales of" in cat_lower:
        return "Other"

    # Job/Class quests
    if "job quest" in cat_lower or "class quest" in cat_lower:
        return "Class"

    # Leves
    if "leve" in cat_lower:
        return "Leve"

    # Beast tribes
    if "beast tribe" in cat_lower:
        return "Beasts"

    # Seasonal
    if "seasonal" in cat_lower or "event" in cat_lower:
        return "Seasonal"

    # Default to Other
    return "Other"

def main():
    # Read data.json
    data_path = Path(__file__).parent / "TimeMemoria" / "data.json"
    with open(data_path, 'r', encoding='utf-8') as f:
        data = json.load(f)

    # Extract quests with categories
    quests_map = {}
    extract_quests_with_categories(data, quests_map)

    print(f"Found {len(quests_map)} quests with categories")

    # Read sorted CSV and add categories
    input_file = Path(__file__).parent / "quest_patches_sorted.csv"
    output_file = Path(__file__).parent / "quest_patches_categorized.csv"

    with open(input_file, 'r', encoding='utf-8') as infile:
        reader = csv.DictReader(infile)

        with open(output_file, 'w', newline='', encoding='utf-8') as outfile:
            writer = csv.DictWriter(outfile, fieldnames=['ID#', 'Patch#', 'Name', 'BucketType', 'Category'])
            writer.writeheader()

            for row in reader:
                quest_id = int(row['ID#'])
                bucket_type = "UNKNOWN"
                category = "UNKNOWN"

                if quest_id in quests_map:
                    bucket_type = quests_map[quest_id]['bucket_type']
                    category = quests_map[quest_id]['full_category']

                writer.writerow({
                    'ID#': row['ID#'],
                    'Patch#': row['Patch#'],
                    'Name': row['Name'],
                    'BucketType': bucket_type,
                    'Category': category
                })

    print(f"Categorized quests saved to {output_file}")

if __name__ == "__main__":
    main()
