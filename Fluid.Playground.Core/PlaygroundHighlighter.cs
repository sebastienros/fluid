using Tint;
using Tint.Grammars;
using Tint.Themes;

namespace Fluid.Playground;

/// <summary>The languages the playground colors: Liquid and JSON in the editors, and the formats of the output.</summary>
public enum PlaygroundLanguage
{
#pragma warning disable CS1591 // The names are the languages.
    Liquid, Json, Html, Xml, Markdown, CSharp, Yaml, Css,
    Cpp, Go, Java, JavaScript, Php, PowerShell, Python, Ruby, Rust, Shell, Sql, Swift, TypeScript,
#pragma warning restore CS1591
}

/// <summary>
/// Colors the editors and the output with Tint, TextMate grammars compiled to .NET: Shopify's Liquid grammar and the VS Code
/// grammars of the other <see cref="PlaygroundLanguage"/> values, with the Light+ colors inline and the Dark+ ones as CSS variables (<c>--tint-dark</c>) that the
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
        // Only the grammars referenced here are kept by the trimmer. Some include others: Liquid includes HTML, which
        // includes CSS and JavaScript; YAML needs its 1.2 grammar, PHP its source grammar and HTML, C++ its macros.
        // Embedded languages without a grammar (a Lua block in Markdown) are left uncolored.
        _registry.Add(
            LiquidGrammar.Instance, TintGrammars.HtmlBasic, TintGrammars.Html, TintGrammars.Css, TintGrammars.JavaScript,
            TintGrammars.Json, TintGrammars.Xml, TintGrammars.Markdown, TintGrammars.CSharp, TintGrammars.Yaml,
            TintGrammars.Yaml12, TintGrammars.YamlEmbedded, TintGrammars.Cpp, TintGrammars.CppMacro, TintGrammars.C,
            TintGrammars.Go, TintGrammars.Java, TintGrammars.Php, TintGrammars.PhpSource, TintGrammars.PowerShell,
            TintGrammars.Python, TintGrammars.Ruby, TintGrammars.Rust, TintGrammars.ShellScript, TintGrammars.Sql,
            TintGrammars.Swift, TintGrammars.TypeScript);
    }

    /// <summary>The highlighted HTML of <paramref name="code"/>: a <c>pre</c> with one <c>span.line</c> per line.</summary>
    public string ToHtml(string code, PlaygroundLanguage language)
    {
        if (!_highlighters.TryGetValue(language, out var highlighter))
        {
            _highlighters[language] = highlighter = _registry.CreateHighlighter(Grammar(language).ScopeName, light: _light, dark: _dark);
        }

        return highlighter.ToHtml(code);
    }

    /// <summary>The scopes of the grammars that <paramref name="language"/> includes and that are not registered.</summary>
    public IReadOnlyCollection<string> GetMissingGrammars(PlaygroundLanguage language) => _registry.GetMissingGrammars(Grammar(language).ScopeName);

    private static IGrammarSource Grammar(PlaygroundLanguage language) => language switch
    {
        PlaygroundLanguage.Liquid => LiquidGrammar.Instance,
        PlaygroundLanguage.Json => TintGrammars.Json,
        PlaygroundLanguage.Html => TintGrammars.HtmlBasic,
        PlaygroundLanguage.Xml => TintGrammars.Xml,
        PlaygroundLanguage.Markdown => TintGrammars.Markdown,
        PlaygroundLanguage.CSharp => TintGrammars.CSharp,
        PlaygroundLanguage.Yaml => TintGrammars.Yaml,
        PlaygroundLanguage.Css => TintGrammars.Css,
        PlaygroundLanguage.Cpp => TintGrammars.Cpp,
        PlaygroundLanguage.Go => TintGrammars.Go,
        PlaygroundLanguage.Java => TintGrammars.Java,
        PlaygroundLanguage.JavaScript => TintGrammars.JavaScript,
        PlaygroundLanguage.Php => TintGrammars.Php,
        PlaygroundLanguage.PowerShell => TintGrammars.PowerShell,
        PlaygroundLanguage.Python => TintGrammars.Python,
        PlaygroundLanguage.Ruby => TintGrammars.Ruby,
        PlaygroundLanguage.Rust => TintGrammars.Rust,
        PlaygroundLanguage.Shell => TintGrammars.ShellScript,
        PlaygroundLanguage.Sql => TintGrammars.Sql,
        PlaygroundLanguage.Swift => TintGrammars.Swift,
        PlaygroundLanguage.TypeScript => TintGrammars.TypeScript,
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };
}

/// <summary>Shopify's Liquid grammar (Grammars/liquid.tmLanguage.json, MIT).</summary>
[TextMateGrammar("Grammars/liquid.tmLanguage.json", LanguageId = "liquid", FileExtensions = [".liquid"])]
internal sealed partial class LiquidGrammar;
