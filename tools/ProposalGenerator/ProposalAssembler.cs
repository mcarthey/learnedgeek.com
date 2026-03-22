using System.Text;

namespace ProposalGenerator;

/// <summary>
/// Assembles the final proposal HTML from processed content and config.
/// Wraps content in the template's CSS shell with cover page and closing page.
/// </summary>
public static class ProposalAssembler
{
    public static string Assemble(string contentHtml, ProposalConfig config)
    {
        var sb = new StringBuilder();

        sb.AppendLine(GenerateHead(config));
        sb.AppendLine(GenerateCoverPage(config));
        sb.AppendLine(GenerateContentWrapper(contentHtml));

        if (!string.IsNullOrEmpty(config.ClosingQuote))
            sb.AppendLine(GenerateClosingPage(config));

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string GenerateHead(ProposalConfig config)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{config.Title} — Proposal | Learned Geek</title>
  <link rel=""preconnect"" href=""https://fonts.googleapis.com"">
  <link rel=""preconnect"" href=""https://fonts.gstatic.com"" crossorigin>
  <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=Playfair+Display:ital,wght@0,400;0,500;0,600;1,400&display=swap"" rel=""stylesheet"">
  <style>
    :root {{
      --brand-dark: #1a1a2e;
      --brand-mid: #2d2d44;
      --brand-accent: #4a90d9;
      --brand-accent-light: #e8f0fb;
      --text-primary: #1a1a2e;
      --text-secondary: #555568;
      --text-light: #8888a0;
      --border: #e0e0e8;
      --bg-subtle: #f8f8fb;
      --bg-warm: #faf9f7;
      --white: #ffffff;
      --accent: {config.Accent};
      --accent-light: {config.AccentLight};
    }}

    @page {{
      size: letter;
      margin: 0.75in 0.85in;
    }}

    * {{ box-sizing: border-box; margin: 0; padding: 0; }}

    body {{
      font-family: 'Inter', -apple-system, sans-serif;
      font-size: 10.5pt;
      line-height: 1.65;
      color: var(--text-primary);
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }}

    /* Cover Page */
    .cover {{
      height: 100vh;
      display: flex;
      flex-direction: column;
      justify-content: center;
      align-items: center;
      text-align: center;
      page-break-after: always;
      break-after: page;
      position: relative;
      background: var(--white);
    }}
    .cover__lg-logo {{ width: 160px; margin-bottom: 2rem; opacity: 0.9; }}
    .cover__divider {{ width: 60px; height: 2px; background: var(--brand-accent); margin: 1.5rem auto; }}
    .cover__presents {{ font-size: 9pt; letter-spacing: 0.15em; text-transform: uppercase; color: var(--text-light); margin-bottom: 2.5rem; }}
    .cover__title {{ font-family: 'Playfair Display', Georgia, serif; font-size: 28pt; font-weight: 400; color: var(--text-primary); line-height: 1.2; margin-bottom: 0.5rem; }}
    .cover__subtitle {{ font-size: 12pt; font-weight: 300; color: var(--text-secondary); letter-spacing: 0.03em; margin-bottom: 3rem; }}
    .cover__client-logo {{ width: 180px; margin-bottom: 1rem; }}
    .cover__meta {{ font-size: 9pt; color: var(--text-light); line-height: 1.8; margin-top: 2.5rem; }}
    .cover__meta strong {{ color: var(--text-secondary); font-weight: 500; }}
    .cover__footer {{ position: absolute; bottom: 0; left: 0; right: 0; padding: 1rem; text-align: center; font-size: 8pt; color: var(--text-light); border-top: 1px solid var(--border); }}

    /* Page Breaks */
    .page-break {{ page-break-before: always; break-before: page; }}

    /* Typography */
    h1 {{ font-family: 'Playfair Display', Georgia, serif; font-size: 20pt; font-weight: 400; color: var(--text-primary); margin-bottom: 0.75rem; padding-bottom: 0.5rem; border-bottom: 2px solid var(--brand-dark); }}
    h2 {{ font-family: 'Playfair Display', Georgia, serif; font-size: 15pt; font-weight: 400; color: var(--text-primary); margin-top: 1.5rem; margin-bottom: 0.5rem; }}
    h3 {{ font-family: 'Inter', sans-serif; font-size: 11pt; font-weight: 600; color: var(--brand-mid); margin-top: 1.25rem; margin-bottom: 0.35rem; text-transform: uppercase; letter-spacing: 0.04em; }}
    h4 {{ font-family: 'Inter', sans-serif; font-size: 10.5pt; font-weight: 600; color: var(--accent); margin-top: 1rem; margin-bottom: 0.3rem; }}
    p {{ margin-bottom: 0.6rem; color: var(--text-secondary); }}
    strong {{ color: var(--text-primary); font-weight: 600; }}
    em {{ color: var(--text-secondary); }}
    a {{ color: var(--brand-accent); text-decoration: none; }}

    /* Lists */
    ul, ol {{ margin: 0.4rem 0 0.8rem 1.25rem; color: var(--text-secondary); }}
    li {{ margin-bottom: 0.3rem; padding-left: 0.25rem; }}
    li::marker {{ color: var(--accent); }}

    /* Tables */
    table {{ width: 100%; border-collapse: collapse; margin: 0.75rem 0 1rem; font-size: 9.5pt; }}
    thead {{ background: var(--brand-dark); color: var(--white); }}
    th {{ padding: 0.5rem 0.65rem; text-align: left; font-weight: 500; font-size: 8.5pt; letter-spacing: 0.03em; text-transform: uppercase; }}
    td {{ padding: 0.45rem 0.65rem; border-bottom: 1px solid var(--border); vertical-align: top; }}
    tbody tr:nth-child(even) {{ background: var(--bg-subtle); }}

    /* Cost Tables */
    .cost-highlight td:last-child {{ font-weight: 600; color: var(--accent); }}
    .cost-total {{ background: var(--accent-light) !important; }}
    .cost-total td {{ font-weight: 700 !important; color: var(--text-primary) !important; border-bottom: 2px solid var(--accent); }}

    /* Callout Boxes — rendered by Markdig CustomContainers as <div class=""callout""> */
    .callout {{ background: var(--bg-subtle); border-left: 3px solid var(--brand-accent); padding: 0.75rem 1rem; margin: 0.75rem 0; border-radius: 0 4px 4px 0; }}
    .callout-sage, .callout--sage {{ background: var(--accent-light); border-left: 3px solid var(--accent); padding: 0.75rem 1rem; margin: 0.75rem 0; border-radius: 0 4px 4px 0; }}
    .callout-warning, .callout--warning {{ background: #fef9e7; border-left: 3px solid #d4a017; padding: 0.75rem 1rem; margin: 0.75rem 0; border-radius: 0 4px 4px 0; }}
    .callout p, .callout-sage p, .callout-warning p, .callout--sage p, .callout--warning p {{ margin-bottom: 0.25rem; font-size: 10pt; }}
    .callout p:last-child, .callout-sage p:last-child, .callout-warning p:last-child {{ margin-bottom: 0; }}

    /* Comparison Blocks — rendered by Markdig CustomContainers as <div class=""comparison""> */
    .comparison {{ background: var(--bg-subtle); border: 1px solid var(--border); border-radius: 4px; padding: 0.6rem 0.85rem; margin: 0.5rem 0 0.75rem; font-size: 9.5pt; color: var(--text-secondary); }}
    .comparison::before {{ content: 'Compared to industry:'; display: block; font-size: 8pt; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; color: var(--text-light); margin-bottom: 0.2rem; }}

    /* Differentiators */
    .diff-item {{ display: flex; gap: 0.75rem; margin-bottom: 0.85rem; padding-bottom: 0.85rem; border-bottom: 1px solid var(--border); }}
    .diff-item:last-child {{ border-bottom: none; }}
    .diff-number {{ width: 28px; height: 28px; background: var(--accent); color: var(--white); border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 9pt; font-weight: 700; flex-shrink: 0; margin-top: 2px; }}
    .diff-content {{ flex: 1; }}
    .diff-content h4 {{ margin-top: 0; margin-bottom: 0.15rem; color: var(--text-primary); font-size: 10pt; }}
    .diff-content p {{ font-size: 9.5pt; margin-bottom: 0.2rem; }}
    .diff-label {{ font-size: 8pt; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; color: var(--text-light); }}

    /* Journey Steps */
    .journey-step {{ display: flex; gap: 0.75rem; margin-bottom: 0.6rem; }}
    .journey-step__label {{ width: 90px; flex-shrink: 0; font-size: 8.5pt; font-weight: 600; color: var(--accent); text-transform: uppercase; letter-spacing: 0.03em; padding-top: 2px; }}
    .journey-step__content {{ flex: 1; padding-bottom: 0.6rem; border-bottom: 1px solid var(--border); font-size: 9.5pt; color: var(--text-secondary); }}
    .journey-step:last-child .journey-step__content {{ border-bottom: none; }}

    /* Phase Blocks */
    .phase-block {{ background: var(--bg-subtle); border-radius: 4px; padding: 0.75rem 1rem; margin-bottom: 0.75rem; }}
    .phase-block h3 {{ margin-top: 0; color: var(--accent); }}
    .phase-block p {{ font-size: 9.5pt; font-style: italic; margin-bottom: 0.35rem; }}
    .phase-block ul {{ margin-bottom: 0.35rem; }}
    .phase-milestone {{ font-size: 9pt; font-weight: 600; color: var(--brand-dark); border-top: 1px solid var(--border); padding-top: 0.4rem; margin-top: 0.35rem; }}

    /* Signature Block */
    .signature-block {{ margin-top: 2rem; display: grid; grid-template-columns: 1fr 1fr; gap: 2.5rem; }}
    .signature-block__party h4 {{ color: var(--text-primary); font-size: 10pt; margin-bottom: 0.15rem; margin-top: 0; }}
    .signature-block__party p {{ font-size: 9pt; color: var(--text-light); margin-bottom: 1.5rem; }}
    .signature-line {{ border-bottom: 1px solid var(--text-primary); margin-bottom: 0.25rem; height: 2rem; }}
    .signature-label {{ font-size: 8pt; color: var(--text-light); }}

    /* Section Divider */
    .section-divider {{ border: none; height: 1px; background: linear-gradient(to right, var(--border), transparent); margin: 1.5rem 0; }}

    /* Footer */
    .page-footer {{ margin-top: 2rem; padding-top: 0.75rem; border-top: 1px solid var(--border); text-align: center; font-size: 8pt; color: var(--text-light); }}

    /* Closing Page */
    .closing-page {{ display: flex; flex-direction: column; justify-content: center; align-items: center; min-height: 50vh; text-align: center; }}
    .closing-page__quote {{ font-family: 'Playfair Display', Georgia, serif; font-size: 16pt; font-style: italic; color: var(--text-secondary); max-width: 500px; line-height: 1.6; margin-bottom: 2rem; }}
    .closing-page__logo {{ width: 60px; opacity: 0.6; margin-bottom: 0.5rem; }}
    .closing-page__byline {{ font-size: 8.5pt; color: var(--text-light); }}

    /* Print */
    @media print {{
      body {{ font-size: 10pt; }}
      .cover {{ height: 100vh; }}
      .no-print {{ display: none; }}
      a {{ color: var(--text-primary); }}
      thead {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
    }}

    /* Screen Preview */
    @media screen {{
      body {{ max-width: 8.5in; margin: 0 auto; padding: 0.5in; background: #eee; }}
      .cover, .page-break {{ background: white; padding: 0.75in 0.85in; margin-bottom: 0.25in; box-shadow: 0 1px 4px rgba(0,0,0,0.1); }}
      .page-break {{ padding-top: 0.75in; }}
    }}
  </style>
</head>
<body>";
    }

    private static string GenerateCoverPage(ProposalConfig config)
    {
        var clientLogoHtml = string.IsNullOrEmpty(config.ClientLogo)
            ? ""
            : $"\n    <img src=\"{config.ClientLogo}\" alt=\"{config.ClientCompany}\" class=\"cover__client-logo\">";

        return $@"
  <div class=""cover"">
    <img src=""{config.LgLogo}"" alt=""Learned Geek"" class=""cover__lg-logo"">
    <div class=""cover__divider""></div>
    <p class=""cover__presents"">presents</p>

    <div class=""cover__title"">{config.Title}</div>
    <p class=""cover__subtitle"">Prepared for {config.ClientCompany}</p>
{clientLogoHtml}
    <div class=""cover__meta"">
      <strong>Prepared for:</strong> {config.ClientName}<br>
      <strong>Prepared by:</strong> Mark McArthey — Learned Geek<br>
      <strong>Date:</strong> {config.Date}
    </div>

    <div class=""cover__footer"">
      Learned Geek &nbsp;&middot;&nbsp; learnedgeek.com &nbsp;&middot;&nbsp; Confidential
    </div>
  </div>";
    }

    private static string GenerateContentWrapper(string contentHtml)
    {
        // The content already has page-break divs inserted by post-processing.
        // We need to wrap the first section in a page-break div.
        return $@"
  <div class=""page-break"">
{contentHtml}
  </div>";
    }

    private static string GenerateClosingPage(ProposalConfig config)
    {
        return $@"
  <div class=""page-break closing-page"">
    <p class=""closing-page__quote"">
      {config.ClosingQuote}
    </p>

    <div class=""cover__divider""></div>

    <img src=""{config.LgLogo}"" alt=""Learned Geek"" class=""closing-page__logo"">
    <p class=""closing-page__byline"">
      Learned Geek &nbsp;&middot;&nbsp; learnedgeek.com<br>
      Mark McArthey
    </p>

    <div class=""page-footer"" style=""border: none; margin-top: 3rem;"">
      <p>This proposal is confidential and prepared exclusively for {config.ClientCompany}.</p>
    </div>
  </div>";
    }
}
