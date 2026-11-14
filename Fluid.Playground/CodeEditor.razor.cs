using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fluid.Playground;

/// <summary>An editor for Liquid or JSON, colored by <see cref="PlaygroundHighlighter"/> as the user types.</summary>
public sealed partial class CodeEditor
{
    private ElementReference _root;
    private string _value = "";
    private string _html = "";
    private PlaygroundLanguage? _highlightedLanguage;
    private string? _highlightedValue;

    [Inject]
    private PlaygroundHighlighter Highlighter { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>The code.</summary>
    [Parameter]
    public string Value { get; set; } = "";

    /// <summary>Called with the new code on every input.</summary>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    /// <summary>The language of the code.</summary>
    [Parameter]
    public PlaygroundLanguage Language { get; set; }

    /// <summary>The accessible name of the editor.</summary>
    [Parameter]
    public string? Label { get; set; }

    protected override void OnParametersSet()
    {
        _value = Value;
        Highlight();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await using var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/playground.js");
            await module.InvokeVoidAsync("attachEditor", _root);
        }
    }

    private Task OnInputAsync(ChangeEventArgs e)
    {
        _value = e.Value?.ToString() ?? "";
        Highlight();
        return ValueChanged.InvokeAsync(_value);
    }

    private void Highlight()
    {
        if (_value == _highlightedValue && Language == _highlightedLanguage)
        {
            return;
        }

        // A last empty line has no span in Tint's output: a space keeps the pre as tall as the textarea.
        _html = Highlighter.ToHtml(_value.EndsWith('\n') ? _value + " " : _value, Language);
        _highlightedValue = _value;
        _highlightedLanguage = Language;
    }
}
