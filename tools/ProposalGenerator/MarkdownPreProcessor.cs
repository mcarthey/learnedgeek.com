using System.Text;
using System.Text.RegularExpressions;

namespace ProposalGenerator;

/// <summary>
/// Pre-processes markdown before Markdig conversion.
/// Transforms custom container blocks (:::type) into the proposal template's HTML components.
/// Standard containers like :::callout and :::comparison are left for Markdig's
/// CustomContainers extension. This handles the ones that need structural HTML
/// beyond what Markdig's container renderer produces.
/// </summary>
public static class MarkdownPreProcessor
{
    /// <summary>
    /// Process custom blocks that need structural HTML transformation.
    /// Markdig's CustomContainers extension handles :::callout, :::comparison, etc.
    /// by wrapping content in div with the class name. We handle blocks that need
    /// more complex HTML structure (journey, diff, phase, signatures).
    /// </summary>
    public static string Process(string markdown)
    {
        markdown = ProcessJourneyBlocks(markdown);
        markdown = ProcessDiffBlocks(markdown);
        markdown = ProcessPhaseBlocks(markdown);
        markdown = ProcessSignatureBlocks(markdown);
        return markdown;
    }

    /// <summary>
    /// Converts :::journey blocks into journey-step HTML.
    ///
    /// Input format:
    /// :::journey
    /// **Day 0 — Discovery**
    /// Sarah's article ranks on Google...
    ///
    /// **Day 1 — Intake**
    /// Emily gets a text...
    /// :::
    ///
    /// Each bold line starts a new step. The label is extracted from the bold text
    /// (before the dash = time label, after = step name).
    /// </summary>
    private static string ProcessJourneyBlocks(string markdown)
    {
        var pattern = @":::journey\s*\n([\s\S]*?):::";
        return Regex.Replace(markdown, pattern, match =>
        {
            var content = match.Groups[1].Value;
            var sb = new StringBuilder();

            // Split on bold lines that start a new step: **Time — Label**
            var stepPattern = @"\*\*(.+?)\*\*\s*\n([\s\S]*?)(?=\*\*|\z)";
            var steps = Regex.Matches(content, stepPattern);

            foreach (Match step in steps)
            {
                var header = step.Groups[1].Value.Trim();
                var body = step.Groups[2].Value.Trim();

                if (string.IsNullOrEmpty(body)) continue;

                // Split header on " — " or " - " to get time label and step name
                string timeLabel, stepName;
                var dashIndex = header.IndexOf(" — ", StringComparison.Ordinal);
                if (dashIndex < 0) dashIndex = header.IndexOf(" - ", StringComparison.Ordinal);

                if (dashIndex >= 0)
                {
                    timeLabel = header[..dashIndex].Trim();
                    stepName = header[(dashIndex + 3)..].Trim();
                }
                else
                {
                    timeLabel = header;
                    stepName = "";
                }

                var label = string.IsNullOrEmpty(stepName)
                    ? timeLabel
                    : $"{timeLabel}<br>{stepName}";

                sb.AppendLine("<div class=\"journey-step\">");
                sb.AppendLine($"  <div class=\"journey-step__label\">{label}</div>");
                sb.AppendLine($"  <div class=\"journey-step__content\">{body}</div>");
                sb.AppendLine("</div>");
            }

            return sb.ToString();
        });
    }

    /// <summary>
    /// Converts :::diff blocks into diff-item HTML.
    ///
    /// Input format:
    /// :::diff
    /// 1. **Interactive Body Map**
    /// Industry: Jane App has body diagrams but therapist-side only.
    /// Allevo: Clients tap where they hurt and watch regions change over time.
    ///
    /// 2. **Visual Progress Charts**
    /// Industry: No massage platform shows clients their pain on a chart.
    /// Allevo: Clear charts showing improvement over sessions.
    /// :::
    /// </summary>
    private static string ProcessDiffBlocks(string markdown)
    {
        var pattern = @":::diff\s*\n([\s\S]*?):::";
        return Regex.Replace(markdown, pattern, match =>
        {
            var content = match.Groups[1].Value;
            var sb = new StringBuilder();

            // Split on numbered items: "1. **Title**"
            var itemPattern = @"(\d+)\.\s+\*\*(.+?)\*\*\s*\n([\s\S]*?)(?=\d+\.\s+\*\*|\z)";
            var items = Regex.Matches(content, itemPattern);

            foreach (Match item in items)
            {
                var number = item.Groups[1].Value;
                var title = item.Groups[2].Value.Trim();
                var body = item.Groups[3].Value.Trim();

                // Look for "Industry:" and "Allevo:" (or similar) labels
                var industryMatch = Regex.Match(body, @"^(?:Industry|Industry status|What exists)[:\s]*(.+?)(?:\n|$)", RegexOptions.IgnoreCase);
                var allevoMatch = Regex.Match(body, @"(?:Allevo|What we do|What .+ does)[:\s]*(.+?)(?:\n|$)", RegexOptions.IgnoreCase);

                sb.AppendLine("<div class=\"diff-item\">");
                sb.AppendLine($"  <div class=\"diff-number\">{number}</div>");
                sb.AppendLine("  <div class=\"diff-content\">");
                sb.AppendLine($"    <h4>{title}</h4>");

                if (industryMatch.Success && allevoMatch.Success)
                {
                    sb.AppendLine("    <p class=\"diff-label\">Industry status</p>");
                    sb.AppendLine($"    <p>{industryMatch.Groups[1].Value.Trim()}</p>");
                    sb.AppendLine("    <p class=\"diff-label\">What Allevo does</p>");
                    sb.AppendLine($"    <p>{allevoMatch.Groups[1].Value.Trim()}</p>");
                }
                else
                {
                    // Fallback: render body paragraphs as-is
                    foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        sb.AppendLine($"    <p>{line.Trim()}</p>");
                    }
                }

                sb.AppendLine("  </div>");
                sb.AppendLine("</div>");
            }

            return sb.ToString();
        });
    }

    /// <summary>
    /// Converts :::phase blocks into phase-block HTML.
    ///
    /// Input format:
    /// :::phase Phase 1 — Foundation
    /// Replace Wix with everything you have today.
    /// - Digital intake with body map
    /// - Booking quiz
    /// Milestone: Site goes live.
    /// :::
    /// </summary>
    private static string ProcessPhaseBlocks(string markdown)
    {
        var pattern = @":::phase\s+(.+?)\s*\n([\s\S]*?):::";
        return Regex.Replace(markdown, pattern, match =>
        {
            var title = match.Groups[1].Value.Trim();
            var body = match.Groups[2].Value.Trim();

            var sb = new StringBuilder();
            sb.AppendLine("<div class=\"phase-block\">");
            sb.AppendLine($"  <h3>{title}</h3>");

            // Extract milestone line if present
            var milestoneMatch = Regex.Match(body, @"^Milestone:\s*(.+)$", RegexOptions.Multiline);
            if (milestoneMatch.Success)
            {
                body = body.Replace(milestoneMatch.Value, "").Trim();
            }

            // Process remaining lines: list items and paragraphs
            var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var inList = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith("- "))
                {
                    if (!inList) { sb.AppendLine("  <ul>"); inList = true; }
                    sb.AppendLine($"    <li>{line[2..].Trim()}</li>");
                }
                else
                {
                    if (inList) { sb.AppendLine("  </ul>"); inList = false; }
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine($"  <p>{line}</p>");
                }
            }
            if (inList) sb.AppendLine("  </ul>");

            if (milestoneMatch.Success)
                sb.AppendLine($"  <div class=\"phase-milestone\">Milestone: {milestoneMatch.Groups[1].Value.Trim()}</div>");

            sb.AppendLine("</div>");
            return sb.ToString();
        });
    }

    /// <summary>
    /// Converts :::signatures blocks into signature-block HTML.
    ///
    /// Input format:
    /// :::signatures
    /// Mark McArthey | Learned Geek
    /// Sarah Lotz, LMT | Allevo Therapeutic Bodywork LLC
    /// :::
    /// </summary>
    private static string ProcessSignatureBlocks(string markdown)
    {
        var pattern = @":::signatures\s*\n([\s\S]*?):::";
        return Regex.Replace(markdown, pattern, match =>
        {
            var content = match.Groups[1].Value.Trim();
            var sb = new StringBuilder();
            sb.AppendLine("<div class=\"signature-block\">");

            foreach (var rawLine in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                var parts = line.Split('|', 2);
                var name = parts[0].Trim();
                var company = parts.Length > 1 ? parts[1].Trim() : "";

                sb.AppendLine("  <div class=\"signature-block__party\">");
                sb.AppendLine($"    <h4>{name}</h4>");
                if (!string.IsNullOrEmpty(company))
                    sb.AppendLine($"    <p>{company}</p>");
                sb.AppendLine("    <div class=\"signature-line\"></div>");
                sb.AppendLine("    <span class=\"signature-label\">Signature &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; Date</span>");
                sb.AppendLine("  </div>");
            }

            sb.AppendLine("</div>");
            return sb.ToString();
        });
    }
}
