# SEO Automation Suite

Tools for blog post validation, internal linking analysis, search engine notification, and competitive analysis. All scripts live in `LearnedGeek/scripts/seo/` and run via npm from the `LearnedGeek/` directory.

## Quick Reference

| Command | What it does | When it runs |
|---|---|---|
| `npm run seo:validate` | Validates posts.json fields, file existence, broken internal links | **Automatic**: pre-commit hook + CI |
| `npm run seo:sitemap` | Verifies sitemap completeness against posts.json | **Automatic**: CI on every push |
| `npm run seo:submit` | Notifies Bing/Yandex/Naver of new posts via IndexNow | **Automatic**: CI on push to main (new slugs only) |
| `npm run seo:links` | Finds internal cross-linking opportunities | **Manual**: run periodically |
| `npm run seo:headings` | Compares your post headings against competitors | **Manual**: run before writing/updating |
| `npm run seo:suggest-links` | Generates paste-ready markdown link snippets | **Manual**: when adding cross-links |
| `npm run seo:suggest-tags` | Suggests tags from existing vocabulary | **Auto** via `seo:new`, or manual |
| `npm run seo:describe` | Flags descriptions outside 50-160 char SEO range | **Auto** via `seo:audit`, or manual |
| `npm run seo:new` | Scaffolds a new post with auto tag + link suggestions | **Manual**: when starting a new post |
| `npm run seo:all` | Runs validate + sitemap + links | **Manual**: quick checkup |
| `npm run seo:audit` | Full audit: validate + sitemap + links + describe + suggest-links | **Manual**: comprehensive SEO review |

## Workflows

### Creating a New Post
```bash
npm run seo:new -- --title "My Post Title" --category tech --tags "dotnet,ef-core"
```

This single command:
1. Generates a slug from the title
2. Creates the markdown file (`Content/posts/{slug}.md`)
3. Adds the entry to posts.json with all required fields
4. Checks your tags against the existing vocabulary (warns about typos/duplicates)
5. Outputs paste-ready cross-link suggestions based on tag overlap
6. Prints a TODO checklist (write content, add description, create SVG)

Options:
- `--date 2026-04-01` — schedule for a future date (default: today)
- `--description "..."` — set the SEO description upfront
- Omit `--tags` to see the most-used tags and pick from them

### Maintaining Existing Posts
```bash
npm run seo:audit    # Full checkup: validation, sitemap, links, descriptions, suggestions
```

This runs everything in sequence and produces a comprehensive report. Use it monthly or after a batch of new posts. It covers:
- Post validation errors and warnings
- Sitemap completeness
- Internal link gaps with stats
- Descriptions that need trimming (shows the 160-char cutoff)
- Paste-ready link snippets for top opportunities

### Before Committing
The pre-commit hook runs `seo:validate --staged-only` automatically. It blocks commits with errors (broken links, missing files) but allows warnings (long descriptions).

### After Pushing to Main
CI runs validation + sitemap check, then auto-submits new post URLs to IndexNow.

## Automated (hands-off)

### Pre-Commit Hook
Every `git commit` that includes posts.json or markdown files triggers `seo:validate --staged-only`. It blocks the commit if there are errors (missing files, broken links, invalid fields). Warnings (like long descriptions) print but don't block.

**Setup** (one-time, already done):
```bash
git config core.hooksPath .githooks
```

### CI Pipeline (`.github/workflows/ci.yml`)
On every push or PR to main:
1. .NET build + test
2. `seo:validate` — full validation of all posts
3. `seo:sitemap` — verify sitemap would include all published posts

On push to main only:
4. `notify-search-engines` job diffs posts.json to detect new slugs
5. Submits new URLs to IndexNow (Bing, Yandex, Naver, Seznam, Yep)
6. Google is NOT notified via IndexNow (they don't participate) — Google discovers new content through the existing sitemap.xml and RSS feed at /feed.xml

**IndexNow key**: stored as `INDEXNOW_KEY` GitHub Actions secret. Key verification file is at `wwwroot/2586b37e-20ec-4e65-9902-3e6320aecbae.txt`.

## Manual Tools

### Suggest Links (paste-ready snippets)
```bash
npm run seo:suggest-links -- --slug my-post   # Suggestions for one post
npm run seo:suggest-links                      # Top suggestions across all posts
npm run seo:suggest-links -- --min-tags 3      # Require 3+ shared tags
```

**When to run**: When writing or updating a post. Outputs ready-to-paste markdown links like `[Post Title](/Blog/Post/slug)` based on tag overlap with existing posts.

### Suggest Tags
```bash
npm run seo:suggest-tags -- --slug my-post           # Analyze existing post content for tag suggestions
npm run seo:suggest-tags -- --title "My Post Title"  # Suggest tags based on title keywords
npm run seo:suggest-tags -- --list                   # Show all existing tags with usage counts
```

**When to run**: When choosing tags for a new post (automatically run by `seo:new`). Suggests tags from the existing vocabulary based on content analysis, warns about tags that aren't in use yet, and catches near-duplicates.

### Description Optimizer
```bash
npm run seo:describe                      # Show all posts with description issues
npm run seo:describe -- --slug my-post    # Analyze one post's description
npm run seo:describe -- --long-only       # Only show descriptions that are too long
```

**When to run**: Periodically or as part of `seo:audit`. Shows where Google would truncate your descriptions in search results (the `|` marker at 160 chars) and suggests a natural break point.

### Internal Link Gap Detector
```bash
npm run seo:links                    # Full report: opportunities, orphans, unreferenced
npm run seo:links -- --min-tags 3    # Only show pairs with 3+ shared tags
npm run seo:links -- --slug my-post  # Suggestions for one post only
npm run seo:links -- --json          # Machine-readable output
```

**When to run**: After publishing a few new posts, or monthly as a checkup. It finds posts that share tags but don't link to each other. The output includes:
- Link opportunities sorted by tag overlap count
- Orphan posts (no outbound links to other posts)
- Unreferenced posts (no other post links to them)
- Stats: total links, averages, gap count

### Competitive Heading Analysis
```bash
# URL mode (no API key needed) — you provide competitor URLs:
npm run seo:headings -- --urls "https://example.com/post1,https://example.com/post2" --slug my-post

# Search mode (requires Google CSE key) — auto-searches Google:
npm run seo:headings -- --keyword "ef core migrations" --slug my-post
npm run seo:headings -- --keyword "ef core migrations" --slug my-post --results 5
```

**When to run**: Before writing a new post or updating an existing one. It compares your heading structure against competitor pages and reports:
- Topics competitors cover that you don't (gaps to consider)
- Your unique headings (competitive advantage)
- Common headings (shared coverage)

**Search mode setup** (optional): Requires `GOOGLE_CSE_KEY` and `GOOGLE_CSE_ID` environment variables from a Google Programmable Search Engine (100 free queries/day).

### IndexNow Manual Submission
```bash
npm run seo:submit -- --slugs "post-slug-1,post-slug-2"  # Specific posts
npm run seo:submit -- --since 2026-02-01                  # All posts since date
npm run seo:submit -- --dry-run                           # Preview without submitting
```

**When to run**: Normally you don't — CI handles it automatically. Use manual mode if you updated an existing post's content and want search engines to re-crawl it, or if you need to backfill older posts.

Requires `INDEXNOW_KEY` environment variable:
```bash
export INDEXNOW_KEY="2586b37e-20ec-4e65-9902-3e6320aecbae"
```

### Sitemap Live Validation
```bash
npm run seo:sitemap -- --live   # Fetch deployed sitemap and compare against posts.json
```

**When to run**: After deploying, to confirm the live sitemap matches what posts.json expects. The offline mode (no `--live` flag) runs in CI and checks the data without network access.

## Validation Rules

`seo:validate` checks these rules for every post in posts.json:

**Errors (block commit):**
- Missing required fields: slug, title, description, category, tags, date, featured, image
- Invalid category (must be: tech, writing, gaming, project, personal, opinion)
- Invalid slug format (must be lowercase kebab-case: `[a-z0-9-]+`)
- Markdown file missing (`Content/posts/{slug}.md`)
- Image file missing (`wwwroot{image}`)
- Broken internal links (link to a slug that doesn't exist in posts.json)
- Duplicate slugs or titles

**Warnings (informational, don't block):**
- Description outside 50-160 character range (Google SERP snippet sweet spot)
- Tags not in lowercase kebab-case

## File Structure

```
scripts/seo/
  lib/
    posts.mjs          # Shared: load posts.json, resolve paths, constants
    markdown.mjs       # Shared: extract internal links and headings from markdown
  validate-posts.mjs   # Pre-commit + CI validation
  validate-sitemap.mjs # Sitemap completeness check
  internal-links.mjs   # Link gap detector
  submit-urls.mjs      # IndexNow submission
  heading-analysis.mjs # Competitive heading comparison
  suggest-links.mjs    # Paste-ready link snippet generator
  suggest-tags.mjs     # Tag vocabulary suggestions
  describe.mjs         # Description length optimizer
  new-post.mjs         # New post scaffolding
  .last-submit         # (gitignored) Timestamp of last IndexNow submission
```
