using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Fluid;
using Fluid.Filters;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SourceGenPerson))]
    [JsonSerializable(typeof(SourceGenAddress))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(Dictionary<string, int>))]
    [JsonSerializable(typeof(string[]))]
    public partial class FluidJsonContext : JsonSerializerContext { }

    public sealed class SourceGenPerson
    {
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public int Age { get; init; }
        public SourceGenAddress HomeAddress { get; init; }
        public List<string> Tags { get; init; }
        public Dictionary<string, int> Scores { get; init; }
    }

    public sealed class SourceGenAddress
    {
        public string Street { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
    }

    public class JsonSourceGenTests
    {
        private static readonly FluidParser _parser = new FluidParser();

        private static IFluidTemplate Parse(string liquid)
        {
            Assert.True(_parser.TryParse(liquid, out var template, out var errors), errors);
            return template;
        }

        private static TemplateContext CreateContext(Action<TemplateOptions> configureOptions = null)
        {
            var options = new TemplateOptions();
            configureOptions?.Invoke(options);
            return new TemplateContext(options);
        }

        [Fact]
        public void JsonFilter_UsesSourceGeneratedContext_ForSimpleObject()
        {
            var person = new SourceGenPerson
            {
                FirstName = "John",
                LastName = "Doe",
                Age = 42,
                HomeAddress = new SourceGenAddress { Street = "123 Main", City = "Metropolis" },
                Tags = new List<string> { "admin", "author" },
                Scores = new Dictionary<string, int> { ["math"] = 95, ["science"] = 90 }
            };

            var ctx = CreateContext(o =>
            {
                var genOptions = new JsonSerializerOptions
                {
                    TypeInfoResolver = JsonTypeInfoResolver.Combine(FluidJsonContext.Default, new DefaultJsonTypeInfoResolver()),
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                o.JsonSerializerOptions = genOptions;
            });

            ctx.Options.Filters.WithMiscFilters();
            ctx.SetValue("person", person);
            var template = Parse("{{ person | json }}");
            var rendered = template.Render(ctx);
            var direct = JsonSerializer.Serialize(person, FluidJsonContext.Default.SourceGenPerson);
            Assert.Equal(direct, rendered);
        }

        [Fact]
        public void JsonFilter_UsesSourceGen_ForArraysAndDictionaries()
        {
            var ctx = CreateContext(o =>
            {
                var genOptions = new JsonSerializerOptions
                {
                    TypeInfoResolver = JsonTypeInfoResolver.Combine(FluidJsonContext.Default, new DefaultJsonTypeInfoResolver())
                };
                o.JsonSerializerOptions = genOptions;
            });

            ctx.Options.Filters.WithMiscFilters();
            var tags = new[] { "one", "two", "three" };
            var scores = new Dictionary<string, int> { ["alpha"] = 1, ["beta"] = 2 };
            ctx.SetValue("tags", tags);
            ctx.SetValue("scores", scores);
            var template = Parse("Tags: {{ tags | json }} Scores: {{ scores | json }}");
            var rendered = template.Render(ctx);
            var directTags = JsonSerializer.Serialize(tags, FluidJsonContext.Default.StringArray);
            var directScores = JsonSerializer.Serialize(scores, FluidJsonContext.Default.DictionaryStringInt32);
            Assert.Equal($"Tags: {directTags} Scores: {directScores}", rendered);
        }

        [Fact]
        public void JsonFilter_FluidValueWrapping_DoesNotBreakSourceGen()
        {
            var ctx = CreateContext(o =>
            {
                var genOptions = new JsonSerializerOptions
                {
                    TypeInfoResolver = JsonTypeInfoResolver.Combine(FluidJsonContext.Default, new DefaultJsonTypeInfoResolver())
                };
                o.JsonSerializerOptions = genOptions;
            });

            ctx.Options.Filters.WithMiscFilters();
            var fluidString = new StringValue("Hello SourceGen");
            ctx.SetValue("msg", fluidString);
            var array = new object[] { "A", 123, true };
            ctx.SetValue("mixed", array);
            var template = Parse("{{ msg | json }} {{ mixed | json }}");
            var rendered = template.Render(ctx);
            var expectedMsg = JsonSerializer.Serialize("Hello SourceGen", FluidJsonContext.Default.String);
            var expectedMixed = JsonSerializer.Serialize(array, ctx.JsonSerializerOptions);
            Assert.Equal($"{expectedMsg} {expectedMixed}", rendered);
        }

        [Fact]
        public void JsonFilter_RespectsCamelCaseNamingPolicy_FromSourceGen()
        {
            var person = new SourceGenPerson
            {
                FirstName = "Jane",
                LastName = "Roe",
                Age = 30,
                HomeAddress = new SourceGenAddress { Street = "500 Market", City = "Gotham" },
                Tags = new List<string> { "x", "y" },
                Scores = new Dictionary<string, int> { ["logic"] = 88 }
            };

            var ctx = CreateContext(o =>
            {
                var genOptions = new JsonSerializerOptions
                {
                    TypeInfoResolver = JsonTypeInfoResolver.Combine(FluidJsonContext.Default, new DefaultJsonTypeInfoResolver()),
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                o.JsonSerializerOptions = genOptions;
            });

            ctx.Options.Filters.WithMiscFilters();
            ctx.SetValue("person", person);
            var template = Parse("{{ person | json }}");
            var output = template.Render(ctx);
            Assert.Contains("\"firstName\":\"Jane\"", output);
            Assert.Contains("\"homeAddress\":{\"street\":\"500 Market\",\"city\":\"Gotham\"}", output);
        }
    }
}
