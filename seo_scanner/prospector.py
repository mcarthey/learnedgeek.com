#!/usr/bin/env python3
"""
SEO Prospector - LearnedGeek.com
Searches Google Places for local businesses in a given area and category,
then splits results into two lists:
  1. scanner_ready.csv  — businesses WITH websites (feed directly into seo_scanner.py)
  2. warm_leads.csv     — businesses WITHOUT websites (different sales conversation)

Usage:
  python prospector.py --location "Milwaukee, WI" --type restaurant
  python prospector.py --lat 43.0389 --lng -87.9065 --radius 5000 --type plumber
  python prospector.py --location "Waukesha, WI" --type "home services" --output ./leads

  # Single type
  python prospector.py --location "Oconomowoc, WI" --type restaurant

  # Sweep ALL small-town business types in one shot (recommended)
  python prospector.py --location "Oconomowoc, WI" --all-types

  # Custom radius
  python prospector.py --location "Oconomowoc, WI" --all-types --radius 4000

Google Places API key:
  export GOOGLE_PLACES_KEY=your_key_here
  OR pass --places-key YOUR_KEY
"""

import argparse
import csv
import os
import sys
import time
from pathlib import Path

import requests

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

GEOCODE_API       = "https://maps.googleapis.com/maps/api/geocode/json"
NEARBY_SEARCH_API = "https://maps.googleapis.com/maps/api/place/nearbysearch/json"
PLACE_DETAILS_API = "https://maps.googleapis.com/maps/api/place/details/json"

REQUEST_TIMEOUT = 15
DELAY_BETWEEN_CALLS = 0.5  # seconds — be polite to the API

# Google Place types most relevant for local SEO prospecting
# https://developers.google.com/maps/documentation/places/web-service/supported_types
COMMON_TYPES = [
    "accounting", "bakery", "bar", "beauty_salon", "car_dealer", "car_repair",
    "car_wash", "clothing_store", "dentist", "doctor", "electrician",
    "florist", "funeral_home", "gym", "hair_care", "hardware_store",
    "home_goods_store", "hospital", "insurance_agency", "jewelry_store",
    "laundry", "lawyer", "locksmith", "meal_delivery", "meal_takeaway",
    "moving_company", "painter", "pet_store", "pharmacy", "physiotherapist",
    "plumber", "real_estate_agency", "restaurant", "roofing_contractor",
    "shoe_store", "spa", "storage", "store", "veterinary_care",
]

# Curated subset for small-town sweeps — broad enough to catch most local
# businesses without pulling in schools, churches, ATMs, and other non-prospects.
# Ordered to minimize overlap so deduplication has less work to do.
SMALL_TOWN_TYPES = [
    "restaurant",        # cafes, bars, fast food, diners
    "beauty_salon",      # includes nail salons
    "hair_care",         # barbershops, salons
    "car_repair",        # auto shops, tire places
    "dentist",
    "doctor",            # physicians, chiropractors, urgent care
    "lawyer",
    "real_estate_agency",
    "insurance_agency",
    "accounting",
    "electrician",
    "plumber",
    "painter",
    "roofing_contractor",
    "florist",
    "gym",               # fitness centers, yoga studios
    "spa",               # massage, wellness
    "pet_store",         # includes groomers
    "veterinary_care",
    "pharmacy",
    "locksmith",
    "moving_company",
    "laundry",           # dry cleaners, laundromats
    "bakery",
    "jewelry_store",
    "clothing_store",
    "shoe_store",
    "hardware_store",
    "funeral_home",
    "physiotherapist",   # physical therapy
]

# ---------------------------------------------------------------------------
# API helpers
# ---------------------------------------------------------------------------

def resolve_places_key(flag_value: str) -> str:
    return flag_value or os.environ.get("GOOGLE_PLACES_KEY", "")


def geocode_location(location_str: str, api_key: str) -> tuple[float, float] | None:
    """Convert a location string like 'Milwaukee, WI' to lat/lng."""
    try:
        resp = requests.get(
            GEOCODE_API,
            params={"address": location_str, "key": api_key},
            timeout=REQUEST_TIMEOUT,
        )
        data = resp.json()
        results = data.get("results", [])
        if results:
            loc = results[0]["geometry"]["location"]
            return loc["lat"], loc["lng"]
    except Exception as e:
        print(f"  [ERROR] Geocoding failed: {e}")
    return None


def nearby_search_page(lat: float, lng: float, radius: int,
                       place_type: str, api_key: str,
                       page_token: str = "") -> dict:
    """Single page of Nearby Search results (up to 20)."""
    params = {
        "location": f"{lat},{lng}",
        "radius": radius,
        "key": api_key,
    }
    if place_type:
        params["type"] = place_type
    if page_token:
        params["pagetoken"] = page_token

    try:
        resp = requests.get(NEARBY_SEARCH_API, params=params, timeout=REQUEST_TIMEOUT)
        return resp.json()
    except Exception as e:
        print(f"  [WARN] Nearby search failed: {e}")
        return {}


def fetch_place_details_lite(place_id: str, api_key: str) -> dict:
    """
    Fetch website, hours, phone, rating, and review count for a place.
    All pulled in one Details call to minimize API usage.

    Claimed status inference:
      Google doesn't expose a direct "is_claimed" field, but unclaimed listings
      almost never have opening_hours or formatted_phone_number populated —
      those fields require owner verification to set. We use their presence
      as a reliable proxy for whether the listing has been claimed.
    """
    fields = (
        "website,opening_hours,formatted_phone_number,"
        "rating,user_ratings_total,editorial_summary"
    )
    try:
        resp = requests.get(
            PLACE_DETAILS_API,
            params={"place_id": place_id, "fields": fields, "key": api_key},
            timeout=REQUEST_TIMEOUT,
        )
        data = resp.json()
        result = data.get("result", {})

        has_hours  = bool(result.get("opening_hours"))
        has_phone  = bool(result.get("formatted_phone_number"))
        has_desc   = bool(result.get("editorial_summary", {}).get("overview"))

        # Claimed = owner has verified and actively manages the listing.
        # Unclaimed listings typically lack hours AND phone (both require owner input).
        # If either is present it's a strong signal the listing is claimed.
        claimed = has_hours or has_phone

        return {
            "website":       result.get("website", ""),
            "phone":         result.get("formatted_phone_number", ""),
            "has_hours":     has_hours,
            "has_phone":     has_phone,
            "has_desc":      has_desc,
            "claimed":       claimed,
            "rating":        result.get("rating"),
            "review_count":  result.get("user_ratings_total", 0),
        }
    except Exception as e:
        print(f"  [WARN] Place details fetch failed for {place_id}: {e}")
    return {
        "website": "", "phone": "", "has_hours": False,
        "has_phone": False, "has_desc": False, "claimed": None,
        "rating": None, "review_count": 0,
    }


# ---------------------------------------------------------------------------
# Core prospector logic
# ---------------------------------------------------------------------------

def collect_places(lat: float, lng: float, radius: int,
                   place_type: str, api_key: str) -> list[dict]:
    """
    Page through Nearby Search results (max 3 pages = 60 results per search).
    Returns raw place summaries from the search results.
    """
    places = []
    page_token = ""
    page_num = 0

    while page_num < 3:
        page_num += 1
        print(f"  Fetching page {page_num}...")

        if page_token:
            # Google requires a short delay before using a next_page_token
            time.sleep(2)

        data = nearby_search_page(lat, lng, radius, place_type, api_key, page_token)
        status = data.get("status", "")

        if status not in ("OK", "ZERO_RESULTS"):
            print(f"  [WARN] API status: {status}")
            break

        results = data.get("results", [])
        places.extend(results)
        print(f"  → {len(results)} results (running total: {len(places)})")

        page_token = data.get("next_page_token", "")
        if not page_token:
            break

    return places


def collect_all_types(lat: float, lng: float, radius: int,
                      types: list[str], api_key: str,
                      location_label: str) -> list[dict]:
    """
    Loop through multiple Place types, deduplicate by place_id as we go,
    and return a single flat list of unique raw places.
    Details calls are NOT made here — just search results.
    """
    seen_ids: set[str] = set()
    all_places: list[dict] = []

    for i, place_type in enumerate(types, 1):
        print(f"\n[{i}/{len(types)}] Searching '{place_type}'...")
        raw = collect_places(lat, lng, radius, place_type, api_key)
        new_count = 0
        for p in raw:
            pid = p.get("place_id")
            if pid and pid not in seen_ids:
                seen_ids.add(pid)
                all_places.append(p)
                new_count += 1
        dupe_count = len(raw) - new_count
        print(f"  → {new_count} new unique businesses "
              f"({dupe_count} duplicates skipped, running total: {len(all_places)})")

    return all_places


def enrich_and_split(raw_places: list[dict], api_key: str,
                     location_label: str) -> tuple[list[dict], list[dict]]:
    """
    Fetch details for each unique place and split into scanner_ready / warm_leads.
    This is called once regardless of whether we ran one type or thirty.
    """
    # Filter permanently closed up front
    active = [p for p in raw_places if p.get("business_status") != "CLOSED_PERMANENTLY"]
    skipped = len(raw_places) - len(active)
    if skipped:
        print(f"\n  (Skipping {skipped} permanently closed businesses)")

    print(f"\nFetching details for {len(active)} businesses...")
    scanner_ready: list[dict] = []
    warm_leads: list[dict] = []

    for i, place in enumerate(active, 1):
        place_id = place.get("place_id", "")
        name     = place.get("name", "")
        vicinity = place.get("vicinity", "")

        details      = fetch_place_details_lite(place_id, api_key)
        website      = details["website"]
        claimed      = details["claimed"]
        review_count = details["review_count"]
        rating       = details["rating"]
        time.sleep(DELAY_BETWEEN_CALLS)

        claimed_label = (
            "Unknown" if claimed is None
            else "Yes" if claimed
            else "NO — likely unclaimed"
        )
        priority = _warm_lead_priority(claimed, review_count, details["has_desc"])

        marker = f"{'✓' if website else '○'} {'[UNCLAIMED]' if not claimed else '':12}"
        print(f"  [{i}/{len(active)}] {marker} {name}")

        if website:
            scanner_ready.append({
                "url":           website,
                "business_name": name,
                "contact_name":  "",
                "city":          location_label,
                "place_id":      place_id,
            })
        else:
            warm_leads.append({
                "priority":        priority,
                "business_name":   name,
                "address":         vicinity,
                "city":            location_label,
                "place_id":        place_id,
                "gbp_claimed":     claimed_label,
                "phone_on_gbp":    details["phone"] or "—",
                "has_hours":       "Yes" if details["has_hours"] else "No",
                "has_description": "Yes" if details["has_desc"] else "No",
                "review_count":    review_count,
                "rating":          rating or "—",
                "google_maps_url": f"https://maps.google.com/?place_id={place_id}",
            })

    warm_leads.sort(key=lambda r: _priority_sort_key(r["priority"]))
    return scanner_ready, warm_leads


def prospect(
    lat: float,
    lng: float,
    radius: int,
    place_type: str,
    api_key: str,
    location_label: str = "",
) -> tuple[list[dict], list[dict]]:
    """Single-type prospect run."""
    print(f"\nSearching for '{place_type}' within {radius}m of {location_label or f'{lat},{lng}'}...")
    raw = collect_places(lat, lng, radius, place_type, api_key)
    if not raw:
        print("  No results found.")
        return [], []
    # Deduplicate within the single type (rare but possible across pages)
    seen: set[str] = set()
    unique = []
    for p in raw:
        pid = p.get("place_id")
        if pid and pid not in seen:
            seen.add(pid)
            unique.append(p)
    return enrich_and_split(unique, api_key, location_label)


def prospect_all_types(
    lat: float,
    lng: float,
    radius: int,
    api_key: str,
    location_label: str = "",
    types: list[str] | None = None,
) -> tuple[list[dict], list[dict]]:
    """Multi-type sweep with global deduplication — one Details call per business."""
    types = types or SMALL_TOWN_TYPES
    print(f"\nSweeping {len(types)} business types within {radius}m of {location_label or f'{lat},{lng}'}...")
    print(f"Types: {', '.join(types)}\n")
    all_unique = collect_all_types(lat, lng, radius, types, api_key, location_label)
    if not all_unique:
        print("No businesses found across any type.")
        return [], []
    print(f"\nTotal unique businesses found across all types: {len(all_unique)}")
    return enrich_and_split(all_unique, api_key, location_label)


def _warm_lead_priority(claimed: bool | None, review_count: int, has_desc: bool) -> str:
    """
    Hot   = unclaimed listing (they haven't even logged in)
    Warm  = claimed but sparse (no description, few reviews)
    Cool  = claimed and reasonably active, just no website
    """
    if not claimed:
        return "🔥 Hot — GBP likely unclaimed"
    if review_count < 5 or not has_desc:
        return "🟡 Warm — GBP claimed but sparse"
    return "🟢 Cool — GBP active, no website"


def _priority_sort_key(priority: str) -> int:
    if "Hot" in priority:
        return 0
    if "Warm" in priority:
        return 1
    return 2


# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------

SCANNER_FIELDS   = ["url", "business_name", "contact_name", "city", "place_id"]
WARM_LEAD_FIELDS = [
    "priority", "business_name", "address", "city",
    "gbp_claimed", "phone_on_gbp", "has_hours", "has_description",
    "review_count", "rating", "place_id", "google_maps_url",
]


def write_csv(path: Path, rows: list[dict], fieldnames: list[str]):
    with open(path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def print_summary(scanner_ready: list, warm_leads: list,
                  scanner_path: Path, warm_path: Path):
    total = len(scanner_ready) + len(warm_leads)
    hot  = sum(1 for r in warm_leads if "Hot"  in r.get("priority", ""))
    warm = sum(1 for r in warm_leads if "Warm" in r.get("priority", ""))
    cool = sum(1 for r in warm_leads if "Cool" in r.get("priority", ""))

    print(f"""
{'='*60}
PROSPECTING COMPLETE
{'='*60}
Total businesses found:      {total}

  With websites (scanner):   {len(scanner_ready)}
    → {scanner_path}

  Without websites (warm):   {len(warm_leads)}
    🔥 Hot  (GBP unclaimed): {hot}
    🟡 Warm (GBP sparse):    {warm}
    🟢 Cool (GBP active):    {cool}
    → {warm_path}
{'='*60}

Next steps:
  1. Run the SEO scanner on website leads:
       python seo_scanner.py --batch {scanner_path}

  2. Work the warm leads list in priority order:
     🔥 Hot  — pitch website + GBP claim in one conversation
     🟡 Warm — pitch website; offer to clean up their GBP too
     🟢 Cool — website-only pitch; their GBP is already decent
{'='*60}
""")


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="SEO Prospector — finds local businesses and prepares leads for the SEO scanner",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=f"""
Examples:
  # Sweep all small-town business types at once (recommended)
  python prospector.py --location "Oconomowoc, WI" --all-types

  # Single type
  python prospector.py --location "Milwaukee, WI" --type restaurant

  # Custom radius (meters) — default 3000 (~2 miles), good for small towns
  python prospector.py --location "Oconomowoc, WI" --all-types --radius 4000

  # By coordinates
  python prospector.py --lat 43.1005 --lng -88.4979 --all-types

  # Custom output directory
  python prospector.py --location "Oconomowoc, WI" --all-types --output ./oconomowoc-leads

Small-town type sweep includes:
  {', '.join(SMALL_TOWN_TYPES)}

Google Places API key:
  export GOOGLE_PLACES_KEY=your_key_here
  OR: python prospector.py ... --places-key AIza...
        """
    )

    loc_group = parser.add_mutually_exclusive_group(required=True)
    loc_group.add_argument("--location", help="Location name, e.g. 'Oconomowoc, WI'")
    loc_group.add_argument("--lat",      type=float, help="Latitude (use with --lng)")

    parser.add_argument("--lng",        type=float, help="Longitude (use with --lat)")
    parser.add_argument("--radius",     type=int, default=3000,
                        help="Search radius in meters (default: 3000 = ~2 miles, good for small towns)")

    type_group = parser.add_mutually_exclusive_group(required=True)
    type_group.add_argument("--type",      help="Single business type (e.g. restaurant, plumber)")
    type_group.add_argument("--all-types", action="store_true", dest="all_types",
                            help=f"Sweep all {len(SMALL_TOWN_TYPES)} small-town business types with deduplication")

    parser.add_argument("--output",     default="prospects",
                        help="Output directory (default: ./prospects)")
    parser.add_argument("--places-key", default="", dest="places_key",
                        help="Google Places API key (or set GOOGLE_PLACES_KEY env variable)")

    args = parser.parse_args()

    api_key = resolve_places_key(args.places_key)
    if not api_key:
        print("ERROR: No Google Places API key found.")
        print("Set GOOGLE_PLACES_KEY environment variable or use --places-key flag.")
        sys.exit(1)

    # Resolve coordinates
    if args.location:
        print(f"Geocoding '{args.location}'...")
        coords = geocode_location(args.location, api_key)
        if not coords:
            print(f"ERROR: Could not geocode '{args.location}'")
            sys.exit(1)
        lat, lng = coords
        location_label = args.location
        print(f"  → {lat}, {lng}")
    else:
        if not args.lng:
            print("ERROR: --lng is required when using --lat")
            sys.exit(1)
        lat, lng = args.lat, args.lng
        location_label = f"{lat},{lng}"

    # Run prospector
    if args.all_types:
        scanner_ready, warm_leads = prospect_all_types(
            lat=lat, lng=lng, radius=args.radius,
            api_key=api_key, location_label=location_label,
        )
        type_label = "all_types"
    else:
        scanner_ready, warm_leads = prospect(
            lat=lat, lng=lng, radius=args.radius,
            place_type=args.type, api_key=api_key, location_label=location_label,
        )
        type_label = args.type.replace(" ", "_")

    # Write output
    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)
    safe_loc = location_label.replace(",", "").replace(" ", "_")

    scanner_path = output_dir / f"{safe_loc}_{type_label}_scanner_ready.csv"
    warm_path    = output_dir / f"{safe_loc}_{type_label}_warm_leads.csv"

    write_csv(scanner_path, scanner_ready, SCANNER_FIELDS)
    write_csv(warm_path,    warm_leads,    WARM_LEAD_FIELDS)

    print_summary(scanner_ready, warm_leads, scanner_path, warm_path)


if __name__ == "__main__":
    main()
