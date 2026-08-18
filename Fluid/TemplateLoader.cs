namespace Fluid;

internal static class TemplateLoader
{
    internal readonly record struct LoadedTemplate(string Path, IFluidTemplate Template);

    internal sealed class LoadCapture
    {
        public string Path { get; private set; }
        public string CacheKey { get; private set; }
        public DateTimeOffset LastModified { get; private set; }

        public void Set(string path, string cacheKey, DateTimeOffset lastModified)
        {
            Path = path;
            CacheKey = cacheKey;
            LastModified = lastModified;
        }
    }

    public static async ValueTask<LoadedTemplate> LoadAsync(
        FluidParser parser,
        string path,
        TemplateContext context,
        string defaultFileExtension,
        LoadCapture capture = null)
    {
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

            if (!parser.TryParse(content, out template, out var errors))
            {
                throw new ParseException(errors);
            }

            if (context.Options.TemplateParsed != null)
            {
                template = context.Options.TemplateParsed(resolvedPath, template);
            }

            context.Options.TemplateCache?.SetTemplate(cacheKey, source.LastModified, template);
        }

        capture?.Set(resolvedPath, cacheKey, source.LastModified);
        return new LoadedTemplate(resolvedPath, template);
    }

    private static ValueTask<TemplateSourceInfo> GetSourceAsync(string path, TemplateContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        return context.Options.FileProvider.GetFileInfoAsync(path, context, context.CancellationToken);
    }
}
