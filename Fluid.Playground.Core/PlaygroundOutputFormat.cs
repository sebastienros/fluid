using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fluid.Playground;

/// <summary>Detects the format of a rendered output, to color it with the matching grammar.</summary>
public static partial class PlaygroundOutputFormat
{
    /// <summary>Outputs longer than this are not detected nor colored, so that typing stays responsive.</summary>
    public const int MaxLength = 200_000;

    /// <summary>
    /// The format of <paramref name="output"/>, or <see langword="null"/> for plain text. In order:
    /// <list type="number">
    /// <item><see cref="PlaygroundLanguage.Json"/>: a valid JSON object or array.</item>
    /// <item><see cref="PlaygroundLanguage.Xml"/> or <see cref="PlaygroundLanguage.Html"/>: an XML declaration or an HTML
    /// doctype; a text that starts with an element is XML when its root is not an HTML element, else HTML.</item>
    /// <item><see cref="PlaygroundLanguage.CSharp"/>: <c>using</c> or <c>namespace</c> directives, or a type declared
    /// with an access modifier.</item>
    /// <item><see cref="PlaygroundLanguage.Markdown"/>: a heading, a code fence, or two other Markdown constructs (list
    /// items, quotes, links, emphasis, tables).</item>
    /// <item><see cref="PlaygroundLanguage.Html"/>: an HTML tag anywhere in the text.</item>
    /// </list>
    /// </summary>
    public static PlaygroundLanguage? Detect(string output)
    {
        if (output.Length > MaxLength)
        {
            return null;
        }

        var text = output.AsSpan().Trim();
        if (text.IsEmpty)
        {
            return null;
        }

        if ((text[0] == '{' && text[^1] == '}') || (text[0] == '[' && text[^1] == ']'))
        {
            return IsJson(text) ? PlaygroundLanguage.Json : null;
        }

        if (text.StartsWith("<?xml", StringComparison.Ordinal))
        {
            return PlaygroundLanguage.Xml;
        }

        if (text.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase))
        {
            return PlaygroundLanguage.Html;
        }

        if (text[0] == '<')
        {
            // A document whose root element is not an HTML element, closed at its end: <rss>...</rss>, <svg>, <ns:root>.
            if (RootElement().Match(output.Trim()) is { Success: true } root && !HtmlElements.Contains(root.Groups["name"].Value))
            {
                return PlaygroundLanguage.Xml;
            }

            return HasHtmlTag(output) ? PlaygroundLanguage.Html : null;
        }

        // Before HTML: XML documentation comments (/// <summary>) and generics are not HTML.
        if (CSharpDirective().IsMatch(output) || CSharpType().IsMatch(output))
        {
            return PlaygroundLanguage.CSharp;
        }

        // Before HTML too: Markdown often contains inline HTML.
        if (IsMarkdown(output))
        {
            return PlaygroundLanguage.Markdown;
        }

        return HasHtmlTag(output) ? PlaygroundLanguage.Html : null;
    }

    private static bool HasHtmlTag(string output)
    {
        foreach (Match tag in Tag().Matches(output))
        {
            if (HtmlElements.Contains(tag.Groups["name"].Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMarkdown(string output)
    {
        if (MarkdownHeading().IsMatch(output) || MarkdownFence().IsMatch(output))
        {
            return true;
        }

        var constructs = 0;
        foreach (var construct in (ReadOnlySpan<Regex>)[MarkdownListItem(), MarkdownQuote(), MarkdownLink(), MarkdownEmphasis(), MarkdownTable()])
        {
            if (construct.IsMatch(output) && ++constructs == 2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsJson(ReadOnlySpan<char> text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text.ToString());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // An element that opens the text and closes it with the same name.
    [GeneratedRegex(@"\A<(?<name>[A-Za-z_][\w.:-]*)[\s/>][\s\S]*</\k<name>\s*>\z")]
    private static partial Regex RootElement();

    [GeneratedRegex(@"</?(?<name>[A-Za-z][A-Za-z0-9-]*)(?:\s[^<>]*)?/?>")]
    private static partial Regex Tag();

    // using System; using static System.Math; using Alias = System.Text; namespace Contoso.App; namespace Contoso {
    [GeneratedRegex(@"^\s*(?:using\s+(?:static\s+)?[A-Za-z_][\w.]*(?:\s*=\s*[\w.<>]+)?\s*;|namespace\s+[A-Za-z_][\w.]*\s*[;{]?\s*$)", RegexOptions.Multiline)]
    private static partial Regex CSharpDirective();

    // public sealed partial class Product, internal record Order(...), public readonly struct Point, public enum Color
    [GeneratedRegex(@"^\s*(?:\[[^\]\r\n]*\]\s*)*(?:public|internal|private|protected)(?:\s+(?:static|sealed|abstract|partial|readonly|ref|unsafe|file))*\s+(?:class|record|struct|interface|enum)\s+[A-Za-z_]\w*", RegexOptions.Multiline)]
    private static partial Regex CSharpType();

    [GeneratedRegex(@"^#{1,6}[ \t]+\S", RegexOptions.Multiline)]
    private static partial Regex MarkdownHeading();

    [GeneratedRegex(@"^[ \t]{0,3}(?:```|~~~)", RegexOptions.Multiline)]
    private static partial Regex MarkdownFence();

    [GeneratedRegex(@"^[ \t]{0,3}(?:[-*+]|\d{1,9}[.)])[ \t]+\S", RegexOptions.Multiline)]
    private static partial Regex MarkdownListItem();

    [GeneratedRegex(@"^[ \t]{0,3}>[ \t]?\S", RegexOptions.Multiline)]
    private static partial Regex MarkdownQuote();

    [GeneratedRegex(@"!?\[[^\]\r\n]+\]\([^)\s]+\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"(?:\*\*|__)\S[^\r\n]*?\S(?:\*\*|__)|`[^`\r\n]+`")]
    private static partial Regex MarkdownEmphasis();

    [GeneratedRegex(@"^\|?[ \t]*:?-{3,}:?[ \t]*\|", RegexOptions.Multiline)]
    private static partial Regex MarkdownTable();

    private static readonly HashSet<string> HtmlElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "abbr", "address", "area", "article", "aside", "audio", "b", "base", "bdi", "bdo", "blockquote", "body", "br",
        "button", "canvas", "caption", "cite", "code", "col", "colgroup", "data", "datalist", "dd", "del", "details", "dfn",
        "dialog", "div", "dl", "dt", "em", "embed", "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3",
        "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html", "i", "iframe", "img", "input", "ins", "kbd", "label",
        "legend", "li", "link", "main", "map", "mark", "menu", "meta", "meter", "nav", "noscript", "object", "ol",
        "optgroup", "option", "output", "p", "picture", "pre", "progress", "q", "rp", "rt", "ruby", "s", "samp", "script",
        "search", "section", "select", "slot", "small", "source", "span", "strong", "style", "sub", "summary", "sup",
        "table", "tbody", "td", "template", "textarea", "tfoot", "th", "thead", "time", "title", "tr", "track", "u", "ul",
        "var", "video", "wbr",
    };
}
