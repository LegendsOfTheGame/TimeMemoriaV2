import requests

BASE_URL = "https://xivapi.com"

def get_leve(row_id):
    """Get full leve data by row ID"""
    url = f"{BASE_URL}/Leve/{row_id}"
    response = requests.get(url)
    data = response.json()
    return data

# Check specific leve IDs the user mentioned
leve_ids = [501, 503, 504, 506, 537, 539, 520, 521, 522, 524, 541, 543]

print("Querying specific levequest IDs:")
for leve_id in leve_ids:
    leve = get_leve(leve_id)
    if 'ID' in leve:
        print(f"\nID {leve['ID']}: {leve.get('Name', 'Unknown')}")
        print(f"  Town ID: {leve.get('Town', {}).get('ID', 'Unknown')}")
        print(f"  Level: {leve.get('ClassJobLevel', 'Unknown')}")
    else:
        print(f"\nID {leve_id}: Not found or invalid")
