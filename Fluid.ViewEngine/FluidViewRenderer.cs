using Fluid.Ast;
using Fluid.Parser;
using Fluid.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Fluid.ViewEngine;

/// <summary>
/// This class is registered as a singleton.
/// </summary>
public class FluidViewRenderer : IFluidViewRenderer
{
    private static readonly char[] PathSeparators = { '/', '\\' };

    private sealed class CacheEntry
    {
        public ConcurrentDictionary<string, AsyncTemplateCacheEntry> TemplateCache = new();
    }

    private sealed record AsyncTemplateCacheEntry(
        IFluidTemplate Template,
        IReadOnlyList<TemplateSourceVersion> Sources,
        // Whether the template, without its view starts, contains a layout tag.
        bool HasLayoutTag);

    private static readonly AsyncTemplateCacheEntry EmptyTemplateEntry = new(new FluidTemplate(), [], HasLayoutTag: false);

    private readonly record struct TemplateSourceVersion(
        string Path,
        string CacheKey,
        DateTimeOffset LastModified);

    private readonly record struct ResolvedTemplateSource(string Path, TemplateSourceInfo Source);

    private readonly ConcurrentDictionary<ITemplateFileProvider, CacheEntry> _cache = new();
    private readonly ITemplateFileProvider _viewsFileProvider;
    private readonly ITemplateFileProvider _partialsFileProvider;

    public FluidViewRenderer(FluidViewEngineOptions fluidViewEngineOptions)
    {
        _fluidViewEngineOptions = fluidViewEngineOptions;

        _viewsFileProvider =
            _fluidViewEngineOptions.ViewsFileProvider ??
            _fluidViewEngineOptions.TemplateOptions.FileProvider;
        _partialsFileProvider =
            _fluidViewEngineOptions.PartialsFileProvider ??
            _fluidViewEngineOptions.ViewsFileProvider ??
            _fluidViewEngineOptions.TemplateOptions.FileProvider;
    }

    private readonly FluidViewEngineOptions _fluidViewEngineOptions;

    public virtual async Task RenderViewAsync(TextWriter writer, string relativePath, TemplateContext context)
    {
        if (writer == null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        var bufferSize = context?.Options?.OutputBufferSize ?? 16 * 1024;
        if (bufferSize <= 0)
        {
            bufferSize = 16 * 1024;
        }

        await using var output = new TextWriterFluidOutput(
            writer,
            bufferSize,
            leaveOpen: true,
            allowSynchronousIO: false,
            cancellationToken: context.CancellationToken);
        await RenderViewAsync(output, relativePath, context);
        await output.FlushAsync();
    }

    public virtual async Task RenderViewAsync(IFluidOutput output, string relativePath, TemplateContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        // Provide some services to all statements
        context.AmbientValues[Constants.ViewPathIndex] = relativePath;
        context.AmbientValues[Constants.SectionsIndex] = null; // it is lazily initialized when first used
        context.AmbientValues[Constants.RendererIndex] = this;

        var template = (await GetTemplateEntryAsync(relativePath, _viewsFileProvider, true, context)).Template;

        if (_fluidViewEngineOptions.RenderingViewAsync != null)
        {
            await _fluidViewEngineOptions.RenderingViewAsync.Invoke(relativePath, context);
        }

        // The body is rendered and buffered before the Layout since it can contain fragments 
        // that need to be rendered as part of the Layout.
        // Also the body or its _ViewStarts might contain a Layout tag.
        // The context is not isolated such that variables can be changed by views

        var body = await template.RenderAsync(context, _fluidViewEngineOptions.TextEncoder, isolateContext: false);

        // Apply the layout specified while rendering the view, then the layout each layout specifies, if any.
        var currentPath = relativePath;
        List<string> bufferedLayouts = null;

        while (context.AmbientValues.TryGetValue(Constants.LayoutIndex, out var layoutPath) && layoutPath is string layoutPathString && !String.IsNullOrEmpty(layoutPathString))
        {
            // A nested layout is resolved from the location of the layout that declares it.
            layoutPathString = await ResolveLayoutPathAsync(
                currentPath,
                layoutPathString,
                _viewsFileProvider,
                context);

            if (bufferedLayouts != null && bufferedLayouts.Contains(layoutPathString))
            {
                throw new InvalidOperationException($"Circular layout reference: the layout '{layoutPathString}' is applied more than once.");
            }

            context.AmbientValues[Constants.ViewPathIndex] = layoutPathString;
            context.AmbientValues[Constants.BodyIndex] = body;

            // Cleared so that the layout can specify its own layout.
            context.AmbientValues[Constants.LayoutIndex] = null;

            // Parse the Layout file but ignore viewstarts
            var layout = await GetTemplateEntryAsync(
                layoutPathString,
                _viewsFileProvider,
                includeViewStarts: false,
                context);

            if (!layout.HasLayoutTag)
            {
                // The outermost layout, which is the only one in most cases, is rendered without buffering.
                await layout.Template.RenderAsync(output, _fluidViewEngineOptions.TextEncoder, context);
                return;
            }

            // The layout might apply another layout, in which case its output is the body of the next one.
            body = await layout.Template.RenderAsync(context, _fluidViewEngineOptions.TextEncoder, isolateContext: false);

            (bufferedLayouts ??= new List<string>()).Add(layoutPathString);
            currentPath = layoutPathString;
        }

        output.Write(body);
    }

    public virtual async Task RenderPartialAsync(TextWriter writer, string relativePath, TemplateContext context)
    {
        if (writer == null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        var bufferSize = context?.Options?.OutputBufferSize ?? 16 * 1024;
        if (bufferSize <= 0)
        {
            bufferSize = 16 * 1024;
        }

        await using var output = new TextWriterFluidOutput(
            writer,
            bufferSize,
            leaveOpen: true,
            allowSynchronousIO: false,
            cancellationToken: context.CancellationToken);
        await RenderPartialAsync(output, relativePath, context);
        await output.FlushAsync();
    }

    public virtual async Task RenderPartialAsync(IFluidOutput output, string relativePath, TemplateContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        // Substitute View Path
        context.AmbientValues[Constants.ViewPathIndex] = relativePath;

        if (_fluidViewEngineOptions.RenderingViewAsync != null)
        {
            await _fluidViewEngineOptions.RenderingViewAsync.Invoke(relativePath, context);
        }

        var template = (await GetTemplateEntryAsync(
            relativePath,
            _partialsFileProvider,
            includeViewStarts: false,
            context)).Template;

        await template.RenderAsync(output, _fluidViewEngineOptions.TextEncoder, context);
    }

    private async ValueTask<string> ResolveLayoutPathAsync(
        string viewPath,
        string layoutPath,
        ITemplateFileProvider fileProvider,
        TemplateContext context)
    {
        if (layoutPath.EndsWith(Constants.ViewExtension))
        {
            return Path.Combine(Path.GetDirectoryName(viewPath), layoutPath);
        }

        var currentViewPath = viewPath;
        var index = currentViewPath.Length - 1;

        while (!String.IsNullOrEmpty(currentViewPath))
        {
            if (index == -1)
            {
                return layoutPath;
            }

            index = currentViewPath.LastIndexOfAny(PathSeparators, index);
            currentViewPath = currentViewPath.Substring(0, index + 1);

            var candidate = Path.Combine(currentViewPath, layoutPath) + Constants.ViewExtension;
            if (await fileProvider.GetFileInfoAsync(candidate, context, context.CancellationToken) != null)
            {
                return candidate;
            }

            index--;
        }

        foreach (var location in _fluidViewEngineOptions.LayoutsLocationFormats)
        {
            var candidate = String.Format(location, Path.GetFileName(layoutPath));
            if (await fileProvider.GetFileInfoAsync(candidate, context, context.CancellationToken) != null)
            {
                return candidate;
            }
        }

        return layoutPath;
    }

    private async ValueTask<AsyncTemplateCacheEntry> GetTemplateEntryAsync(
        string path,
        ITemplateFileProvider fileProvider,
        bool includeViewStarts,
        TemplateContext context)
    {
        var source = await fileProvider.GetFileInfoAsync(path, context, context.CancellationToken);
        if (source == null)
        {
            return EmptyTemplateEntry;
        }

        var sources = new List<ResolvedTemplateSource>();
        if (includeViewStarts)
        {
            sources.AddRange(await FindViewStartSourcesAsync(path, fileProvider, context));
        }

        sources.Add(new ResolvedTemplateSource(path, source));

        var cache = _cache.GetOrAdd(fileProvider, static _ => new CacheEntry());
        var cacheKey = source.CacheKey ?? path;
        if (cache.TemplateCache.TryGetValue(cacheKey, out var cachedTemplate) &&
            SourcesMatch(
                cachedTemplate.Sources,
                sources,
                _fluidViewEngineOptions.TrackFileChanges))
        {
            return cachedTemplate;
        }

        var subTemplates = new List<IFluidTemplate>(sources.Count * 2);

        for (var i = 0; i < sources.Count - 1; i++)
        {
            var viewStartPath = sources[i].Path;
            subTemplates.Add(new FluidTemplate(new CallbackStatement((writer, encoder, templateContext) =>
            {
                templateContext.AmbientValues[Constants.ViewPathIndex] = viewStartPath;
                return Statement.NormalCompletion;
            })));

            subTemplates.Add((await GetTemplateEntryAsync(viewStartPath, fileProvider, includeViewStarts: false, context)).Template);
        }

        var parsedTemplate = await ParseTemplateSourceAsync(source, context.CancellationToken);
        subTemplates.Add(parsedTemplate);

        IFluidTemplate template = new CompositeFluidTemplate(subTemplates);

        if (_fluidViewEngineOptions.TemplateOptions.TemplateParsed != null)
        {
            template = _fluidViewEngineOptions.TemplateOptions.TemplateParsed(path, template);
        }

        var versions = new TemplateSourceVersion[sources.Count];
        for (var i = 0; i < sources.Count; i++)
        {
            versions[i] = new TemplateSourceVersion(
                sources[i].Path,
                sources[i].Source.CacheKey,
                sources[i].Source.LastModified);
        }

        var entry = new AsyncTemplateCacheEntry(template, versions, LayoutTagFinder.HasLayoutTag(parsedTemplate));
        cache.TemplateCache[cacheKey] = entry;
        return entry;
    }

    private static bool SourcesMatch(
        IReadOnlyList<TemplateSourceVersion> cachedSources,
        IReadOnlyList<ResolvedTemplateSource> sources,
        bool compareLastModified)
    {
        if (cachedSources.Count != sources.Count)
        {
            return false;
        }

        for (var i = 0; i < cachedSources.Count; i++)
        {
            if (!string.Equals(cachedSources[i].Path, sources[i].Path, StringComparison.Ordinal) ||
                !string.Equals(cachedSources[i].CacheKey, sources[i].Source.CacheKey, StringComparison.Ordinal) ||
                (compareLastModified &&
                 cachedSources[i].LastModified < sources[i].Source.LastModified))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<List<ResolvedTemplateSource>> FindViewStartSourcesAsync(
        string viewPath,
        ITemplateFileProvider fileProvider,
        TemplateContext context)
    {
        var viewStarts = new List<ResolvedTemplateSource>();
        var index = viewPath.Length - 1;

        while (!String.IsNullOrEmpty(viewPath))
        {
            if (index == -1)
            {
                break;
            }

            index = viewPath.LastIndexOfAny(PathSeparators, index);
            viewPath = viewPath.Substring(0, index + 1);

            var viewStartPath = viewPath + Constants.ViewStartFilename;
            var source = await fileProvider.GetFileInfoAsync(
                viewStartPath,
                context,
                context.CancellationToken);
            if (source != null)
            {
                viewStarts.Add(new ResolvedTemplateSource(viewStartPath, source));
            }

            index--;
        }

        viewStarts.Reverse();
        return viewStarts;
    }

    /// <summary>
    /// Finds whether a template contains a <c>layout</c> tag, in which case it is buffered when used as a layout.
    /// </summary>
    private sealed class LayoutTagFinder : AstVisitor
    {
        private bool _found;

        public static bool HasLayoutTag(IFluidTemplate template)
        {
            // A template that can't be inspected is assumed to contain one.
            if (template is not IStatementList)
            {
                return true;
            }

            var finder = new LayoutTagFinder();
            finder.VisitTemplate(template);
            return finder._found;
        }

        protected override Statement VisitParserTagStatement<T>(ParserTagStatement<T> parserTagStatement)
        {
            if (parserTagStatement.TagName == "layout")
            {
                _found = true;
            }

            return parserTagStatement;
        }
    }

    private async ValueTask<IFluidTemplate> ParseTemplateSourceAsync(
        TemplateSourceInfo source,
        CancellationToken cancellationToken)
    {
        var content = await source.ReadToEndAsync(cancellationToken);

        try
        {
            return _fluidViewEngineOptions.Parser.Parse(content);
        }
        catch (ParseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ParseException(exception.Message, content, null, exception);
        }
    }

}
