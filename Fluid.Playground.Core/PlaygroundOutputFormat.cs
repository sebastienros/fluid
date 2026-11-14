using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fluid.Playground;

/// <summary>Detects the format of a rendered output, to color it with the matching grammar.</summary>
public static partial class PlaygroundOutputFormat
{
    /// <summary>Outputs longer than this are not detected nor colored, so that typing stays responsive.</summary>
    public const int MaxLength = 200_000;

    /// <summary>
    /// The format of <paramref name="output"/>: <see cref="PlaygroundLanguage.Json"/> for a valid JSON document,
    /// <see cref="PlaygroundLanguage.Xml"/> for a document that starts with an XML declaration or whose root element is
    /// not an HTML element, <see cref="PlaygroundLanguage.Html"/> for text that contains HTML tags, and
    /// <see langword="null"/> for anything else, plain text included.
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

        // A document whose root element is not an HTML element, closed at its end: <rss>...</rss>, <svg>, <ns:root>.
        if (text[0] == '<' && RootElement().Match(output.Trim()) is { Success: true } root && !HtmlElements.Contains(root.Groups["name"].Value))
        {
            return PlaygroundLanguage.Xml;
        }

        foreach (Match tag in Tag().Matches(output))
        {
            if (HtmlElements.Contains(tag.Groups["name"].Value))
            {
                return PlaygroundLanguage.Html;
            }
        }

        return null;
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
