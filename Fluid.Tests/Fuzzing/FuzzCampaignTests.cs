using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Fluid.Filters;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests.Fuzzing;

public class FuzzCampaignTests
{
    private const int DefaultParserSeed = 0x5EED1234;
    private const int DefaultParserCases = 256;
    private const int DefaultFilterSeed = 0x0F17E25;
    private const int DefaultFilterCases = 32;

    private static readonly string[] BuiltInFilterNames =
    [
        "abs", "append", "at_least", "at_most", "base64_decode", "base64_encode",
        "base64_url_safe_decode", "base64_url_safe_encode", "brightness_difference", "capitalize",
        "ceil", "color_brightness", "color_contrast", "color_darken", "color_desaturate",
        "color_difference", "color_extract", "color_lighten", "color_modify", "color_saturate",
        "color_to_hex", "color_to_hsl", "color_to_rgb", "compact", "concat", "date", "default",
        "divided_by", "downcase", "escape", "escape_once", "find", "find_index", "first", "floor",
        "format_date", "format_number", "format_string", "handle", "handleize", "has", "hmac_sha1",
        "hmac_sha256", "join", "json", "last", "lstrip", "map", "md5", "minus", "modulo",
        "money", "money_with_currency", "money_without_currency", "money_without_trailing_zeros",
        "newline_to_br", "plus", "prepend", "raw", "reject", "remove", "remove_first", "remove_last",
        "replace", "replace_first", "replace_last", "reverse", "round", "rstrip", "sha1", "sha256",
        "size", "slice", "sort", "sort_natural", "split", "strip", "strip_html", "strip_newlines",
        "sum", "time_zone", "times", "truncate", "truncatewords", "uniq", "upcase", "url_decode",
        "url_encode", "where"
    ];

    [Fact]
    [Trait("Category", "Fuzz")]
    public async Task InterpretedAndCompiledParsersStayEquivalent()
    {
        var seed = GetEnvironmentInt("FLUID_FUZZ_SEED", DefaultParserSeed);
        var generatedCases = GetEnvironmentInt("FLUID_FUZZ_CASES", DefaultParserCases);
        var sources = BuildParserSources(seed, generatedCases).ToArray();

        for (var optionMask = 0; optionMask < 16; optionMask++)
        {
            var interpreted = new FluidParser(CreateParserOptions(optionMask));
            var compiled = new FluidParser(CreateParserOptions(optionMask)).Compile();

            foreach (var source in sources)
            {
                var interpretedOutcome = Parse(interpreted, source);
                var compiledOutcome = Parse(compiled, source);

                Assert.DoesNotContain("unexpected:", interpretedOutcome.Diagnostic, StringComparison.Ordinal);
                Assert.DoesNotContain("unexpected:", compiledOutcome.Diagnostic, StringComparison.Ordinal);
                Assert.Equal(
                    interpretedOutcome.ToDiagnostic(source, optionMask),
                    compiledOutcome.ToDiagnostic(source, optionMask));

                if (interpretedOutcome.Template == null)
                {
                    continue;
                }

                var interpretedRender = await RenderAsync(interpretedOutcome.Template);
                var compiledRender = await RenderAsync(compiledOutcome.Template);

                Assert.DoesNotContain("unexpected:", interpretedRender.Diagnostic, StringComparison.Ordinal);
                Assert.DoesNotContain("unexpected:", compiledRender.Diagnostic, StringComparison.Ordinal);
                Assert.Equal(
                    interpretedRender.ToDiagnostic(source, optionMask),
                    compiledRender.ToDiagnostic(source, optionMask));
            }
        }

        Console.WriteLine(
            "Parser fuzz seed {0}; {1} sources x 16 option combinations x 2 parser modes = {2} parses.",
            seed,
            sources.Length,
            sources.Length * 32);
    }

    [Fact]
    [Trait("Category", "Fuzz")]
    public async Task EveryBuiltInFilterHandlesAdversarialValuesDeterministically()
    {
        var seed = GetEnvironmentInt("FLUID_FILTER_FUZZ_SEED", DefaultFilterSeed);
        var generatedCases = GetEnvironmentInt("FLUID_FILTER_FUZZ_CASES", DefaultFilterCases);
        var filters = CreateAllFilters();
        var actualNames = filters.Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.Equal(BuiltInFilterNames.OrderBy(x => x, StringComparer.Ordinal), actualNames);

        var inputs = BuildFilterInputs(seed, generatedCases).ToArray();
        var argumentSets = BuildFilterArguments().ToArray();
        var invocations = 0;

        foreach (var pair in filters)
        {
            foreach (var input in inputs)
            {
                foreach (var arguments in argumentSets)
                {
                    var first = await InvokeFilterAsync(pair.Value, input, arguments);
                    var second = await InvokeFilterAsync(pair.Value, input, arguments);
                    invocations += 2;
                    var firstDiagnostic = first.ToDiagnostic(pair.Key, input, arguments);
                    var secondDiagnostic = second.ToDiagnostic(pair.Key, input, arguments);

                    Assert.DoesNotContain("unexpected:", firstDiagnostic, StringComparison.Ordinal);
                    Assert.DoesNotContain("unexpected:", secondDiagnostic, StringComparison.Ordinal);
                    Assert.Equal(firstDiagnostic, secondDiagnostic);
                }
            }
        }

        Console.WriteLine(
            "Filter fuzz seed {0}; {1} filters x {2} values x {3} argument sets x 2 runs = {4} invocations.",
            seed,
            filters.Count,
            inputs.Length,
            argumentSets.Length,
            invocations);
    }

    private static ParseOutcome Parse(FluidParser parser, string source)
    {
        try
        {
            var template = parser.Parse(source);

            if (template == null)
            {
                return new ParseOutcome(null, "Parser returned null without a parse error.");
            }

            var tryParseSucceeded = parser.TryParse(source, out var tryParseTemplate, out var tryParseError);
            if (!tryParseSucceeded || tryParseTemplate == null || tryParseError != null)
            {
                return new ParseOutcome(
                    null,
                    $"Parse succeeded but TryParse differed: success={tryParseSucceeded}, template={tryParseTemplate != null}, error={tryParseError}");
            }

            return new ParseOutcome(template, "accepted");
        }
        catch (ParseException exception)
        {
            var tryParseSucceeded = parser.TryParse(source, out var template, out var error);
            if (tryParseSucceeded || template != null || String.IsNullOrEmpty(error))
            {
                return new ParseOutcome(
                    null,
                    $"Parse rejected but TryParse differed: success={tryParseSucceeded}, template={template != null}, error={error}");
            }

            return new ParseOutcome(null, $"rejected:{exception.Message}");
        }
        catch (Exception exception)
        {
            return new ParseOutcome(null, $"unexpected:{exception.GetType().FullName}:{exception.Message}");
        }
    }

    private static async Task<RenderOutcome> RenderAsync(IFluidTemplate template)
    {
        try
        {
            var result = await template.RenderAsync(CreateContext());
            return new RenderOutcome($"rendered:{result}");
        }
        catch (Exception exception) when (IsExpectedInputException(exception))
        {
            return new RenderOutcome($"rejected:{exception.GetType().FullName}:{exception.Message}");
        }
        catch (Exception exception)
        {
            return new RenderOutcome($"unexpected:{exception.GetType().FullName}:{exception.Message}");
        }
    }

    private static async Task<FilterOutcome> InvokeFilterAsync(
        FilterDelegate filter,
        FluidValue input,
        FilterArguments arguments)
    {
        try
        {
            var result = await filter(input, arguments, CreateContext());
            if (result == null)
            {
                return new FilterOutcome("unexpected:null result");
            }

            return new FilterOutcome($"result:{result.Type}:{result.ToStringValue()}");
        }
        catch (Exception exception) when (IsExpectedInputException(exception))
        {
            return new FilterOutcome($"rejected:{exception.GetType().FullName}:{exception.Message}");
        }
        catch (Exception exception)
        {
            return new FilterOutcome($"unexpected:{exception.GetType().FullName}:{exception.Message}");
        }
    }

    private static bool IsExpectedInputException(Exception exception)
    {
        return exception is LiquidException
            or ArgumentException
            or FormatException
            or OverflowException
            or DivideByZeroException
            or JsonException
            or InvalidOperationException
            or IOException;
    }

    private static TemplateContext CreateContext()
    {
        var options = new TemplateOptions
        {
            MaxSteps = 20_000,
            MaxOutputSize = 32_768,
            MaxCollectionSize = 4_096,
            MaxRecursion = 64,
            CultureInfo = CultureInfo.InvariantCulture,
            TimeZone = TimeZoneInfo.Utc,
            Now = static () => new DateTimeOffset(2000, 1, 2, 3, 4, 5, TimeSpan.Zero)
        };

        options.Filters.WithColorFilters().WithMoneyFilters();

        return new TemplateContext(options)
            .SetValue("nil", NilValue.Instance)
            .SetValue("empty", "")
            .SetValue("text", "a<&\r\nb")
            .SetValue("number", Decimal.MaxValue)
            .SetValue("items", new object[] { 1, "two", null, new { name = "three" } })
            .SetValue("object", new { name = "value", enabled = true });
    }

    private static FilterCollection CreateAllFilters()
    {
        return new FilterCollection()
            .WithArrayFilters()
            .WithStringFilters()
            .WithNumberFilters()
            .WithMiscFilters()
            .WithColorFilters()
            .WithMoneyFilters();
    }

    private static IEnumerable<FluidValue> BuildFilterInputs(int seed, int generatedCases)
    {
        yield return NilValue.Instance;
        yield return UndefinedValue.Instance;
        yield return EmptyValue.Instance;
        yield return BlankValue.Instance;
        yield return BooleanValue.False;
        yield return BooleanValue.True;
        yield return NumberValue.Zero;
        yield return NumberValue.Create(-1);
        yield return NumberValue.Create(Decimal.MinValue);
        yield return NumberValue.Create(Decimal.MaxValue);
        yield return StringValue.Empty;
        yield return new StringValue("abc");
        yield return new StringValue("\0\u0001\r\n\t");
        yield return new StringValue("\ud800");
        yield return new StringValue("%%%not-base64%%%");
        yield return new StringValue("#00ff7f");
        yield return new StringValue("rgb(1, 2, 3)");
        yield return new StringValue("2024-02-29T23:59:59Z");
        yield return ArrayValue.Empty;
        yield return new ArrayValue(
        [
            NilValue.Instance,
            BooleanValue.False,
            NumberValue.Create(-1),
            new StringValue("x"),
            new ArrayValue([NumberValue.Create(2)])
        ]);
        yield return new DictionaryValue(
            new FluidValueDictionaryFluidIndexable(
                new Dictionary<string, FluidValue>(StringComparer.Ordinal)
                {
                    ["name"] = new StringValue("value"),
                    ["enabled"] = BooleanValue.True,
                    ["count"] = NumberValue.Create(3)
                }));
        yield return new DateTimeValue(DateTimeOffset.MinValue);
        yield return new DateTimeValue(DateTimeOffset.MaxValue);

        var random = new Random(seed);
        for (var i = 0; i < generatedCases; i++)
        {
            switch (random.Next(4))
            {
                case 0:
                    yield return new StringValue(RandomText(random, random.Next(0, 96)));
                    break;
                case 1:
                    yield return NumberValue.Create((decimal)random.NextInt64(Int64.MinValue, Int64.MaxValue) / 1000);
                    break;
                case 2:
                    yield return new ArrayValue(
                    [
                        NumberValue.Create(random.Next(-1000, 1001)),
                        new StringValue(RandomText(random, random.Next(0, 24))),
                        random.Next(2) == 0 ? NilValue.Instance : BooleanValue.True
                    ]);
                    break;
                default:
                    yield return new StringValue(
                        random.Next(2) == 0
                            ? $"#{random.Next(0x1000000):x6}"
                            : $"hsl({random.Next(-720, 721)}, {random.Next(-100, 201)}%, {random.Next(-100, 201)}%)");
                    break;
            }
        }
    }

    private static IEnumerable<FilterArguments> BuildFilterArguments()
    {
        yield return new FilterArguments();
        yield return new FilterArguments(NilValue.Instance);
        yield return new FilterArguments(BooleanValue.False);
        yield return new FilterArguments(NumberValue.Zero);
        yield return new FilterArguments(NumberValue.Create(-1));
        yield return new FilterArguments(NumberValue.Create(Decimal.MaxValue));
        yield return new FilterArguments(StringValue.Empty);
        yield return new FilterArguments(new StringValue("x"));
        yield return new FilterArguments(new StringValue("%%%not-base64%%%"));
        yield return new FilterArguments(new StringValue("red"), NumberValue.Create(255));
        yield return new FilterArguments(NumberValue.Create(Int32.MinValue), NumberValue.Create(Int32.MaxValue));
        yield return new FilterArguments(StringValue.Empty, new StringValue("insert"));
        yield return new FilterArguments(new StringValue("x"), new StringValue("y"), new StringValue("excess"));
        yield return new FilterArguments().Add("currency", new StringValue("ZZZ"));
        yield return new FilterArguments().Add("allow_false", BooleanValue.True).Add(new StringValue("fallback"));
    }

    private static IEnumerable<string> BuildParserSources(int seed, int generatedCases)
    {
        var sources = new HashSet<string>(StringComparer.Ordinal)
        {
            "",
            "plain text",
            "\0\u0001\u001f",
            "\ud800",
            "{{",
            "}}",
            "{{ }}",
            "{{- -}}",
            "{{ value",
            "{{ value }",
            "{% ",
            "{%",
            "%}",
            "{% if %}",
            "{% if true %}",
            "{% endif %}",
            "{% if true %}x{% endunless %}",
            "{% for x in %}{% endfor %}",
            "{% for x in (1..3) %}{{ x }}{% endfor %}",
            "{% case x %}{% when %}{% endcase %}",
            "{% assign = 1 %}",
            "{% assign x = %}",
            "{% capture %}{% endcapture %}",
            "{% liquid\nassign x = 1\necho x\n%}",
            "{{ a | }}",
            "{{ a || b }}",
            "{{ a | append: }}",
            "{{ a | replace: '', '' }}",
            "{{ (1..2) }}",
            "{{ f(1, 2) }}",
            "{{ object.member? }}",
            "{{ [broken }}",
            "{{ 'unterminated }}",
            "{{ \"unterminated }}",
            "{{ 999999999999999999999999999999999999999999 }}",
            "{{ 1.2.3 }}",
            "{{ true and or false }}",
            "{{ nil contains nil }}",
            "{% raw %}{{{% endraw %}",
            "{% comment %}\0\ud800{% endcomment %}",
            "{%-if true-%}x{%-endif-%}",
            "{{- text -}}",
            new string('x', 4096),
            "{{ '" + new string('x', 4096) + "' }}",
            CreateNestedBlocks(64, closeAll: true),
            CreateNestedBlocks(64, closeAll: false)
        };

        var random = new Random(seed);
        var mutationSeeds = sources.ToArray();

        for (var i = 0; i < generatedCases; i++)
        {
            string source;
            switch (random.Next(6))
            {
                case 0:
                    source = RandomText(random, random.Next(0, 160));
                    break;
                case 1:
                    source = "{{ " + RandomExpression(random) + RandomClosingDelimiter(random);
                    break;
                case 2:
                    source = "{% " + RandomTag(random) + RandomClosingTagDelimiter(random);
                    break;
                case 3:
                    source = CreateNestedBlocks(random.Next(1, 33), random.Next(2) == 0);
                    break;
                case 4:
                    source = Mutate(random, mutationSeeds[random.Next(mutationSeeds.Length)]);
                    break;
                default:
                    source = String.Concat(
                        RandomText(random, random.Next(0, 40)),
                        "{{ ",
                        RandomExpression(random),
                        " }}",
                        RandomText(random, random.Next(0, 40)));
                    break;
            }

            sources.Add(source);
        }

        return sources;
    }

    private static string RandomExpression(Random random)
    {
        string[] atoms =
        [
            "nil", "true", "false", "empty", "blank", "0", "-1", "1.0", "1e2", "a", "a.b",
            "a['x']", "'x'", "\"x\"", "(1..3)", "f()", "a?", "\0", "\ud800"
        ];
        string[] operators =
        [
            "", " and ", " or ", " == ", " != ", " > ", " < ", " contains ", " | append: ",
            " | slice: ", " | replace: '', "
        ];

        return atoms[random.Next(atoms.Length)]
            + operators[random.Next(operators.Length)]
            + atoms[random.Next(atoms.Length)];
    }

    private static string RandomTag(Random random)
    {
        string[] tags =
        [
            "if true", "unless false", "endif", "endfor", "assign x = 1", "assign =",
            "for x in (1..3)", "break", "continue", "capture x", "endcapture", "case x",
            "when 1", "else", "echo x", "liquid", "include 'x'", "render 'x'", "\0", "\ud800"
        ];

        return tags[random.Next(tags.Length)];
    }

    private static string RandomClosingDelimiter(Random random)
    {
        string[] delimiters = [" }}", " -}}", " }", " %}", "", "\0}}"];
        return delimiters[random.Next(delimiters.Length)];
    }

    private static string RandomClosingTagDelimiter(Random random)
    {
        string[] delimiters = [" %}", " -%}", " }", " }}", "", "\0%}"];
        return delimiters[random.Next(delimiters.Length)];
    }

    private static string CreateNestedBlocks(int depth, bool closeAll)
    {
        var builder = new StringBuilder(depth * 32);
        for (var i = 0; i < depth; i++)
        {
            builder.Append("{% if true %}");
        }

        builder.Append('x');

        var closeCount = closeAll ? depth : depth / 2;
        for (var i = 0; i < closeCount; i++)
        {
            builder.Append("{% endif %}");
        }

        return builder.ToString();
    }

    private static string Mutate(Random random, string source)
    {
        if (source.Length == 0)
        {
            return RandomText(random, 1);
        }

        var index = random.Next(source.Length);
        return random.Next(3) switch
        {
            0 => source.Remove(index, 1),
            1 => source.Insert(index, RandomText(random, random.Next(1, 5))),
            _ => source[..index] + RandomText(random, 1) + source[(index + 1)..]
        };
    }

    private static string RandomText(Random random, int length)
    {
        ReadOnlySpan<char> alphabet =
        [
            '{', '}', '%', '|', ':', ',', '\'', '"', '-', '?', '.', '[', ']', '(', ')', ' ',
            '\t', '\r', '\n', '\0', '\u0001', '\u001f', '\u007f', '\u00a0', '\u2028',
            '\ud800', '\udfff', '\u03bb', '\u4e2d', 'a', 'Z', '0', '9', '<', '>', '&'
        ];
        var builder = new StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            builder.Append(alphabet[random.Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    private static FluidParserOptions CreateParserOptions(int mask)
    {
        return new FluidParserOptions
        {
            AllowFunctions = (mask & 1) != 0,
            AllowParentheses = (mask & 2) != 0,
            AllowLiquidTag = (mask & 4) != 0,
            AllowTrailingQuestionMark = (mask & 8) != 0
        };
    }

    private static int GetEnvironmentInt(string name, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            && result >= 0
                ? result
                : defaultValue;
    }

    private sealed record ParseOutcome(IFluidTemplate Template, string Diagnostic)
    {
        public string ToDiagnostic(string source, int optionMask)
        {
            return $"options={optionMask}; source={JsonSerializer.Serialize(source)}; {Diagnostic}";
        }
    }

    private sealed record RenderOutcome(string Diagnostic)
    {
        public string ToDiagnostic(string source, int optionMask)
        {
            return $"options={optionMask}; source={JsonSerializer.Serialize(source)}; {Diagnostic}";
        }
    }

    private sealed record FilterOutcome(string Diagnostic)
    {
        public string ToDiagnostic(string filter, FluidValue input, FilterArguments arguments)
        {
            return $"filter={filter}; input={input.Type}:{JsonSerializer.Serialize(input.ToStringValue())}; args={arguments.Count}; {Diagnostic}";
        }
    }
}
