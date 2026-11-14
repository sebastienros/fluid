using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fluid.Playground;

/// <summary>
/// The playground page: editors for the template, its partials and the model, the output, and the options. Every change
/// renders again after a short pause, and the state is written in the URL fragment so that a link shows the same result.
/// </summary>
public sealed partial class App : IAsyncDisposable
{
    private const int TemplateTab = -1;
    private const int ModelTab = -2;
    private const int RenderDelayMs = 120;
    private const int HashDelayMs = 400;

    private static readonly string FluidVersion =
        typeof(FluidParser).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "";

    private string _template = "";
    private string _model = "{}";
    private List<PlaygroundPartial> _partials = [];
    private PlaygroundOptions _options = new();
    private string _sampleId = "";
    private int _tab = TemplateTab;
    private bool _preview;
    private bool _showOptions;
    private int _changedOptions;
    private PlaygroundResult? _result;
    private double? _readyMs;
    private string _shareLabel = "Share";
    private string _copyLabel = "Copy";
    private string _outputFormat = "auto";
    private PlaygroundLanguage? _outputLanguage;
    private string? _outputHtml;

    private IJSObjectReference? _module;
    private CancellationTokenSource? _pending;
    private CancellationTokenSource? _pendingHash;

    [Inject]
    private PlaygroundEngine Engine { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private PlaygroundHighlighter Highlighter { get; set; } = null!;

    private PlaygroundState State => new()
    {
        Template = _template,
        Model = _model,
        Partials = _partials.ToArray(),
        Options = _options,
    };

    // The preview renders the output in a sandboxed frame, with the colors of a blank page.
    private string PreviewDocument =>
        "<!doctype html><meta charset=\"utf-8\"><style>body{font:15px/1.5 system-ui,sans-serif;margin:16px;color:#111;background:#fff}</style>" + (_result?.Output ?? "");

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/playground.js");
        var state = PlaygroundState.FromHash(await _module.InvokeAsync<string>("readHash"));
        if (state is null)
        {
            Load(PlaygroundSamples.Default.State);
            _sampleId = PlaygroundSamples.Default.Id;
        }
        else
        {
            Load(state);
            _sampleId = PlaygroundSamples.All.FirstOrDefault(s => s.State.ToHash() == state.ToHash())?.Id ?? "";
        }

        _showOptions = _changedOptions > 0 && await _module.InvokeAsync<bool>("isWide");
        await RenderNowAsync();
        _readyMs = await _module.InvokeAsync<double>("markReady");
        StateHasChanged();
    }

    private void Load(PlaygroundState state)
    {
        _template = state.Template;
        _model = state.Model;
        _partials = [.. state.Partials];
        _options = state.Options;
        _changedOptions = CountChanged(_options);
        _tab = TemplateTab;
    }

    private async Task RenderNowAsync()
    {
        _result = await Engine.RenderAsync(State);
        HighlightOutput();
    }

    // Colors the output in the selected format, or in the detected one with "auto". Plain text and long outputs aren't.
    private void HighlightOutput()
    {
        var output = _result?.Error is null ? _result?.Output ?? "" : "";
        _outputLanguage = _outputFormat switch
        {
            "html" => PlaygroundLanguage.Html,
            "json" => PlaygroundLanguage.Json,
            "xml" => PlaygroundLanguage.Xml,
            "text" => null,
            _ => PlaygroundOutputFormat.Detect(output),
        };

        _outputHtml = _outputLanguage is { } language && output.Length > 0 && output.Length <= PlaygroundOutputFormat.MaxLength
            ? Highlighter.ToHtml(output, language)
            : null;
    }

    private void OnOutputFormatChanged(ChangeEventArgs e)
    {
        _outputFormat = e.Value?.ToString() ?? "auto";
        HighlightOutput();
    }

    private static string FormatName(PlaygroundLanguage? language) => language switch
    {
        PlaygroundLanguage.Html => "HTML",
        PlaygroundLanguage.Json => "JSON",
        PlaygroundLanguage.Xml => "XML",
        _ => "plain text",
    };

    // Renders again after a pause, so that typing doesn't render on every key.
    private async Task ScheduleRenderAsync(bool custom = true)
    {
        if (custom)
        {
            _sampleId = "";
        }

        _pending?.Cancel();
        var pending = _pending = new CancellationTokenSource();
        try
        {
            await Task.Delay(RenderDelayMs, pending.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await RenderNowAsync();
        StateHasChanged();
        await ScheduleHashAsync();
    }

    private async Task ScheduleHashAsync()
    {
        _pendingHash?.Cancel();
        var pending = _pendingHash = new CancellationTokenSource();
        try
        {
            await Task.Delay(HashDelayMs, pending.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (_module is not null)
        {
            await _module.InvokeVoidAsync("writeHash", State.ToHash());
        }
    }

    private Task OnTemplateChanged(string value)
    {
        _template = value;
        return ScheduleRenderAsync();
    }

    private Task OnModelChanged(string value)
    {
        _model = value;
        return ScheduleRenderAsync();
    }

    private Task OnPartialChanged(int index, string value)
    {
        _partials[index] = _partials[index] with { Source = value };
        return ScheduleRenderAsync();
    }

    private async Task OnSampleChanged(ChangeEventArgs e)
    {
        var sample = PlaygroundSamples.All.FirstOrDefault(s => s.Id == e.Value?.ToString());
        if (sample is null)
        {
            return;
        }

        Load(sample.State);
        _sampleId = sample.Id;
        await RenderNowAsync();
        await ScheduleHashAsync();
    }

    private Task AddPartial()
    {
        var name = "partial";
        for (var i = 2; _partials.Any(p => p.Name == name); i++)
        {
            name = "partial" + i;
        }

        _partials.Add(new PlaygroundPartial(name, "Hello from " + name));
        _tab = _partials.Count - 1;
        return ScheduleRenderAsync();
    }

    private Task RenamePartial(int index, string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || _partials.Where((_, i) => i != index).Any(p => p.Name == name))
        {
            return Task.CompletedTask;
        }

        _partials[index] = _partials[index] with { Name = name };
        return ScheduleRenderAsync();
    }

    private Task RemovePartial(int index)
    {
        _partials.RemoveAt(index);
        _tab = TemplateTab;
        return ScheduleRenderAsync();
    }

    private Task Set(PlaygroundOptions options)
    {
        _options = options;
        _changedOptions = CountChanged(options);
        return ScheduleRenderAsync();
    }

    private Task ResetOptions() => Set(new PlaygroundOptions());

    private bool HasTrimming(TrimmingFlags flag) => (_options.Trimming & flag) != 0;

    private Task SetTrimming(TrimmingFlags flag, ChangeEventArgs e) =>
        Set(_options with { Trimming = IsChecked(e) ? _options.Trimming | flag : _options.Trimming & ~flag });

    private async Task ShareAsync()
    {
        if (_module is null)
        {
            return;
        }

        var hash = State.ToHash();
        await _module.InvokeVoidAsync("writeHash", hash);
        _shareLabel = await _module.InvokeAsync<bool>("copyText", await _module.InvokeAsync<string>("currentUrl")) ? "Link copied" : "Copy failed";
        StateHasChanged();
        await Task.Delay(1600);
        _shareLabel = "Share";
    }

    private async Task CopyOutputAsync()
    {
        if (_module is null || _result is null)
        {
            return;
        }

        _copyLabel = await _module.InvokeAsync<bool>("copyText", _result.Output) ? "Copied" : "Copy failed";
        StateHasChanged();
        await Task.Delay(1600);
        _copyLabel = "Copy";
    }

    private async Task ToggleThemeAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("toggleTheme");
        }
    }

    private static int CountChanged(PlaygroundOptions options)
    {
        var defaults = new PlaygroundOptions();
        var count = 0;
        foreach (var property in typeof(PlaygroundOptions).GetProperties())
        {
            if (!Equals(property.GetValue(options), property.GetValue(defaults)))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsChecked(ChangeEventArgs e) => e.Value is true || (e.Value is string s && bool.TryParse(s, out var b) && b);

    private static int ToInt(ChangeEventArgs e, int fallback) =>
        int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : fallback;

    private static string Ms(TimeSpan time) => time.TotalMilliseconds.ToString(time.TotalMilliseconds < 10 ? "0.00" : "0", CultureInfo.InvariantCulture) + " ms";

    private static string ErrorTitle(PlaygroundError error) => error.Kind switch
    {
        PlaygroundErrorKind.Model => "Invalid model",
        PlaygroundErrorKind.Parse => "Syntax error",
        _ => "Rendering error",
    };

    public async ValueTask DisposeAsync()
    {
        _pending?.Cancel();
        _pendingHash?.Cancel();
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
