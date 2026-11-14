namespace Fluid.Playground;

/// <summary>A format the output can be colored in: its identifier in the format picker, its name and its language.</summary>
/// <param name="Id">The value of the format in the picker.</param>
/// <param name="Name">The name shown to users.</param>
/// <param name="Language">The language of the grammar.</param>
/// <param name="Detected">Whether <see cref="PlaygroundOutputFormat.Detect"/> can find it.</param>
public sealed record PlaygroundFormat(string Id, string Name, PlaygroundLanguage Language, bool Detected);

/// <summary>The formats of the output picker.</summary>
public static class PlaygroundFormats
{
    /// <summary>The formats, the detected ones first.</summary>
    public static IReadOnlyList<PlaygroundFormat> All { get; } =
    [
        new("html", "HTML", PlaygroundLanguage.Html, true),
        new("json", "JSON", PlaygroundLanguage.Json, true),
        new("xml", "XML", PlaygroundLanguage.Xml, true),
        new("markdown", "Markdown", PlaygroundLanguage.Markdown, true),
        new("yaml", "YAML", PlaygroundLanguage.Yaml, true),
        new("css", "CSS", PlaygroundLanguage.Css, true),
        new("csharp", "C#", PlaygroundLanguage.CSharp, true),
        new("cpp", "C++", PlaygroundLanguage.Cpp, false),
        new("go", "Go", PlaygroundLanguage.Go, false),
        new("java", "Java", PlaygroundLanguage.Java, false),
        new("javascript", "JavaScript", PlaygroundLanguage.JavaScript, false),
        new("php", "PHP", PlaygroundLanguage.Php, false),
        new("powershell", "PowerShell", PlaygroundLanguage.PowerShell, false),
        new("python", "Python", PlaygroundLanguage.Python, false),
        new("ruby", "Ruby", PlaygroundLanguage.Ruby, false),
        new("rust", "Rust", PlaygroundLanguage.Rust, false),
        new("shell", "Shell", PlaygroundLanguage.Shell, false),
        new("sql", "SQL", PlaygroundLanguage.Sql, false),
        new("swift", "Swift", PlaygroundLanguage.Swift, false),
        new("typescript", "TypeScript", PlaygroundLanguage.TypeScript, false),
    ];

    /// <summary>The format whose identifier is <paramref name="id"/>.</summary>
    public static PlaygroundFormat? Find(string? id) => All.FirstOrDefault(f => f.Id == id);

    /// <summary>The name of <paramref name="language"/>, or "plain text".</summary>
    public static string NameOf(PlaygroundLanguage? language) => All.FirstOrDefault(f => f.Language == language)?.Name ?? "plain text";
}
