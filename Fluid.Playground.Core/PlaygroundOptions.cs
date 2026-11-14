using System.Text.Json.Serialization;

namespace Fluid.Playground;

/// <summary>
/// The parser and template options the playground lets users change: the ones that change how a template is read or
/// rendered. Every property defaults to Fluid's default, so that a permalink only stores what was changed.
/// </summary>
public sealed record PlaygroundOptions
{
    /// <summary>The default limit of statements, set so that a template can't freeze the browser tab.</summary>
    public const int DefaultMaxSteps = 1_000_000;

    /// <summary>The default limit of rendered characters.</summary>
    public const int DefaultMaxOutputSize = 2_000_000;

    // Parser options

    /// <summary><see cref="FluidParserOptions.AllowFunctions"/>: the <c>macro</c> and <c>from</c> tags and function calls.</summary>
    [JsonPropertyName("fn")] public bool AllowFunctions { get; init; }

    /// <summary><see cref="FluidParserOptions.AllowParentheses"/>: grouping expressions with parentheses.</summary>
    [JsonPropertyName("par")] public bool AllowParentheses { get; init; }

    /// <summary><see cref="FluidParserOptions.AllowLiquidTag"/>: the <c>liquid</c> tag.</summary>
    [JsonPropertyName("lq")] public bool AllowLiquidTag { get; init; }

    /// <summary><see cref="FluidParserOptions.AllowStringEscapeSequences"/>: <c>\n</c> and other escapes in strings.</summary>
    [JsonPropertyName("esc")] public bool AllowStringEscapeSequences { get; init; }

    /// <summary><see cref="FluidParserOptions.AllowTrailingQuestionMark"/>: identifiers such as <c>product.available?</c>.</summary>
    [JsonPropertyName("qm")] public bool AllowTrailingQuestionMark { get; init; }

    // Rendering

    /// <summary>Renders with an HTML encoder (<c>HtmlEncoder.Create(UnicodeRanges.All)</c>) instead of no encoding.</summary>
    [JsonPropertyName("html")] public bool HtmlEncode { get; init; }

    /// <summary><see cref="TemplateOptions.StrictVariables"/>: an undefined variable is an error.</summary>
    [JsonPropertyName("sv")] public bool StrictVariables { get; init; }

    /// <summary><see cref="TemplateOptions.StrictFilters"/>: an undefined filter is an error.</summary>
    [JsonPropertyName("sf")] public bool StrictFilters { get; init; }

    /// <summary><see cref="TemplateOptions.Trimming"/>: whitespace stripped without hyphens.</summary>
    [JsonPropertyName("trim")] public TrimmingFlags Trimming { get; init; }

    /// <summary><see cref="TemplateOptions.Greedy"/>: trimming strips every whitespace, not only up to the first new line.</summary>
    [JsonPropertyName("greedy")] public bool Greedy { get; init; } = true;

    /// <summary>The name of the culture, empty for the invariant culture.</summary>
    [JsonPropertyName("culture")] public string Culture { get; init; } = "";

    /// <summary>The identifier of the time zone used to parse dates, empty for UTC.</summary>
    [JsonPropertyName("tz")] public string TimeZone { get; init; } = "";

    // Filters

    /// <summary>Registers the money filters.</summary>
    [JsonPropertyName("money")] public bool MoneyFilters { get; init; }

    /// <summary>Registers the color filters.</summary>
    [JsonPropertyName("color")] public bool ColorFilters { get; init; }

    // Limits

    /// <summary><see cref="TemplateOptions.MaxSteps"/>.</summary>
    [JsonPropertyName("steps")] public int MaxSteps { get; init; } = DefaultMaxSteps;

    /// <summary><see cref="TemplateOptions.MaxRecursion"/>.</summary>
    [JsonPropertyName("rec")] public int MaxRecursion { get; init; } = 100;

    /// <summary>The parser options.</summary>
    public FluidParserOptions ToParserOptions() => new()
    {
        AllowFunctions = AllowFunctions,
        AllowParentheses = AllowParentheses,
        AllowLiquidTag = AllowLiquidTag,
        AllowStringEscapeSequences = AllowStringEscapeSequences,
        AllowTrailingQuestionMark = AllowTrailingQuestionMark,
    };

    /// <summary>The parser options that change how a template is parsed, as a cache key.</summary>
    internal (bool, bool, bool, bool, bool) ParserKey =>
        (AllowFunctions, AllowParentheses, AllowLiquidTag, AllowStringEscapeSequences, AllowTrailingQuestionMark);
}
