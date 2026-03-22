using System.Text.RegularExpressions;
using Markdig;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ProposalGenerator;

if (args.Length < 1)
{
    Console.WriteLine("Usage: ProposalGenerator <input.md> [output.html]");
    Console.WriteLine();
    Console.WriteLine("Converts a markdown proposal into a polished, print-ready HTML document.");
    Console.WriteLine("If output is not specified, writes to <input-name>-PRINT.html in the same directory.");
    return 1;
}

var inputPath = Path.GetFullPath(args[0]);
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: File not found: {inputPath}");
    return 1;
}

var outputPath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.Combine(
        Path.GetDirectoryName(inputPath)!,
        Path.GetFileNameWithoutExtension(inputPath) + "-PRINT.html");

Console.WriteLine($"Input:  {inputPath}");
Console.WriteLine($"Output: {outputPath}");

// 1. Read the markdown file
var rawMarkdown = await File.ReadAllTextAsync(inputPath);

// 2. Extract YAML front matter
var config = ExtractFrontMatter(rawMarkdown, out var markdownBody);

// 3. Pre-process: transform structural blocks (:::journey, :::diff, :::phase, :::signatures)
//    into raw HTML that Markdig will pass through
var preprocessed = MarkdownPreProcessor.Process(markdownBody);

// 4. Convert markdown to HTML using Markdig with the same extensions as the blog
var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();

var contentHtml = Markdown.ToHtml(preprocessed, pipeline);

// 5. Post-process: add CSS classes to cost tables, convert <hr> to page breaks, etc.
contentHtml = HtmlPostProcessor.Process(contentHtml);

// 6. Assemble: wrap in template shell with cover page, CSS, and closing page
var finalHtml = ProposalAssembler.Assemble(contentHtml, config);

// 7. Write output
var outputDir = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
    Directory.CreateDirectory(outputDir);

await File.WriteAllTextAsync(outputPath, finalHtml);

Console.WriteLine($"Generated: {outputPath}");
return 0;

// --- Helper methods ---

static ProposalConfig ExtractFrontMatter(string markdown, out string body)
{
    var config = new ProposalConfig();

    // Match YAML front matter between --- delimiters at the start of the file
    var match = Regex.Match(markdown, @"^---\s*\n([\s\S]*?)\n---\s*\n", RegexOptions.Multiline);
    if (match.Success)
    {
        var yaml = match.Groups[1].Value;
        body = markdown[(match.Index + match.Length)..];

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        try
        {
            config = deserializer.Deserialize<ProposalConfig>(yaml) ?? config;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Could not parse YAML front matter: {ex.Message}");
            Console.Error.WriteLine("Using default config values.");
        }
    }
    else
    {
        body = markdown;
        Console.Error.WriteLine("Warning: No YAML front matter found. Using default config values.");
    }

    return config;
}
