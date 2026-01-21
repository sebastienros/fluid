using Fluid.Tests.Mocks;
using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TimeZoneConverter;
using Xunit;
using Xunit.Sdk;

namespace Fluid.Tests
{
    public class GoldenLiquidTests
    {
        private static readonly TimeZoneInfo Pacific = TZConvert.GetTimeZoneInfo("America/Los_Angeles");
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        private static readonly Dictionary<string, string> _skippedTests = new()
        {
            // e.g. ["id:negative_float"] = "reason for skipping single test",
            // e.g. ["tag:[tag]"] = "reason for skipping category of tests",

            ["tag:base64_url_safe_encode filter"] = "https://github.com/Shopify/liquid/issues/1862",
            ["id:filters_date_missing_argument"] = "test expects error but Shopify parses date",
            ["id:filters_date_undefined_argument"] = "test expects error but Shopify parses date",

            ["tag:strict2"] = "Fluid uses lenient parsing mode; strict2 mode not implemented",

            ["id:tags_liquid_single_line_comment_tag"] = "Skipping: `{% liquid %}` comment/endcomment special-case parsing not implemented",
            ["id:tags_liquid_multi-line_comment_tag"] = "Skipping: `{% liquid %}` multi-line comment/endcomment special-case parsing not implemented",

            // Nested comment blocks and special comment parsing: Shopify Liquid tracks comment nesting depth
            // and handles raw/incomplete/malformed tags specially inside comments. Implementing this would
            // require a custom comment parser with state tracking.
            ["id:tags_comment_nested_comment_blocks"] = "Nested comment block tracking not implemented",
            ["id:tags_comment_nested_comment_blocks_with_nested_tags"] = "Nested comment block tracking not implemented",
            ["id:tags_comment_raw_inside_comment_block"] = "Raw tag handling inside comment blocks not implemented",
            ["id:tags_comment_incomplete_tags_are_not_parsed"] = "Incomplete tag detection inside comments not implemented",
            ["id:tags_comment_malformed_tags_are_not_parsed"] = "Malformed tag detection inside comments not implemented",

            // Inline comment multi-line enforcement: Shopify requires # on each line for multi-line inline comments
            ["id:tags_inline_comment_enforce_leading_hash"] = "Multi-line inline comment validation not implemented",

            ["id:tags_if_endswith_is_not_a_valid_operator"] = "Skipping: Shopify Liquid doesn't support `endswith`/`startswith` as operators; Fluid keeps them for backward compatibility",
            ["id:tags_if_startswith_is_not_a_valid_operator"] = "Skipping: Shopify Liquid doesn't support `endswith`/`startswith` as operators; Fluid keeps them for backward compatibility",

            // Trailing question mark tests: Ruby supports '?' in identifiers (e.g., 'foo?' maps to hash["foo?"]),
            // but .NET doesn't support '?' in property names. Fluid strips the '?' to allow Ruby-style syntax
            // like 'product.empty?' to map to .NET properties like 'product.empty'.
            ["id:identifiers_trailing_question_mark_output"] = "Fluid strips trailing '?' for .NET compatibility; Ruby keeps it as part of the identifier",
            ["id:identifiers_trailing_question_mark_in_for_loop_target"] = "Fluid strips trailing '?' for .NET compatibility; Ruby keeps it as part of the identifier",
        };

        public ITestOutputHelper TestOutputHelper { get; }

        private static FluidValue ConvertJsonElement(JsonElement value, TemplateOptions options)
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.Array => ArrayValue.Create(jsonElement.EnumerateArray().Select(x => ConvertJsonElement(x, options)), options),
                    JsonValueKind.Object => ObjectValue.Create(jsonElement.EnumerateObject().ToDictionary(x => x.Name, x => ConvertJsonElement(x.Value, options)), options),
                    JsonValueKind.String => StringValue.Create(jsonElement.GetString()),
                    JsonValueKind.Number when jsonElement.TryGetInt32(out int i) => NumberValue.Create(i),
                    JsonValueKind.Number when jsonElement.TryGetDecimal(out decimal d) => NumberValue.Create(d),
                    JsonValueKind.True => BooleanValue.True,
                    JsonValueKind.False => BooleanValue.False,
                    _ => NilValue.Instance
                };
            }
            return null;
        }

        private static TemplateOptions CreateOptions()
        {
            var options = new TemplateOptions();
            options.ValueConverters.Add(x => x is JsonElement ? ConvertJsonElement((JsonElement)x, options) : null);
            return options;
        }

        private static FluidParser CreateParser()
        {
            return new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true, AllowLiquidTag = true });
        }

        public GoldenLiquidTests(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper;
        }

        [Theory]
        [ClassData(typeof(GoldenClassData))]
        public async ValueTask GoldenTestShouldPass(GoldenTest test)
        {
            CheckNotSkippedTest(test);

            var parser = CreateParser();
            var parseResult = parser.TryParse(test.Template, out var template, out var error);

            if (parseResult == false)
            {
                if (test.Invalid)
                {
                    return;
                }

                TestOutputHelper.WriteLine(JsonSerializer.Serialize(test, _jsonSerializerOptions));
                Assert.Fail();
            }

            Assert.True(parseResult, error?.ToString());

            var context = new TemplateContext(CreateOptions());

            context.TimeZone = test.Tags.Contains("utc") ? TimeZoneInfo.Utc : Pacific;

            if (test.Data.Count != 0)
            {
                foreach (var item in test.Data)
                {
                    context.SetValue(item.Key, item.Value);
                }
            }

            var fileSystem = new MockFileProvider();
            context.Options.FileProvider = fileSystem;

            if (test.Partials.Count != 0)
            {
                foreach (var partial in test.Partials)
                {
                    fileSystem.Add(partial.Key + ".liquid", partial.Value);
                }
            }

            if (test.Invalid || test.Absent)
            {
                try
                {
                    await Assert.ThrowsAnyAsync<Exception>(async () => await template.RenderAsync(context));
                }
                catch
                {
                    TestOutputHelper.WriteLine(JsonSerializer.Serialize(test, _jsonSerializerOptions));
                    throw;
                }
            }
            else
            {
                var result = await template.RenderAsync(context);

                try
                {
                    if (test.Results.Length > 0)
                    {
                        Assert.Contains(result, test.Results);
                    }
                    else
                    {
                        Assert.Equal(test.Result, result);
                    }
                }
                catch
                {
                    TestOutputHelper.WriteLine(JsonSerializer.Serialize(test, _jsonSerializerOptions));
                    throw;
                }
            }
        }

        private static void CheckNotSkippedTest(GoldenTest test)
        {
            if (_skippedTests.TryGetValue("id:" + test.Id, out var reason))
            {
                throw SkipException.ForSkip(reason);
            }
            else if (_skippedTests.TryGetValue("id:" + test.Id.Split('_')[0] + "_*", out reason))
            {
                throw SkipException.ForSkip(reason);
            }
            else
            {
                foreach (var tag in test.Tags)
                {
                    if (_skippedTests.TryGetValue($"tag:{tag}", out reason))
                    {
                        throw SkipException.ForSkip(reason);
                    }
                }
            }
        }
    }

    class GoldenClassData : TheoryData<GoldenTest>
    {
        private readonly string _testsFilePath;

        private static string GetGoldenGitHash()
        {
            // Read the hash from the version file in the .github directory
            var versionFilePath = Path.Combine(GetRepositoryRoot(), ".github", "golden-liquid-version.txt");
            return File.ReadAllText(versionFilePath).Trim();
        }

        private static string GetRepositoryRoot()
        {
            // Navigate up from the test assembly location to find the repository root
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Fluid.sln")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("Could not find repository root");
        }

        public GoldenClassData()
        {
            // Download the test specs locally if it doesn't exist
            var goldenGitHash = GetGoldenGitHash();
            var _goldenLiquidUrl = $"https://raw.githubusercontent.com/jg-rp/golden-liquid/{goldenGitHash}/golden_liquid.json";

            _testsFilePath = Path.Combine(Path.GetTempPath(), $"golden_liquid.{goldenGitHash}.json");

            if (!File.Exists(_testsFilePath))
            {
                using (var client = new HttpClient())
                {
                    var content = client.GetStringAsync(_goldenLiquidUrl).Result;
                    Directory.CreateDirectory(Path.GetDirectoryName(_testsFilePath));
                    File.WriteAllText(_testsFilePath, content);
                }
            }

            // Read the test specs from _testsFilePath file
            var goldenTestFile = JsonSerializer.Deserialize<GoldenTestFile>(File.ReadAllText(_testsFilePath));

            foreach (var test in goldenTestFile.Tests)
            {
                if (test.Tags.Remove("rigid"))
                {
                    test.Rigid = true;
                }
                if (test.Tags.Remove("strict"))
                {
                    test.Strict = true;
                }
                if (test.Tags.Remove("absent"))
                {
                    test.Absent = true;
                }
                
                test.Id = JsonNamingPolicy.SnakeCaseLower.ConvertName(test.Name.Replace(",", " "));

                Add(test);
            }
            
        }
    }

    public class GoldenTestFile
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }


        [JsonPropertyName("tests")]
        public List<GoldenTest> Tests { get; set; } = [];
    }

    public class GoldenTest : IXunitSerializable
    {
        /// <summary>
        /// Descriptive name for the test case.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Liquid source text as a string.
        /// </summary>
        [JsonPropertyName("template")]
        public string Template { get; set; }

        /// <summary>
        /// Expected result of rendering the template with the associated context.
        /// </summary>
        [JsonPropertyName("result")]
        public string Result { get; set; }

        /// <summary>
        /// Expected result of rendering the template with the associated context.
        /// </summary>
        [JsonPropertyName("results")]
        public string[] Results { get; set; } = [];

        /// <summary>
        /// JSON object mapping strings to arbitrary, possibly nested, strings, numbers, arrays, objects and booleans. These are the variables that the associated template should be rendered with.
        /// </summary>
        [JsonPropertyName("data")]
        public Dictionary<string, object> Data { get; set; } = [];

        /// <summary>
        /// Array of strings indicating which Liquid tags and/or filters are being tested.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = [];

        /// <summary>
        /// JSON object mapping strings to strings. You can think of it as a mock file system for testing {% include %} and {% render %}.
        /// </summary>
        [JsonPropertyName("templates")]
        public Dictionary<string, string> Partials { get; set; } = [];

        /// <summary>
        /// Boolean indicating if the test case should raise/throw an exception/error.
        /// </summary>
        [JsonPropertyName("invalid")]
        public bool Invalid { get; set; }

        public bool Absent { get; set; }
        public bool Strict { get; set; }

        public bool Rigid { get; set; }

        public string Id { get; set; }

        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            Name = info.GetValue<string>(nameof(Name));
            Id = info.GetValue<string>(nameof(Id));
            Template = info.GetValue<string>(nameof(Template));
            Result = info.GetValue<string>(nameof(Result));
            Results = JsonSerializer.Deserialize<string[]>(info.GetValue<string>(nameof(Results))) ?? [];
            Tags = JsonSerializer.Deserialize<List<string>>(info.GetValue<string>(nameof(Tags))) ?? new List<string>();
            Data = JsonSerializer.Deserialize<Dictionary<string, object>>(info.GetValue<string>(nameof(Data))) ?? [];
            Partials = JsonSerializer.Deserialize<Dictionary<string, string>>(info.GetValue<string>(nameof(Partials))) ?? [];
            Invalid = info.GetValue<bool>(nameof(Invalid));
            Strict = info.GetValue<bool>(nameof(Strict));
            Rigid = info.GetValue<bool>(nameof(Rigid));
            Absent = info.GetValue<bool>(nameof(Absent));
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Name), Name);
            info.AddValue(nameof(Template), Template);
            info.AddValue(nameof(Result), Result);
            info.AddValue(nameof(Results), JsonSerializer.Serialize(Results));
            info.AddValue(nameof(Tags), JsonSerializer.Serialize(Tags));
            info.AddValue(nameof(Data), JsonSerializer.Serialize(Data));
            info.AddValue(nameof(Partials), JsonSerializer.Serialize(Partials));
            info.AddValue(nameof(Invalid), Invalid);
            info.AddValue(nameof(Strict), Strict);
            info.AddValue(nameof(Rigid), Rigid);
            info.AddValue(nameof(Id), Id);
            info.AddValue(nameof(Absent), Absent);
        }

        public override string ToString() => Id;
    }
}
