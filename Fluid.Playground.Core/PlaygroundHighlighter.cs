using Tint;
using Tint.Grammars;
using Tint.Themes;

namespace Fluid.Playground;

/// <summary>The languages of the playground editors.</summary>
public enum PlaygroundLanguage
{
    /// <summary>Liquid templates, with the HTML, CSS and JavaScript around the tags.</summary>
    Liquid,

    /// <summary>The JSON model.</summary>
    Json,
}

/// <summary>
/// Colors the code of the editors with Tint, TextMate grammars compiled to .NET: Shopify's Liquid grammar and the VS Code
/// JSON grammar, with the Light+ colors inline and the Dark+ ones as CSS variables (<c>--tint-dark</c>) that the
/// stylesheet applies in the dark theme. Not thread-safe, like the Tint highlighters it owns.
/// </summary>
public sealed class PlaygroundHighlighter
{
    private readonly Highlighter _liquid;
    private readonly Highlighter _json;

    /// <summary>Creates the highlighters. Grammar patterns are parsed when they are first used.</summary>
    public PlaygroundHighlighter()
    {
        var registry = new GrammarRegistry();

        // The Liquid grammar includes HTML, which includes CSS and JavaScript, and JSON for {% schema %} blocks. Only the
        // grammars referenced here are kept by the trimmer.
        registry.Add(LiquidGrammar.Instance, TintGrammars.HtmlBasic, TintGrammars.Css, TintGrammars.JavaScript, TintGrammars.Json);

        var light = Theme.FromSource(TintThemes.LightPlus);
        var dark = Theme.FromSource(TintThemes.DarkPlus);
        _liquid = registry.CreateHighlighter(LiquidGrammar.Instance.ScopeName, light: light, dark: dark);
        _json = registry.CreateHighlighter(TintGrammars.Json.ScopeName, light: light, dark: dark);
    }

    /// <summary>The highlighted HTML of <paramref name="code"/>: a <c>pre</c> with one <c>span.line</c> per line.</summary>
    public string ToHtml(string code, PlaygroundLanguage language) =>
        (language == PlaygroundLanguage.Json ? _json : _liquid).ToHtml(code);
}

/// <summary>Shopify's Liquid grammar (Grammars/liquid.tmLanguage.json, MIT).</summary>
[TextMateGrammar("Grammars/liquid.tmLanguage.json", LanguageId = "liquid", FileExtensions = [".liquid"])]
internal sealed partial class LiquidGrammar;
