using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid.Values;

namespace Fluid.Playground;

/// <summary>Where a rendering failed.</summary>
public enum PlaygroundErrorKind
{
    /// <summary>The model is not valid JSON.</summary>
    Model,

    /// <summary>The template is not valid Liquid.</summary>
    Parse,

    /// <summary>The template failed while it was rendered.</summary>
    Render,
}

/// <summary>An error, with its position in the template or the model when it is known (one-based).</summary>
public sealed record PlaygroundError(PlaygroundErrorKind Kind, string Message, int? Line = null, int? Column = null);

/// <summary>The result of a rendering.</summary>
public sealed record PlaygroundResult(string Output, PlaygroundError? Error, TimeSpan ParseTime, TimeSpan RenderTime);

/// <summary>Parses and renders the state of the playground.</summary>
public sealed class PlaygroundEngine
{
    // Encodes the characters HTML needs, and keeps other text readable, like the view engines do.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    // A parser per combination of parser options: creating one is expensive, and they are thread-safe.
    private readonly ConcurrentDictionary<(bool, bool, bool, bool, bool), FluidParser> _parsers = new();

    /// <summary>The culture names offered by the playground, the invariant culture first.</summary>
    public static IReadOnlyList<string> Cultures { get; } = ["", "en-US", "en-GB", "fr-FR", "de-DE", "es-ES", "ja-JP", "zh-CN", "ar-SA"];

    /// <summary>The time zones offered by the playground, UTC first.</summary>
    public static IReadOnlyList<string> TimeZones { get; } = ["", "America/Los_Angeles", "America/New_York", "Europe/London", "Europe/Paris", "Asia/Kolkata", "Asia/Tokyo", "Australia/Sydney"];

    /// <summary>Parses and renders <paramref name="state"/>.</summary>
    public async ValueTask<PlaygroundResult> RenderAsync(PlaygroundState state, CancellationToken cancellationToken = default)
    {
        var options = state.Options;

        JsonNode? model;
        try
        {
            model = string.IsNullOrWhiteSpace(state.Model) ? null : JsonNode.Parse(state.Model, documentOptions: JsonOptions);
        }
        catch (JsonException e)
        {
            var line = e.LineNumber is { } l ? (int)l + 1 : (int?)null;
            var column = e.BytePositionInLine is { } c ? (int)c + 1 : (int?)null;
            return new PlaygroundResult("", new PlaygroundError(PlaygroundErrorKind.Model, FirstSentence(e.Message), line, column), TimeSpan.Zero, TimeSpan.Zero);
        }

        var parser = _parsers.GetOrAdd(options.ParserKey, _ => new FluidParser(options.ToParserOptions()));

        var start = Stopwatch.GetTimestamp();
        IFluidTemplate template;
        try
        {
            template = parser.Parse(state.Template);
        }
        catch (ParseException e)
        {
            return new PlaygroundResult("", new PlaygroundError(PlaygroundErrorKind.Parse, e.Message, e.Position?.Line, e.Position?.Column), Stopwatch.GetElapsedTime(start), TimeSpan.Zero);
        }

        var parseTime = Stopwatch.GetElapsedTime(start);

        TemplateContext context;
        try
        {
            var templateOptions = BuildOptions(options, state.Partials);
            context = model is JsonObject root ? new TemplateContext(root, templateOptions) : new TemplateContext(templateOptions);
            if (model is not null and not JsonObject)
            {
                context.SetValue("data", FluidValue.Create(model, templateOptions));
            }

            context.CancellationToken = cancellationToken;
        }
        catch (CultureNotFoundException)
        {
            return new PlaygroundResult("", new PlaygroundError(PlaygroundErrorKind.Render, $"The culture '{options.Culture}' is not available."), parseTime, TimeSpan.Zero);
        }

        start = Stopwatch.GetTimestamp();
        try
        {
            var output = await template.RenderAsync(context, options.HtmlEncode ? Html : NullEncoder.Default);
            return new PlaygroundResult(output, null, parseTime, Stopwatch.GetElapsedTime(start));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new PlaygroundResult("", new PlaygroundError(PlaygroundErrorKind.Render, e.Message), parseTime, Stopwatch.GetElapsedTime(start));
        }
    }

    private static TemplateOptions BuildOptions(PlaygroundOptions options, IReadOnlyList<PlaygroundPartial> partials)
    {
        var builder = new TemplateOptionsBuilder()
            .WithStrictVariables(options.StrictVariables)
            .WithStrictFilters(options.StrictFilters)
            .WithTrimming(options.Trimming)
            .WithGreedy(options.Greedy)
            .WithMaxSteps(Math.Max(0, options.MaxSteps))
            .WithMaxRecursion(Math.Max(0, options.MaxRecursion))
            .WithMaxOutputSize(PlaygroundOptions.DefaultMaxOutputSize)
            .WithCultureInfo(options.Culture.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(options.Culture, predefinedOnly: true))
            .WithTimeZone(FindTimeZone(options.TimeZone));

        if (options.MoneyFilters)
        {
            builder.WithMoneyFilters();
        }

        if (options.ColorFilters)
        {
            builder.WithColorFilters();
        }

        if (partials.Count > 0)
        {
            // The include and render statements parse partials with the parser of the template: same syntax.
            builder.WithFileProvider(new DelegateTemplateFileProvider((path, _, _) => ValueTask.FromResult(FindPartial(partials, path)!)));
        }

        return builder.Build();
    }

    private static TemplateSourceInfo? FindPartial(IReadOnlyList<PlaygroundPartial> partials, string path)
    {
        foreach (var partial in partials)
        {
            if (string.Equals(partial.Name, path, StringComparison.Ordinal) || string.Equals(partial.Name + ".liquid", path, StringComparison.Ordinal))
            {
                var bytes = Encoding.UTF8.GetBytes(partial.Source);
                return new TemplateSourceInfo(DateTimeOffset.UnixEpoch, _ => ValueTask.FromResult<Stream>(new MemoryStream(bytes)), cacheKey: partial.Name + ":" + partial.Source.GetHashCode());
            }
        }

        return null;
    }

    private static TimeZoneInfo FindTimeZone(string id)
    {
        if (id.Length == 0)
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out var timeZone) ? timeZone : TimeZoneInfo.Utc;
    }

    private static string FirstSentence(string message)
    {
        // System.Text.Json messages end with the path and position, which the playground shows separately.
        var index = message.IndexOf(" Path:", StringComparison.Ordinal);
        return index > 0 ? message[..index] : message;
    }
}
