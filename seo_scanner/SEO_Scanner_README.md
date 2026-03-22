# SEO Scanner — LearnedGeek.com

Scans websites for SEO issues and generates client-ready proposals in Markdown.

## Setup

```bash
pip install -r requirements.txt
```

## Usage

### Single URL (no Places API)
```bash
python seo_scanner.py --url https://joespizza.com --name "Joe's Pizza" --contact "Joe"
```

### Single URL with Google Business Profile check
```bash
# API key as flag
python seo_scanner.py --url https://joespizza.com --name "Joe's Pizza" \
  --contact "Joe" --city "Milwaukee, WI" --places-key AIza...

# API key as environment variable (recommended)
export GOOGLE_PLACES_KEY=AIza...
python seo_scanner.py --url https://joespizza.com --name "Joe's Pizza" \
  --contact "Joe" --city "Milwaukee, WI"

# Skip the Places search entirely by providing a known Place ID
python seo_scanner.py --url https://joespizza.com --name "Joe's Pizza" \
  --contact "Joe" --place-id ChIJxxxxxxxx --places-key AIza...
```

### Batch Mode
```bash
export GOOGLE_PLACES_KEY=AIza...
python seo_scanner.py --batch clients.csv
python seo_scanner.py --batch clients.csv --output ~/Desktop/proposals
```

## CSV Format

```csv
url,business_name,contact_name,city,place_id
https://joespizza.com,Joe's Pizza,Joe Marchetti,Milwaukee WI,
https://mapleplumbing.com,Maple Plumbing,Sarah Maple,Waukesha WI,ChIJexample
```

- `contact_name`, `city`, and `place_id` are all optional
- If `place_id` is provided, the Places search is skipped (faster and more reliable)
- If neither `city` nor `place_id` is provided, the GBP check is skipped for that row

## What It Checks

### On-Page (always runs)
| Check | Severity |
|---|---|
| Page indexing / noindex flags | 🔴 High |
| Mobile page speed (Google PageSpeed Insights) | 🔴 High |
| Title tag presence, length, optimization | 🔴 High |
| LocalBusiness schema markup | 🔴 High |
| NAP (Name, Address, Phone) on page | 🔴 High |
| Geographic keyword signals | 🔴 High |
| Meta description presence and length | 🟡 Medium |
| H1/H2 heading structure | 🟡 Medium |
| Canonical tags | 🟡 Medium |
| Image alt text | 🟡 Medium |
| Open Graph / social sharing tags | 🟢 Low |

### Google Business Profile (requires Places API key + city or place_id)
| Check | Severity |
|---|---|
| GBP listing exists and is claimed | 🔴 High |
| Business status (open/closed) | 🔴 High |
| NAP cross-check: site vs. GBP | 🔴 High |
| Review count and rating | 🟡 Medium |
| Photo count | 🟡 Medium |
| Business hours set | 🟡 Medium |
| Business description | 🟡 Medium |

## Google Places API Setup

1. Go to https://console.cloud.google.com
2. Create a project (or use an existing one)
3. Enable **Places API**
4. Create an API key under Credentials
5. Set `export GOOGLE_PLACES_KEY=your_key` or pass `--places-key`

Free tier usage for lookup-style calls (Find Place + Place Details) is generous 
and well within reasonable use for a local business prospecting tool.

## Geographic Keywords

The scanner checks for Wisconsin cities by default. To target other regions, 
edit `LOCAL_GEO_PATTERNS` near the top of `seo_scanner.py`.

