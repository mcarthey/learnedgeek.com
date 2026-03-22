using System.Text.RegularExpressions;

namespace ProposalGenerator;

/// <summary>
/// Post-processes Markdig's HTML output to apply proposal template styling.
/// Handles patterns that are standard markdown but need CSS class additions
/// or structural changes for the proposal template.
/// </summary>
public static class HtmlPostProcessor
{
    public static string Process(string html)
    {
        html = ConvertHorizontalRulesToPageBreaks(html);
        html = StyleCostTables(html);
        html = ConvertSectionDividers(html);
        return html;
    }

    /// <summary>
    /// Converts &lt;hr&gt; tags into page break divs.
    /// Each &lt;hr&gt; closes the current page div and opens a new one.
    /// The first content section doesn't need a closing div before it.
    /// </summary>
    private static string ConvertHorizontalRulesToPageBreaks(string html)
    {
        // Markdig produces <hr /> — replace with page break structure
        // We wrap the content later in the assembler, so here we just
        // insert the close/open div pair
        html = html.Replace("<hr />", "</div>\n<div class=\"page-break\">");
        return html;
    }

    /// <summary>
    /// Detects tables where a column header contains "Cost" and adds cost-highlight class.
    /// Detects rows where the first cell contains "Total" and adds cost-total class.
    /// Also right-aligns the last column in cost tables.
    /// </summary>
    private static string StyleCostTables(string html)
    {
        // Find tables that have a "Cost" or "Monthly Cost" header
        var tablePattern = @"<table>([\s\S]*?)</table>";
        html = Regex.Replace(html, tablePattern, match =>
        {
            var tableContent = match.Groups[1].Value;

            // Check if any <th> contains "Cost" (case-insensitive)
            var isCostTable = Regex.IsMatch(tableContent, @"<th[^>]*>.*?cost.*?</th>", RegexOptions.IgnoreCase);

            if (isCostTable)
            {
                // Add cost-highlight class to table
                var result = $"<table class=\"cost-highlight\">{tableContent}</table>";

                // Add cost-total class to rows containing "Total"
                result = Regex.Replace(result, @"<tr>\s*(<td[^>]*>.*?total.*?</td>)", match2 =>
                {
                    return $"<tr class=\"cost-total\">\n{match2.Groups[1].Value}";
                }, RegexOptions.IgnoreCase);

                // Right-align the last <th> and last <td> in each row
                result = Regex.Replace(result, @"(<th>)((?:(?!</th>).)*)(</th>)\s*</tr>",
                    m => $"<th style=\"text-align:right;\">{m.Groups[2].Value}</th>\n</tr>");
                result = Regex.Replace(result, @"(<td>)((?:(?!</td>).)*)(</td>)\s*</tr>",
                    m => $"<td style=\"text-align:right;\">{m.Groups[2].Value}</td>\n</tr>");

                return result;
            }

            return match.Value;
        });

        return html;
    }

    /// <summary>
    /// Converts &lt;hr class="section-divider"&gt; markers.
    /// In the markdown, we use *** (three asterisks) for section dividers
    /// vs --- for page breaks.
    /// Markdig renders both as &lt;hr /&gt; but we can differentiate using
    /// a custom marker approach. For now, we handle this in the assembler.
    /// </summary>
    private static string ConvertSectionDividers(string html)
    {
        // If we used HTML comments as markers in the markdown pre-processing:
        html = html.Replace("<!-- section-divider -->", "<hr class=\"section-divider\">");
        return html;
    }
}
