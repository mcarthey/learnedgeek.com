# ProposalGenerator

Converts markdown proposals into polished, print-ready HTML documents using the Learned Geek proposal template.

## Usage

```bash
dotnet run --project tools/ProposalGenerator -- <input.md> [output.html]
```

If output is not specified, writes to `<input-name>-PRINT.html` in the same directory as the input file.

### Example

```bash
dotnet run --project tools/ProposalGenerator -- docs/allevo/PROPOSAL.md
# → docs/allevo/PROPOSAL-PRINT.html
```

Open the generated HTML in Chrome/Edge, `Ctrl+P`, check "Background graphics", save as PDF.

## YAML Front Matter

Every proposal markdown file starts with a YAML front matter block that configures the cover page and styling:

```yaml
---
title: Custom Website & Client Platform Proposal
client_name: Sarah Lotz, LMT — Owner
client_company: Allevo Therapeutic Bodywork LLC
client_logo: client-logo.png
lg_logo: learned-geek-logo.png
date: March 2026
accent: "#5a8548"
accent_light: "#e8f0e4"
closing_quote: "Your memorable closing statement here."
---
```

| Field | Required | Default | Description |
|-------|----------|---------|-------------|
| `title` | Yes | | Proposal title (cover page and `<title>` tag) |
| `client_name` | Yes | | Client contact name |
| `client_company` | Yes | | Client business name |
| `client_logo` | No | *(none)* | Path to client logo image, relative to the HTML output |
| `lg_logo` | No | `learned-geek-logo.png` | Path to Learned Geek logo |
| `date` | Yes | | Date shown on cover page |
| `accent` | No | `#4a90d9` | Accent color (hex) — used for h4, list markers, badges, etc. |
| `accent_light` | No | `#e8f0fb` | Light accent color (hex) — used for callout backgrounds |
| `closing_quote` | No | *(none)* | If provided, generates a closing page with this quote |

### Image Paths

All image paths in the front matter are **relative to the generated HTML file**. Place logos in the same directory as your markdown, or use relative paths like `images/logo.png`.

Copy `learned-geek-logo.png` from `docs/templates/` into your proposal directory.

## Markdown Conventions

### Standard Markdown (handled by Markdig)

Everything you'd expect works: headings, bold, italic, lists, tables, links, inline code. The tool uses the same Markdig pipeline (`UseAdvancedExtensions()`) as the blog.

### Page Breaks — `---`

Horizontal rules (`---`) create page breaks in the printed output. Each `---` starts a new page.

```markdown
## Section One

Content...

---

## Section Two (new page)
```

### Section Dividers — `<!-- section-divider -->`

For visual dividers *within* a page (a subtle horizontal line without a page break):

```markdown
Some content above.

<!-- section-divider -->

## Another Section (same page)
```

### Comparison Blocks — `:::comparison`

Uses Markdig's **Custom Containers** extension. Renders as a boxed callout with an auto-generated "Compared to industry:" label.

```markdown
:::comparison
Your current site uses a Wix template. A custom build means every pixel
reflects your brand — not "Template #47,000."
:::
```

### Callout Boxes — `:::callout` / `:::callout-sage` / `:::callout-warning`

Uses Markdig's **Custom Containers** extension. Three variants:

```markdown
:::callout
**Note:** Standard blue-accent callout.
:::

:::callout-sage
**Highlight:** Client-accent colored callout (uses your --accent color).
:::

:::callout-warning
**Important:** Amber warning callout.
:::
```

### Differentiators — `:::diff`

Custom block for numbered feature comparisons. Each item starts with a numbered bold title, followed by `Industry:` and `Allevo:` (or `What we do:`) lines.

```markdown
:::diff
1. **Interactive Body Map**
Industry: Jane App has body diagrams but they're therapist-side only.
Allevo: Clients tap where they hurt and watch regions change over time.

2. **Visual Progress Charts**
Industry: No massage platform shows clients their pain on a chart.
Allevo: Clear charts showing improvement percentage over sessions.
:::
```

Renders as numbered badge circles with structured industry/differentiator content.

### Journey / Timeline — `:::journey`

Custom block for step-by-step timelines. Each step starts with a bold header in the format `**Time — Label**`.

```markdown
:::journey
**Day 0 — Discovery**
A potential client finds your article on Google and clicks through.

**Day 1 — Intake**
They receive a text to complete their health history online.

**Week 8 — Progress**
They open their portal and see their pain dropped 71%.
:::
```

The time portion (`Day 0`) becomes the left-aligned label, and the step name (`Discovery`) appears below it.

### Phase Blocks — `:::phase`

Custom block for delivery phases. The phase title goes on the `:::phase` line. Include a `Milestone:` line for the phase milestone.

```markdown
:::phase Phase 1 — Foundation
Replace the existing site with a modern platform.
- Digital intake with body map
- Smart booking quiz
- SEO foundation
Milestone: Site goes live. Old platform can be cancelled.
:::
```

### Signature Block — `:::signatures`

Custom block for agreement signatures. Each line is `Name | Company`.

```markdown
:::signatures
Mark McArthey | Learned Geek
Sarah Lotz, LMT | Allevo Therapeutic Bodywork LLC
:::
```

### Cost Tables (Auto-Detected)

Standard markdown tables where any column header contains "Cost" are automatically styled as cost tables (right-aligned last column, accent-colored values). Rows containing "Total" in the first cell get highlighted as summary rows.

```markdown
| Service | What It Does | Monthly Cost |
|---------|-------------|-------------|
| Hosting | Where the site lives | Covered |
| Stripe | Payment processing | 2.9% + $0.30/txn |
| **Estimated total** | | **~$15–35/month** |
```

## Accent Colors

Set `accent` and `accent_light` in the front matter to match the client's brand:

```yaml
# Sage green (wellness/bodywork)
accent: "#5a8548"
accent_light: "#e8f0e4"

# Warm gold
accent: "#b8860b"
accent_light: "#fdf6e3"

# Deep blue (default)
accent: "#4a90d9"
accent_light: "#e8f0fb"

# Coral
accent: "#c75c3a"
accent_light: "#fde8e2"
```

## Project Structure

```
tools/ProposalGenerator/
  Program.cs                  — CLI entry point, Markdig pipeline
  ProposalConfig.cs           — YAML front matter model
  MarkdownPreProcessor.cs     — Transforms :::journey, :::diff, :::phase, :::signatures
  HtmlPostProcessor.cs        — Cost table styling, page breaks, section dividers
  ProposalAssembler.cs        — Template shell, cover page, closing page, CSS
```

## Creating a New Proposal

1. Create a directory for the client: `docs/clientname/`
2. Copy `docs/templates/learned-geek-logo.png` into it
3. Copy the client's logo if available
4. Create `PROPOSAL.md` with the YAML front matter and content
5. Run: `dotnet run --project tools/ProposalGenerator -- docs/clientname/PROPOSAL.md`
6. Open the generated HTML in Chrome/Edge → `Ctrl+P` → Save as PDF

## Print Tips

- Use Chrome or Edge for best results
- Set margins to "Default"
- Check "Background graphics" to preserve table headers and accent colors
- Pages break automatically at each `---`
