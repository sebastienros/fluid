using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Fluid.Values;

namespace Fluid.Tool;

public static class LiquidTool
{
    public const int Success = 0;
    public const int RenderError = 1;
    public const int UsageError = 2;

    private const string Description = """
        Renders Liquid templates using Fluid. JSON read from stdin (or --data) provides the model.

        Examples:
          echo '{"name":"World"}' | liquid -e "Hello {{ name }}!"
          liquid page.liquid --data model.json -o page.html
          cat model.json | liquid page.liquid -I ./partials
          liquid -e "{{ user }} on {{ env.HOME }}" --set user=Bob --env
          liquid page.liquid --validate

        Exit codes: 0 success, 1 template/data/render error, 2 usage error.
        """;

    public static Task<int> RunAsync(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var templateArgument = new Argument<string?>("template")
        {
            Description = "Path of the Liquid template file to render.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var templateOption = new Option<string?>("--template", "-t") { Description = "Path of the Liquid template file to render (same as the positional argument)." };
        var inlineOption = new Option<string?>("--inline", "-e") { Description = "Liquid template source provided inline." };
        var dataOption = new Option<string?>("--data", "-d") { Description = "JSON data file. Use '-' to read stdin. Defaults to stdin when it is redirected." };
        var outputOption = new Option<string?>("--output", "-o") { Description = "Write the result to a file instead of stdout." };
        var setOption = new Option<string[]>("--set", "-s")
        {
            Description = "Set a string variable (key=value). Repeatable; overrides JSON values.",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => [],
        };
        var includeOption = new Option<string[]>("--include-path", "-I")
        {
            Description = "Directory used to resolve include/render/from templates. Repeatable. Defaults to the template directory.",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => [],
        };
        var cultureOption = new Option<string?>("--culture") { Description = "Culture used to format numbers and dates (e.g. fr-FR). Default is invariant." };
        var timezoneOption = new Option<string?>("--timezone") { Description = "Time zone id used for dates (e.g. UTC, Europe/Paris). Default is local." };
        var strictOption = new Option<bool>("--strict") { Description = "Fail on undefined variables and filters." };
        var envOption = new Option<bool>("--env") { Description = "Expose environment variables as the 'env' object." };
        var validateOption = new Option<bool>("--validate") { Description = "Only parse the template and report errors; do not render." };
        var maxStepsOption = new Option<int>("--max-steps") { Description = "Maximum number of statements to execute (0 = unlimited)." };
        var maxRecursionOption = new Option<int>("--max-recursion") { Description = "Maximum include/render recursion depth.", DefaultValueFactory = _ => 100 };

        var root = new RootCommand(Description)
        {
            templateArgument, templateOption, inlineOption, dataOption, outputOption, setOption, includeOption,
            cultureOption, timezoneOption, strictOption, envOption, validateOption, maxStepsOption, maxRecursionOption,
        };

        root.SetAction(async (parseResult, cancellationToken) =>
        {
            var request = new Request
            {
                TemplatePath = parseResult.GetValue(templateOption) ?? parseResult.GetValue(templateArgument),
                Inline = parseResult.GetValue(inlineOption),
                DataPath = parseResult.GetValue(dataOption),
                OutputPath = parseResult.GetValue(outputOption),
                Sets = parseResult.GetValue(setOption) ?? [],
                IncludePaths = parseResult.GetValue(includeOption) ?? [],
                Culture = parseResult.GetValue(cultureOption),
                TimeZone = parseResult.GetValue(timezoneOption),
                Strict = parseResult.GetValue(strictOption),
                Env = parseResult.GetValue(envOption),
                Validate = parseResult.GetValue(validateOption),
                MaxSteps = parseResult.GetValue(maxStepsOption),
                MaxRecursion = parseResult.GetValue(maxRecursionOption),
            };

            return await ExecuteAsync(request, stdin, stdout, stderr, cancellationToken);
        });

        var parse = root.Parse(args);
        return parse.InvokeAsync(new InvocationConfiguration { Output = stdout, Error = stderr });
    }

    private sealed class Request
    {
        public string? TemplatePath, Inline, DataPath, OutputPath, Culture, TimeZone;
        public string[] Sets = [], IncludePaths = [];
        public bool Strict, Env, Validate;
        public int MaxSteps, MaxRecursion;
    }

    private static async Task<int> ExecuteAsync(Request r, TextReader stdin, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        if ((r.TemplatePath is null) == (r.Inline is null))
        {
            await stderr.WriteLineAsync("error: specify exactly one template source: a template file (positional or --template) or --inline. Use --help for usage.");
            return UsageError;
        }

        var sets = new List<KeyValuePair<string, string>>();
        foreach (var set in r.Sets)
        {
            var index = set.IndexOf('=');
            if (index <= 0)
            {
                await stderr.WriteLineAsync($"error: invalid --set value '{set}', expected key=value.");
                return UsageError;
            }
            sets.Add(new(set[..index], set[(index + 1)..]));
        }

        var optionsBuilder = new TemplateOptionsBuilder()
            .WithStrictVariables(r.Strict)
            .WithStrictFilters(r.Strict)
            .WithMaxSteps(r.MaxSteps)
            .WithMaxRecursion(r.MaxRecursion)
            .WithJsonSerializerOptions(ToolJsonContext.Default.Options);

        try
        {
            if (r.Culture is not null)
            {
                optionsBuilder.WithCultureInfo(CultureInfo.GetCultureInfo(r.Culture));
            }

            if (r.TimeZone is not null)
            {
                optionsBuilder.WithTimeZone(TemplateOptions.Default.TimeZoneResolver(r.TimeZone));
            }
        }
        catch (Exception e) when (e is CultureNotFoundException or TimeZoneNotFoundException or ArgumentException)
        {
            await stderr.WriteLineAsync($"error: {e.Message}");
            return UsageError;
        }

        string source;
        string? templateDirectory = null;
        if (r.TemplatePath is not null)
        {
            if (r.TemplatePath == "-" && r.DataPath != "-")
            {
                await stderr.WriteLineAsync("error: stdin carries the JSON data; use a template file or --inline.");
                return UsageError;
            }

            if (!File.Exists(r.TemplatePath))
            {
                await stderr.WriteLineAsync($"error: template file '{r.TemplatePath}' not found.");
                return RenderError;
            }

            source = await File.ReadAllTextAsync(r.TemplatePath, cancellationToken);
            templateDirectory = Path.GetDirectoryName(Path.GetFullPath(r.TemplatePath));
        }
        else
        {
            source = r.Inline!;
        }

        var includeRoots = r.IncludePaths.Length > 0
            ? r.IncludePaths
            : [templateDirectory ?? Directory.GetCurrentDirectory()];
        var options = optionsBuilder
            .WithFileProvider(new DirectoryTemplateFileProvider(includeRoots))
            .Build();

        var parser = new FluidParser();
        if (!parser.TryParse(source, out var template, out var parseError))
        {
            await stderr.WriteLineAsync($"error: {parseError}");
            return RenderError;
        }

        if (r.Validate)
        {
            return Success;
        }

        JsonElement? data = null;
        try
        {
            string? json = r.DataPath switch
            {
                null => null,
                "-" => await stdin.ReadToEndAsync(cancellationToken),
                var path => await File.ReadAllTextAsync(path, cancellationToken),
            };

            // Stdin is the default data source; reading it yields an empty string when nothing is piped.
            json ??= await stdin.ReadToEndAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(json))
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                data = document.RootElement.Clone();
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            await stderr.WriteLineAsync($"error: unable to read JSON data: {e.Message}");
            return RenderError;
        }

        var context = new TemplateContext(options);
        if (data is { } root)
        {
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    context.SetValue(property.Name, JsonFluidConverter.Convert(property.Value));
                }
            }
            else
            {
                context.SetValue("data", JsonFluidConverter.Convert(root));
            }
        }

        if (r.Env)
        {
            var env = new Dictionary<string, FluidValue>(StringComparer.Ordinal);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                env[(string)entry.Key] = StringValue.Create(entry.Value?.ToString() ?? "");
            }
            context.SetValue("env", new DictionaryValue(new FluidValueDictionaryFluidIndexable(env)));
        }

        foreach (var (key, value) in sets)
        {
            context.SetValue(key, StringValue.Create(value));
        }

        try
        {
            if (r.OutputPath is null)
            {
                await template.RenderAsync(stdout, context);
                await stdout.FlushAsync(cancellationToken);
            }
            else
            {
                // Render fully before writing so a failing template doesn't truncate an existing file.
                var builder = new StringWriter();
                await template.RenderAsync(builder, context);
                await File.WriteAllTextAsync(r.OutputPath, builder.ToString(), new UTF8Encoding(false), cancellationToken);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await stderr.WriteLineAsync($"error: {e.Message}");
            return RenderError;
        }

        return Success;
    }
}
