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
using Xunit;
using Xunit.Sdk;

namespace Fluid.Tests
{
    public class GoldenLiquidTests
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        private static readonly FluidParser _parser = new FluidParser(new FluidParserOptions { AllowTrailingQuestionMark = true, AllowLiquidTag = true });
        private static readonly TemplateOptions _options = new TemplateOptions();
        private static readonly Dictionary<string, string> _skippedTests = new()
        {
            // e.g. ["liquid.golden.abs_filter/negative float"] = "reason for skipping single test",
            // e.g. ["liquid.golden.abs_filter/*"] = "reason for skipping category of tests",

            ["liquid.golden.base64_url_safe_encode_filter/not a string"] = "https://github.com/Shopify/liquid/issues/1862",
            ["liquid.golden.base64_url_safe_encode_filter/from string"] = "https://github.com/Shopify/liquid/issues/1862",

            ["liquid.golden.special/first of a string"] = "https://github.com/Shopify/liquid/discussions/1881#discussioncomment-11805960",
            ["liquid.golden.special/last of a string"] = "https://github.com/Shopify/liquid/discussions/1881#discussioncomment-11805960",

            // Fluid can't distinguish between C# null and template undefined variable - both become NilValue
            // C# API expects null → use default, but Golden Liquid expects undefined → throw error
            // Prioritizing existing C# API behavior
            ["liquid.golden.truncate_filter/undefined first argument"] = "Fluid treats nil same as C# null parameter (use default)",
            ["liquid.golden.truncate_filter/undefined second argument"] = "Fluid treats nil same as C# null parameter (use default)",
            ["liquid.golden.truncatewords_filter/undefined first argument"] = "Fluid treats nil same as C# null parameter (use default)",
            ["liquid.golden.truncatewords_filter/undefined second argument"] = "Fluid treats nil same as C# null parameter (use default)",

        };

        public ITestOutputHelper TestOutputHelper { get; }

        static GoldenLiquidTests()
        {
            FluidValue ConvertJsonElement(JsonElement value, TemplateOptions options)
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

            _options.ValueConverters.Add(x => x is JsonElement ? ConvertJsonElement((JsonElement)x, _options) : null);                
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

            var parseResult = _parser.TryParse(test.Template, out var template, out var error);

            if (parseResult == false)
            {
                if (test.Error)
                {
                    return;
                }

                TestOutputHelper.WriteLine(JsonSerializer.Serialize(test, _jsonSerializerOptions));
                Assert.Fail();
            }

            Assert.True(parseResult, error?.ToString());

            var context = new TemplateContext(test.Context, _options);

            if (test.Context.Count != 0)
            {
                foreach (var item in test.Context)
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

            if (test.Error)
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
                    Assert.Equal(test.Want, result);
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
            if (_skippedTests.TryGetValue(test.Id, out var reason))
            {
                throw SkipException.ForSkip(reason);
            }
            else
            {
                var group = test.Id.Split('/')[0];

                if (_skippedTests.TryGetValue($"{group}/*", out reason))
                {
                    throw SkipException.ForSkip(reason);
                }
            }
        }
    }

    class GoldenClassData : TheoryData<GoldenTest>
    {
        private readonly string _goldenGitHash = "507a01c5453607c8d9e33d83677f639c53e36fee";
        private readonly string _testsFilePath;

        public GoldenClassData()
        {
            // Download the test specs locally if it doesn't exist
            var _goldenLiquidUrl = $"https://raw.githubusercontent.com/jg-rp/golden-liquid/{_goldenGitHash}/golden_liquid.json";

            _testsFilePath = Path.Combine(Path.GetTempPath(), $"golden_liquid.{_goldenGitHash}.json");

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

            foreach (var group in goldenTestFile.TestGroups)
            {
                foreach (var test in group.Tests)
                {
                    test.GroupName = group.Name;
                    Add(test);
                }
            }
        }
    }

    public class GoldenTestFile
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("test_groups")]
        public List<GoldenTestGroup> TestGroups { get; set; } = [];
    }

    public class GoldenTestGroup
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tests")]
        public List<GoldenTest> Tests { get; set; } = [];
    }

    public class GoldenTest : IXunitSerializable
    {
        /// <summary>
        /// Descriptive name for the test case. Together with the group name it should uniquely identify the test case.
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
        [JsonPropertyName("want")]
        public string Want { get; set; }

        /// <summary>
        /// JSON object mapping strings to arbitrary, possibly nested, strings, numbers, arrays, objects and booleans. These are the variables that the associated template should be rendered with.
        /// </summary>
        [JsonPropertyName("context")]
        public Dictionary<string, object> Context { get; set; } = [];

        /// <summary>
        /// JSON object mapping strings to strings. You can think of it as a mock file system for testing {% include %} and {% render %}.
        /// </summary>
        [JsonPropertyName("partials")]
        public Dictionary<string, string> Partials { get; set; } = [];

        /// <summary>
        /// Boolean indicating if the test case should raise/throw an exception/error.
        /// </summary>
        [JsonPropertyName("error")]
        public bool Error { get; set; }

        /// <summary>
        /// Boolean indicating if the test should be rendered in "strict mode", if the target environment has a strict mode.
        /// </summary>
        [JsonPropertyName("strict")]
        public bool Strict { get; set; }

        public string GroupName { get; set; }

        public string Id => $"{GroupName}/{Name}";

        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            Name = info.GetValue<string>(nameof(Name));
            GroupName = info.GetValue<string>(nameof(GroupName));
            Template = info.GetValue<string>(nameof(Template));
            Want = info.GetValue<string>(nameof(Want));
            Context = JsonSerializer.Deserialize<Dictionary<string, object>>(info.GetValue<string>(nameof(Context))) ?? [];
            Partials = JsonSerializer.Deserialize<Dictionary<string, string>>(info.GetValue<string>(nameof(Partials))) ?? [];
            Error = info.GetValue<bool>(nameof(Error));
            Strict = info.GetValue<bool>(nameof(Strict));
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Name), Name);
            info.AddValue(nameof(Template), Template);
            info.AddValue(nameof(Want), Want);
            info.AddValue(nameof(Context), JsonSerializer.Serialize(Context));
            info.AddValue(nameof(Partials), JsonSerializer.Serialize(Partials));
            info.AddValue(nameof(Error), Error);
            info.AddValue(nameof(Strict), Strict);
            info.AddValue(nameof(GroupName), GroupName);
        }

        public override string ToString() => Id;
    }
}
