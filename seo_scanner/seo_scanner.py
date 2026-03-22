#!/usr/bin/env python3
"""
SEO Scanner - LearnedGeek.com
Scans websites for SEO issues and generates client-ready proposals in Markdown.

Usage:
  Single:  python seo_scanner.py --url https://example.com --name "Business Name" --contact "Owner Name" [--city "Milwaukee, WI"]
  Batch:   python seo_scanner.py --batch clients.csv

Google Places API (optional but recommended):
  Set env variable:  export GOOGLE_PLACES_KEY=your_key_here
  Or pass as flag:   python seo_scanner.py --places-key YOUR_KEY ...

CSV columns: url, business_name, contact_name, city, place_id
  - city:     used to search Places by name+city (e.g. "Milwaukee, WI")
  - place_id: pass a known Place ID to skip the search step
  Both are optional; Places check is skipped if neither is available AND no API key is configured.
"""

import argparse
import csv
import json
import os
import re
import sys
import time
from datetime import datetime
from pathlib import Path

import requests
from bs4 import BeautifulSoup

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

PAGESPEED_API    = "https://www.googleapis.com/pagespeedonline/v5/runPagespeed"
PLACES_FIND_API  = "https://maps.googleapis.com/maps/api/place/findplacefromtext/json"
PLACES_DETAIL_API = "https://maps.googleapis.com/maps/api/place/details/json"

HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/122.0.0.0 Safari/537.36"
    ),
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
    "Accept-Language": "en-US,en;q=0.9",
    "Accept-Encoding": "gzip, deflate, br",
    "Connection": "keep-alive",
    "Upgrade-Insecure-Requests": "1",
}
REQUEST_TIMEOUT = 15

# Local SEO geographic keywords — extend with any cities in your target market
LOCAL_GEO_PATTERNS = [
    r"\b(near me|in [a-z\s]+wi|wisconsin|milwaukee|madison|green bay|racine|kenosha|"
    r"waukesha|appleton|oshkosh|janesville|eau claire|west allis|sheboygan)\b"
]

# ---------------------------------------------------------------------------
# Fetching
# ---------------------------------------------------------------------------

def fetch_page(url: str):
    try:
        resp = requests.get(url, headers=HEADERS, timeout=REQUEST_TIMEOUT)
        resp.raise_for_status()
        soup = BeautifulSoup(resp.text, "html.parser")
        return resp, soup
    except Exception as e:
        print(f"  [ERROR] Could not fetch {url}: {e}")
        return None, None


def fetch_pagespeed(url: str) -> dict:
    try:
        params = {
            "url": url,
            "strategy": "mobile",
            "category": ["performance", "seo", "best-practices"],
        }
        resp = requests.get(PAGESPEED_API, params=params, timeout=30)
        if resp.status_code == 200:
            return resp.json()
    except Exception as e:
        print(f"  [WARN] PageSpeed fetch failed: {e}")
    return {}


# ---------------------------------------------------------------------------
# Google Places API
# ---------------------------------------------------------------------------

def find_place_id(business_name: str, city: str, api_key: str) -> str | None:
    """Search for a Place ID by business name + city."""
    query = f"{business_name} {city}"
    try:
        resp = requests.get(
            PLACES_FIND_API,
            params={
                "input": query,
                "inputtype": "textquery",
                "fields": "place_id,name",
                "key": api_key,
            },
            timeout=REQUEST_TIMEOUT,
        )
        data = resp.json()
        candidates = data.get("candidates", [])
        if candidates:
            return candidates[0]["place_id"]
    except Exception as e:
        print(f"  [WARN] Places search failed: {e}")
    return None


def fetch_place_details(place_id: str, api_key: str) -> dict:
    """Fetch full details for a known Place ID."""
    fields = (
        "name,formatted_address,formatted_phone_number,"
        "business_status,opening_hours,photos,rating,"
        "user_ratings_total,types,website,editorial_summary"
    )
    try:
        resp = requests.get(
            PLACES_DETAIL_API,
            params={"place_id": place_id, "fields": fields, "key": api_key},
            timeout=REQUEST_TIMEOUT,
        )
        data = resp.json()
        return data.get("result", {})
    except Exception as e:
        print(f"  [WARN] Places details fetch failed: {e}")
    return {}


def check_places(
    business_name: str,
    city: str,
    api_key: str,
    place_id: str | None = None,
    site_phone: str | None = None,
    site_address: str | None = None,
) -> dict:
    """Run the Google Business Profile check and return structured findings."""
    result = {
        "enabled": True,
        "place_id": place_id,
        "found": False,
        "name": None,
        "address": None,
        "phone": None,
        "rating": None,
        "review_count": None,
        "photo_count": None,
        "has_hours": False,
        "has_description": False,
        "business_status": None,
        "nap_phone_match": None,
        "nap_address_match": None,
        "issues": [],
        "positives": [],
    }

    # Resolve place_id if not directly provided
    if not place_id:
        if not city:
            result["enabled"] = False
            return result
        print(f"  Searching Google Places for \"{business_name}\" in {city}...")
        place_id = find_place_id(business_name, city, api_key)
        if not place_id:
            result["issues"].append(
                "No Google Business Profile found for this business name and city — "
                "this is a significant local SEO gap. A claimed and optimized GBP listing "
                "is often the single biggest driver of local search visibility"
            )
            return result
        result["place_id"] = place_id

    print(f"  Fetching Google Business Profile details...")
    details = fetch_place_details(place_id, api_key)
    if not details:
        result["issues"].append(
            "Google Business Profile could not be retrieved — verify the listing exists and is claimed"
        )
        return result

    result["found"]            = True
    result["name"]             = details.get("name")
    result["address"]          = details.get("formatted_address")
    result["phone"]            = details.get("formatted_phone_number")
    result["rating"]           = details.get("rating")
    result["review_count"]     = details.get("user_ratings_total", 0)
    result["photo_count"]      = len(details.get("photos", []))
    result["has_hours"]        = bool(details.get("opening_hours"))
    result["has_description"]  = bool(details.get("editorial_summary", {}).get("overview"))
    result["business_status"]  = details.get("business_status")

    issues    = result["issues"]
    positives = result["positives"]

    # Business status
    status = result["business_status"]
    if status == "CLOSED_PERMANENTLY":
        issues.append(
            "⚠️ CRITICAL: Google shows this business as PERMANENTLY CLOSED — "
            "this must be corrected immediately or the listing won't appear in searches"
        )
    elif status == "CLOSED_TEMPORARILY":
        issues.append(
            "Google shows this business as temporarily closed — "
            "if the business is open, update the listing status immediately"
        )

    # Reviews
    rc = result["review_count"]
    if rc == 0:
        issues.append(
            "No Google reviews — reviews are one of the strongest local ranking signals. "
            "A strategy for asking satisfied customers to leave reviews should be a priority"
        )
    elif rc < 10:
        issues.append(
            f"Only {rc} Google review(s) — businesses with 10+ reviews "
            "see significantly better local pack visibility"
        )
    else:
        positives.append(
            f"Google Business Profile has {rc} reviews (avg {result['rating']}/5.0)"
        )

    if result["rating"] and result["rating"] < 4.0 and rc > 5:
        issues.append(
            f"Average rating is {result['rating']}/5.0 — ratings below 4.0 negatively affect "
            "click-through rates and local pack rankings; a review response strategy is worth discussing"
        )

    # Photos
    pc = result["photo_count"]
    if pc == 0:
        issues.append(
            "No photos on the Google Business Profile — Google heavily weights listings with photos. "
            "Even a handful of quality images (storefront, interior, team) can meaningfully "
            "improve ranking and click-through"
        )
    elif pc < 5:
        issues.append(
            f"Only {pc} photo(s) on the Google Business Profile — "
            "aim for at least 10 photos covering the storefront, interior, products/services, and team"
        )
    else:
        positives.append(f"Google Business Profile has {pc} photos")

    # Hours
    if not result["has_hours"]:
        issues.append(
            "Business hours are not set on the Google Business Profile — "
            "missing hours reduces customer confidence and can hurt local search rankings"
        )
    else:
        positives.append("Business hours are set on the Google Business Profile")

    # Description
    if not result["has_description"]:
        issues.append(
            "No business description on the Google Business Profile — "
            "a well-written description with relevant keywords improves both rankings and conversions"
        )

    # NAP cross-check: phone
    if site_phone and result["phone"]:
        site_digits = re.sub(r"\D", "", site_phone)
        gbp_digits  = re.sub(r"\D", "", result["phone"])
        if site_digits and gbp_digits:
            if site_digits[-10:] == gbp_digits[-10:]:
                result["nap_phone_match"] = True
                positives.append("Phone number matches between website and Google Business Profile")
            else:
                result["nap_phone_match"] = False
                issues.append(
                    f"Phone number mismatch — website shows a different number than the "
                    f"Google Business Profile ({result['phone']}). "
                    "Inconsistent NAP data confuses search engines and can suppress rankings"
                )

    # NAP cross-check: address (zip code comparison)
    if site_address and result["address"]:
        site_zip = re.search(r"\b(\d{5})\b", site_address)
        gbp_zip  = re.search(r"\b(\d{5})\b", result["address"])
        if site_zip and gbp_zip:
            if site_zip.group(1) == gbp_zip.group(1):
                result["nap_address_match"] = True
                positives.append("Address matches between website and Google Business Profile")
            else:
                result["nap_address_match"] = False
                issues.append(
                    f"Address mismatch — website address doesn't match the "
                    f"Google Business Profile ({result['address']}). "
                    "NAP inconsistency is a known local ranking suppressor"
                )

    return result


# ---------------------------------------------------------------------------
# On-page analysis
# ---------------------------------------------------------------------------

def check_title(soup: BeautifulSoup) -> dict:
    tag = soup.find("title")
    title = tag.get_text(strip=True) if tag else ""
    length = len(title)
    issues = []
    if not title:
        issues.append("Missing title tag entirely")
    elif length < 30:
        issues.append(f"Title is too short ({length} chars; aim for 50–60)")
    elif length > 60:
        issues.append(f"Title is too long ({length} chars; aim for 50–60)")
    return {"value": title, "length": length, "issues": issues}


def check_meta_description(soup: BeautifulSoup) -> dict:
    tag = soup.find("meta", attrs={"name": "description"})
    desc = tag["content"].strip() if tag and tag.get("content") else ""
    length = len(desc)
    issues = []
    if not desc:
        issues.append("Missing meta description")
    elif length < 100:
        issues.append(f"Meta description too short ({length} chars; aim for 150–160)")
    elif length > 160:
        issues.append(f"Meta description too long ({length} chars; aim for 150–160)")
    return {"value": desc, "length": length, "issues": issues}


def check_headings(soup: BeautifulSoup) -> dict:
    h1s = [h.get_text(strip=True) for h in soup.find_all("h1")]
    h2s = [h.get_text(strip=True) for h in soup.find_all("h2")]
    issues = []
    if not h1s:
        issues.append("No H1 tag found — every page should have exactly one")
    elif len(h1s) > 1:
        issues.append(f"Multiple H1 tags found ({len(h1s)}) — use only one per page")
    if not h2s:
        issues.append("No H2 tags found — subheadings help structure content for search engines")
    return {"h1s": h1s, "h2s": h2s, "issues": issues}


def check_images(soup: BeautifulSoup) -> dict:
    imgs = soup.find_all("img")
    missing_alt = [
        img.get("src", "[no src]") for img in imgs
        if not img.get("alt") and img.get("role") != "presentation"
    ]
    issues = []
    if missing_alt:
        count = len(missing_alt)
        issues.append(
            f"{count} image{'s' if count > 1 else ''} missing alt text "
            "— alt text helps search engines understand your images"
        )
    return {"total": len(imgs), "missing_alt_count": len(missing_alt), "issues": issues}


def check_schema(soup: BeautifulSoup) -> dict:
    ld_json = soup.find_all("script", attrs={"type": "application/ld+json"})
    schemas_found = []
    for tag in ld_json:
        try:
            data = json.loads(tag.string or "")
            # Handle top-level array of schema objects
            items = data if isinstance(data, list) else [data]
            for item in items:
                if not isinstance(item, dict):
                    continue
                t = item.get("@type", "Unknown")
                # @type can be a string OR a list — flatten either way
                if isinstance(t, list):
                    schemas_found.extend([str(x) for x in t])
                else:
                    schemas_found.append(str(t))
        except Exception:
            pass
    local_types = {
        "LocalBusiness", "Restaurant", "Store", "MedicalBusiness",
        "AutomotiveBusiness", "HomeAndConstructionBusiness", "Organization",
    }
    has_local = any(t in local_types for t in schemas_found)
    issues = []
    if not schemas_found:
        issues.append(
            "No structured data (schema markup) found — this helps Google display rich results "
            "and understand your business type"
        )
    elif not has_local:
        issues.append(
            "Schema markup present but no LocalBusiness type detected — "
            "adding LocalBusiness schema significantly boosts local search visibility"
        )
    return {"schemas": schemas_found, "has_local_schema": has_local, "issues": issues}


def check_nap(soup: BeautifulSoup) -> dict:
    text = soup.get_text(" ", strip=True)
    phone_match   = re.search(r"(\(?\d{3}\)?[\s.\-]\d{3}[\s.\-]\d{4})", text)
    address_match = re.search(r"\d+\s+\w[\w\s]+,\s*\w[\w\s]+,?\s*[A-Z]{2}\s*\d{5}", text)
    issues = []
    if not phone_match:
        issues.append(
            "No phone number detected on this page — for local businesses, "
            "your phone number should appear on every page"
        )
    if not address_match:
        issues.append(
            "No street address detected — displaying your address consistently "
            "is critical for local search rankings (NAP consistency)"
        )
    return {
        "phone_found":   bool(phone_match),
        "phone_value":   phone_match.group(1) if phone_match else None,
        "address_found": bool(address_match),
        "address_value": address_match.group(0) if address_match else None,
        "issues": issues,
    }


def check_canonical(soup: BeautifulSoup) -> dict:
    tag = soup.find("link", attrs={"rel": "canonical"})
    issues = []
    if not tag:
        issues.append(
            "No canonical tag found — without it, Google may index duplicate versions "
            "of your pages and split your ranking authority"
        )
    return {"canonical": tag["href"] if tag else None, "issues": issues}


def check_open_graph(soup: BeautifulSoup) -> dict:
    missing = []
    for prop in ("og:title", "og:description", "og:image"):
        if not soup.find("meta", property=prop):
            missing.append(prop)
    issues = []
    if missing:
        issues.append(
            f"Missing Open Graph tags: {', '.join(missing)} — these control how your "
            "page appears when shared on social media and can affect click-through rates"
        )
    return {"missing": missing, "issues": issues}


def check_robots_meta(soup: BeautifulSoup) -> dict:
    tag = soup.find("meta", attrs={"name": "robots"})
    content = tag["content"].lower() if tag and tag.get("content") else ""
    issues = []
    if "noindex" in content:
        issues.append(
            "⚠️ CRITICAL: This page has a noindex directive — it is being actively "
            "blocked from appearing in search results"
        )
    if "nofollow" in content:
        issues.append(
            "Page has nofollow directive — search engines won't follow links on this page"
        )
    return {"content": content, "issues": issues}


def check_geo_keywords(soup: BeautifulSoup) -> dict:
    text       = soup.get_text(" ", strip=True).lower()
    title_tag  = soup.find("title")
    title_text = title_tag.get_text(strip=True).lower() if title_tag else ""
    h1_texts   = [h.get_text(strip=True).lower() for h in soup.find_all("h1")]

    def has_geo(s: str) -> bool:
        return any(re.search(p, s, re.IGNORECASE) for p in LOCAL_GEO_PATTERNS)

    geo_in_title = has_geo(title_text)
    geo_in_h1    = any(has_geo(h) for h in h1_texts)
    geo_in_body  = has_geo(text)

    issues = []
    if not geo_in_title:
        issues.append(
            "No geographic location keyword in the page title — "
            "including your city or region in the title tag is one of the highest-impact "
            "local SEO improvements you can make"
        )
    if not geo_in_h1:
        issues.append(
            "No geographic keyword in the main heading (H1) — "
            "reinforcing your location in headings strengthens local relevance signals"
        )
    if not geo_in_body:
        issues.append(
            "Geographic keywords appear sparse in page content — "
            "naturally mentioning your city, region, or service area throughout "
            "your content helps local rankings"
        )
    return {
        "geo_in_title": geo_in_title,
        "geo_in_h1":    geo_in_h1,
        "geo_in_body":  geo_in_body,
        "issues": issues,
    }


def parse_pagespeed(ps_data: dict) -> dict:
    result = {"performance": None, "seo": None, "best_practices": None, "issues": []}
    if not ps_data:
        return result
    cats = ps_data.get("lighthouseResult", {}).get("categories", {})
    result["performance"]    = round((cats.get("performance",     {}).get("score") or 0) * 100)
    result["seo"]            = round((cats.get("seo",             {}).get("score") or 0) * 100)
    result["best_practices"] = round((cats.get("best-practices",  {}).get("score") or 0) * 100)

    perf = result["performance"]
    if perf is not None and perf < 50:
        result["issues"].append(
            f"Mobile page speed score is critically low ({perf}/100) — "
            "slow sites rank lower and lose visitors before the page even loads"
        )
    elif perf is not None and perf < 75:
        result["issues"].append(
            f"Mobile page speed score needs improvement ({perf}/100) — "
            "Google uses page speed as a direct ranking factor"
        )

    seo_score = result["seo"]
    if seo_score is not None and seo_score < 80:
        result["issues"].append(
            f"Google's own SEO audit scores this page at {seo_score}/100 — "
            "there are technical SEO issues Google has flagged directly"
        )
    return result


# ---------------------------------------------------------------------------
# Full scan
# ---------------------------------------------------------------------------

def scan_url(
    url: str,
    business_name: str = "",
    city: str = "",
    place_id: str = "",
    places_key: str = "",
) -> dict:
    print(f"  Fetching page...")
    resp, soup = fetch_page(url)
    if not soup:
        return {"error": f"Could not fetch {url}"}

    print(f"  Running PageSpeed Insights...")
    ps_data = fetch_pagespeed(url)

    print(f"  Analyzing on-page signals...")
    nap = check_nap(soup)

    # Places check
    places_result = {"enabled": False, "issues": [], "positives": []}
    if places_key:
        if place_id or city:
            places_result = check_places(
                business_name=business_name,
                city=city,
                api_key=places_key,
                place_id=place_id or None,
                site_phone=nap.get("phone_value"),
                site_address=nap.get("address_value"),
            )
        else:
            print(f"  [INFO] Skipping Places check — no city or place_id provided for this entry")
    else:
        print(f"  [INFO] Skipping Places check — no API key (set GOOGLE_PLACES_KEY or use --places-key)")

    return {
        "url":             url,
        "scanned_at":      datetime.now().isoformat(),
        "title":           check_title(soup),
        "meta_description": check_meta_description(soup),
        "headings":        check_headings(soup),
        "images":          check_images(soup),
        "schema":          check_schema(soup),
        "nap":             nap,
        "canonical":       check_canonical(soup),
        "open_graph":      check_open_graph(soup),
        "robots_meta":     check_robots_meta(soup),
        "geo_keywords":    check_geo_keywords(soup),
        "pagespeed":       parse_pagespeed(ps_data),
        "places":          places_result,
    }


# ---------------------------------------------------------------------------
# Proposal generation
# ---------------------------------------------------------------------------

SEVERITY_ORDER = {"high": 0, "medium": 1, "low": 2}
SEVERITY_EMOJI = {"high": "🔴", "medium": "🟡", "low": "🟢"}
SEVERITY_LABEL = {"high": "High Impact", "medium": "Medium Impact", "low": "Low Impact"}

# Issues containing these strings are elevated to high severity in the GBP section
GBP_HIGH_KEYWORDS = (
    "critical", "permanently closed", "temporarily closed",
    "no google business", "mismatch",
)


def classify_issues(results: dict) -> list[dict]:
    all_issues = []

    def add(category_label: str, issues: list, severity: str):
        for issue in issues:
            all_issues.append({
                "category": category_label,
                "issue":    issue,
                "severity": severity,
            })

    add("Indexing & Crawlability",    results["robots_meta"]["issues"],      "high")
    add("Page Speed & Performance",   results["pagespeed"]["issues"],        "high")
    add("Title Tag",                  results["title"]["issues"],            "high")
    add("Local Business Schema",      results["schema"]["issues"],           "high")
    add("NAP Consistency (Website)",  results["nap"]["issues"],              "high")
    add("Geographic Keywords",        results["geo_keywords"]["issues"],     "high")

    # GBP issues — smart severity assignment
    places = results.get("places", {})
    if places.get("enabled"):
        for issue in places.get("issues", []):
            sev = "high" if any(kw in issue.lower() for kw in GBP_HIGH_KEYWORDS) else "medium"
            add("Google Business Profile", [issue], sev)

    add("Meta Description",           results["meta_description"]["issues"], "medium")
    add("Heading Structure",          results["headings"]["issues"],         "medium")
    add("Canonical Tags",             results["canonical"]["issues"],        "medium")
    add("Image Optimization",         results["images"]["issues"],           "medium")
    add("Social Media Tags",          results["open_graph"]["issues"],       "low")

    return sorted(all_issues, key=lambda x: SEVERITY_ORDER.get(x["severity"], 99))


def generate_proposal(scan: dict, business_name: str, contact_name: str) -> str:
    if "error" in scan:
        return f"# SEO Proposal — {business_name}\n\n**Error:** {scan['error']}\n"

    url          = scan["url"]
    scanned_date = datetime.fromisoformat(scan["scanned_at"]).strftime("%B %d, %Y")
    issues       = classify_issues(scan)
    high         = [i for i in issues if i["severity"] == "high"]
    medium       = [i for i in issues if i["severity"] == "medium"]
    low          = [i for i in issues if i["severity"] == "low"]

    # Stats block
    ps = scan["pagespeed"]
    stat_lines = []
    if ps["performance"] is not None:
        stat_lines.append(f"- **Mobile Performance Score:** {ps['performance']}/100")
        stat_lines.append(f"- **SEO Audit Score (Google):** {ps['seo']}/100")

    places = scan.get("places", {})
    if places.get("found"):
        stat_lines.append(
            f"- **Google Business Profile:** Found ✅  "
            f"({places.get('review_count', 0)} reviews, "
            f"avg {places.get('rating', 'N/A')}/5.0, "
            f"{places.get('photo_count', 0)} photos)"
        )
    elif places.get("enabled") and not places.get("found"):
        stat_lines.append("- **Google Business Profile:** Not found or unclaimed ⚠️")

    stats_block = ("\n".join(stat_lines) + "\n\n") if stat_lines else ""

    # What's working
    working = []
    if not scan["title"]["issues"]:
        working.append(f"Title tag is well-optimized: *\"{scan['title']['value']}\"*")
    if not scan["meta_description"]["issues"]:
        working.append("Meta description is present and properly sized")
    if scan["schema"]["schemas"]:
        working.append(f"Structured data is present ({', '.join(scan['schema']['schemas'])})")
    if scan["nap"]["phone_found"]:
        working.append("Phone number is visible on the page")
    if scan["nap"]["address_found"]:
        working.append("Business address is present on the page")
    if ps["performance"] and ps["performance"] >= 75:
        working.append(f"Page speed is solid ({ps['performance']}/100 on mobile)")
    for pos in places.get("positives", []):
        working.append(pos)

    working_section = ""
    if working:
        working_section = "## What's Already Working\n\n"
        working_section += "\n".join(f"- {w}" for w in working) + "\n\n"

    # Findings
    def render_issues(issue_list: list[dict]) -> str:
        if not issue_list:
            return ""
        out = ""
        current_cat = None
        for item in issue_list:
            if item["category"] != current_cat:
                current_cat = item["category"]
                emoji = SEVERITY_EMOJI[item["severity"]]
                label = SEVERITY_LABEL[item["severity"]]
                out += f"\n### {emoji} {item['category']} — {label}\n\n"
            out += f"**Finding:** {item['issue']}\n\n"
        return out

    findings = render_issues(high) + render_issues(medium) + render_issues(low)
    if not issues:
        findings = (
            "\nNo significant SEO issues were found during this automated scan. "
            "A deeper manual review may surface additional opportunities.\n"
        )

    summary_line = (
        f"{len(high)} high-impact, {len(medium)} medium-impact, "
        f"and {len(low)} low-impact item(s) identified."
    )

    # Scope of work — derived directly from findings
    categories_found = {i["category"] for i in issues}
    scope_items = []

    mapping = [
        ("Title Tag",                 "Rewrite and optimize page title tags with geographic keywords"),
        ("Meta Description",          "Write compelling meta descriptions for key pages"),
        ("Local Business Schema",     "Implement LocalBusiness structured data markup"),
        ("NAP Consistency (Website)", "Ensure Name, Address, and Phone appear consistently site-wide"),
        ("Geographic Keywords",       "Integrate geographic keywords into titles, headings, and content"),
        ("Page Speed & Performance",  "Audit and address page speed issues (images, caching, etc.)"),
        ("Heading Structure",         "Restructure page headings for proper H1/H2 hierarchy"),
        ("Image Optimization",        "Add descriptive alt text to all images"),
        ("Canonical Tags",            "Add canonical tags to prevent duplicate content issues"),
        ("Social Media Tags",         "Add Open Graph tags for improved social media sharing"),
    ]
    for cat, action in mapping:
        if cat in categories_found:
            scope_items.append(action)

    if "Google Business Profile" in categories_found:
        scope_items.append("Audit and optimize Google Business Profile listing")
        if not places.get("has_description"):
            scope_items.append("Write keyword-rich Google Business Profile description")
        if (places.get("photo_count") or 0) < 5:
            scope_items.append("Add professional photos to Google Business Profile")
        if (places.get("review_count") or 0) < 10:
            scope_items.append("Implement a review generation strategy for Google")

    scope_section = ""
    if scope_items:
        scope_section = (
            "## Suggested Scope of Work\n\n"
            "Based on the findings above, here is a summary of recommended improvements:\n\n"
            + "\n".join(f"- {s}" for s in scope_items)
            + "\n\n"
        )

    return f"""# SEO Improvement Proposal
## {business_name}

---

**Prepared by:** Mark McArthey — LearnedGeek.com  
**Prepared for:** {contact_name}  
**Website reviewed:** {url}  
**Date of scan:** {scanned_date}  

---

## Overview

This proposal is based on a technical SEO scan of **{url}** conducted on {scanned_date}. 
The findings below are specific to your website and represent concrete opportunities to 
improve your visibility in local search results.

{summary_line}

{stats_block}---

{working_section}## Findings

The following issues were identified during the scan, organized by potential impact on 
your search rankings and local visibility.
{findings}

---

{scope_section}---

## Next Steps

I'd welcome the opportunity to walk through these findings with you in person and discuss 
which improvements would have the biggest impact for your business. There's no obligation — 
I'd rather you walk away with a clear picture of where things stand than feel pressured into 
anything.

If you'd like to schedule a conversation, you can reach me at:

- **Email:** markm@learnedgeek.com  
- **Website:** https://learnedgeek.com  

— Mark McArthey  
*LearnedGeek.com — Curiosity Driven, Custom Built*

---
*This report was generated by an automated scanner and reflects the state of the homepage 
at the time of the scan. A full site audit may surface additional opportunities.*
"""


# ---------------------------------------------------------------------------
# CLI helpers
# ---------------------------------------------------------------------------

def safe_filename(name: str) -> str:
    return re.sub(r"[^\w\-]", "_", name).strip("_")


def resolve_places_key(flag_value: str) -> str:
    """Flag takes priority; fall back to environment variable."""
    return flag_value or os.environ.get("GOOGLE_PLACES_KEY", "")


def run_single(
    url: str,
    business_name: str,
    contact_name: str,
    output_dir: Path,
    places_key: str = "",
    city: str = "",
    place_id: str = "",
) -> tuple[Path | None, str | None]:
    """Returns (proposal_path, error_reason) — one will always be None."""
    print(f"\nScanning: {url}")
    scan = scan_url(url, business_name=business_name, city=city,
                    place_id=place_id, places_key=places_key)

    # Skip proposal generation if the page couldn't be fetched
    if "error" in scan:
        reason = scan["error"]
        print(f"  [SKIP] Could not connect — no proposal generated")
        return None, reason

    proposal = generate_proposal(scan, business_name, contact_name)
    filename = output_dir / f"{safe_filename(business_name)}_SEO_Proposal.md"
    filename.write_text(proposal, encoding="utf-8")
    print(f"  ✅ Proposal saved: {filename}")
    return filename, None


def run_batch(csv_path: str, output_dir: Path, places_key: str = ""):
    with open(csv_path, newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    print(f"\nBatch mode: {len(rows)} clients loaded from {csv_path}")
    if places_key:
        print("  Google Places API key detected — GBP checks enabled")
    else:
        print(
            "  No Places API key — GBP checks will be skipped\n"
            "  (set GOOGLE_PLACES_KEY env variable or use --places-key flag)"
        )

    results   = []
    skipped   = []  # list of dicts: {business_name, url, city, place_id, error}
    for i, row in enumerate(rows, 1):
        url      = row.get("url", "").strip()
        name     = row.get("business_name", "").strip()
        contact  = row.get("contact_name", "").strip() or "Business Owner"
        city     = row.get("city", "").strip()
        place_id = row.get("place_id", "").strip()

        if not url or not name:
            print(f"  [SKIP] Row {i}: missing url or business_name")
            continue

        print(f"\n[{i}/{len(rows)}] {name}")
        time.sleep(1)  # polite delay
        outfile, error = run_single(url, name, contact, output_dir,
                                    places_key=places_key, city=city, place_id=place_id)
        if outfile:
            results.append(outfile)
        else:
            skipped.append({
                "business_name": name,
                "url":           url,
                "city":          city,
                "place_id":      place_id,
                "error":         error or "Unknown error",
            })

    # Write unreachable sites CSV alongside the proposals
    unreachable_path = output_dir / "unreachable_sites.csv"
    unreachable_fields = ["business_name", "url", "city", "place_id", "error"]
    with open(unreachable_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=unreachable_fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(skipped)

    print(f"\n✅ Batch complete.")
    print(f"   {len(results)} proposals generated in: {output_dir}")
    if skipped:
        print(f"   {len(skipped)} unreachable sites → {unreachable_path}")
        for s in skipped:
            print(f"     - {s['business_name']} ({s['url']})")


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="SEO Scanner — generates client proposals for local businesses",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Single scan, no Places API
  python seo_scanner.py --url https://example.com --name "Joe's Pizza" --contact "Joe"

  # Single scan with Places API (name+city search)
  python seo_scanner.py --url https://joespizza.com --name "Joe's Pizza" \\
    --contact "Joe" --city "Milwaukee, WI" --places-key AIza...

  # Single scan with a known Place ID (skips search, more reliable)
  python seo_scanner.py --url https://joespizza.com --name "Joe's Pizza" \\
    --contact "Joe" --place-id ChIJxxxx --places-key AIza...

  # Batch mode (API key from environment variable)
  export GOOGLE_PLACES_KEY=AIza...
  python seo_scanner.py --batch clients.csv

CSV columns (all except url and business_name are optional):
  url, business_name, contact_name, city, place_id
        """
    )
    parser.add_argument("--url",        help="Single URL to scan")
    parser.add_argument("--name",       help="Business name")
    parser.add_argument("--contact",    default="Business Owner", help="Contact/owner name")
    parser.add_argument("--city",       default="", help="City + state for Places search, e.g. 'Milwaukee, WI'")
    parser.add_argument("--place-id",   default="", dest="place_id",
                        help="Known Google Place ID — skips the search step")
    parser.add_argument("--places-key", default="", dest="places_key",
                        help="Google Places API key (or set GOOGLE_PLACES_KEY env variable)")
    parser.add_argument("--batch",      help="Path to CSV file for batch processing")
    parser.add_argument("--output",     default="proposals",
                        help="Output directory for proposals (default: ./proposals)")

    args = parser.parse_args()
    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)
    places_key = resolve_places_key(args.places_key)

    if args.batch:
        run_batch(args.batch, output_dir, places_key=places_key)
    elif args.url and args.name:
        run_single(args.url, args.name, args.contact, output_dir,
                   places_key=places_key, city=args.city, place_id=args.place_id)
    else:
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
