using Tint;
using Tint.Grammars;
using Tint.Themes;

namespace Fluid.Playground;

/// <summary>The languages of the playground editors.</summary>
public enum PlaygroundLanguage
{
    /// <summary>Liquid templates, with the HTML, CSS and JavaScript around the tags.</summary>
    Liquid,

    /// <summary>JSON: the model, or an output detected as JSON.</summary>
    Json,

    /// <summary>An output detected as HTML.</summary>
    Html,

    /// <summary>An output detected as XML.</summary>
    Xml,
}

/// <summary>
/// Colors the editors and the output with Tint, TextMate grammars compiled to .NET: Shopify's Liquid grammar and the VS Code
/// JSON, HTML and XML grammars, with the Light+ colors inline and the Dark+ ones as CSS variables (<c>--tint-dark</c>) that the
/// stylesheet applies in the dark theme. Not thread-safe, like the Tint highlighters it owns.
/// </summary>
public sealed class PlaygroundHighlighter
{
    private readonly GrammarRegistry _registry = new();
    private readonly Theme _light = Theme.FromSource(TintThemes.LightPlus);
    private readonly Theme _dark = Theme.FromSource(TintThemes.DarkPlus);
    private readonly Dictionary<PlaygroundLanguage, Highlighter> _highlighters = [];

    /// <summary>Creates the highlighter. Grammars are compiled when they are first used.</summary>
    public PlaygroundHighlighter()
    {
        // The Liquid grammar includes HTML, which includes CSS and JavaScript, and JSON for {% schema %} blocks. Only the
        // grammars referenced here are kept by the trimmer.
        _registry.Add(LiquidGrammar.Instance, TintGrammars.HtmlBasic, TintGrammars.Css, TintGrammars.JavaScript, TintGrammars.Json, TintGrammars.Xml);
    }

    /// <summary>The highlighted HTML of <paramref name="code"/>: a <c>pre</c> with one <c>span.line</c> per line.</summary>
    public string ToHtml(string code, PlaygroundLanguage language)
    {
        if (!_highlighters.TryGetValue(language, out var highlighter))
        {
            var scope = language switch
            {
                PlaygroundLanguage.Json => TintGrammars.Json.ScopeName,
                PlaygroundLanguage.Html => TintGrammars.HtmlBasic.ScopeName,
                PlaygroundLanguage.Xml => TintGrammars.Xml.ScopeName,
                _ => LiquidGrammar.Instance.ScopeName,
            };

            _highlighters[language] = highlighter = _registry.CreateHighlighter(scope, light: _light, dark: _dark);
        }

        return highlighter.ToHtml(code);
    }
}

/// <summary>Shopify's Liquid grammar (Grammars/liquid.tmLanguage.json, MIT).</summary>
[TextMateGrammar("Grammars/liquid.tmLanguage.json", LanguageId = "liquid", FileExtensions = [".liquid"])]
internal sealed partial class LiquidGrammar;
