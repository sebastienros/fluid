namespace Fluid;

internal static class TemplateLoader
{
    internal readonly record struct LoadedTemplate(string Path, IFluidTemplate Template);

    internal readonly struct LoadedTemplateKey : IEquatable<LoadedTemplateKey>
    {
        public LoadedTemplateKey(FluidParser parser, string path)
        {
            Parser = parser;
            Path = path;
        }

        public FluidParser Parser { get; }
        public string Path { get; }

        public bool Equals(LoadedTemplateKey other) =>
            ReferenceEquals(Parser, other.Parser) && string.Equals(Path, other.Path, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LoadedTemplateKey other && Equals(other);

        public override int GetHashCode() => Path.GetHashCode();
    }

    public static ValueTask<LoadedTemplate> LoadAsync(
        FluidParser parser,
        string path,
        TemplateContext context,
        string defaultFileExtension)
    {
        var key = new LoadedTemplateKey(parser, path);

        if (context.TryGetLoadedTemplate(key, out var loadedTemplate))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<LoadedTemplate>(loadedTemplate);
        }

        return LoadCoreAsync(key, context, defaultFileExtension);
    }

    private static async ValueTask<LoadedTemplate> LoadCoreAsync(
        LoadedTemplateKey key,
        TemplateContext context,
        string defaultFileExtension)
    {
        var parser = key.Parser;
        var path = key.Path;
        var resolvedPath = path;
        var source = await GetSourceAsync(resolvedPath, context);

        if (source == null &&
            !string.IsNullOrEmpty(defaultFileExtension) &&
            !resolvedPath.EndsWith(defaultFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            resolvedPath += defaultFileExtension;
            source = await GetSourceAsync(resolvedPath, context);
        }

        if (source == null)
        {
            throw new FileNotFoundException(path);
        }

        var cacheKey = source.CacheKey ?? resolvedPath;

        if (context.Options.TemplateCache == null ||
            !context.Options.TemplateCache.TryGetTemplate(cacheKey, source.LastModified, out var template))
        {
            var content = await source.ReadToEndAsync(context.CancellationToken);

            try
            {
                template = parser.Parse(content);
            }
            catch (Exception exception)
            {
                var parseException = exception as ParseException;
                throw new ParseException(
                    $"Failed to parse template '{GetDisplayPath(resolvedPath)}'.\n{exception.Message}",
                    parseException?.TemplateSource ?? content,
                    parseException?.Position,
                    exception);
            }

            if (context.Options.TemplateParsed != null)
            {
                template = context.Options.TemplateParsed(resolvedPath, template);
            }

            context.Options.TemplateCache?.SetTemplate(cacheKey, source.LastModified, template);
        }

        var loadedTemplate = new LoadedTemplate(resolvedPath, template);
        context.SetLoadedTemplate(key, loadedTemplate);
        return loadedTemplate;
    }

    private static string GetDisplayPath(string path)
    {
        var isRooted =
            path.Length > 0 &&
            (path[0] == '/' ||
             path[0] == '\\' ||
             (path.Length > 1 && path[1] == ':' && char.IsLetter(path[0])));

        if (!isRooted)
        {
            return path;
        }

        var separator = path.LastIndexOfAny(['/', '\\']);
        return separator < 0 ? path : path.Substring(separator + 1);
    }

    private static ValueTask<TemplateSourceInfo> GetSourceAsync(string path, TemplateContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        return context.Options.FileProvider.GetFileInfoAsync(path, context, context.CancellationToken);
    }
}
