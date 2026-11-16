using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    // The json filter must only write the members a template can read through the member access strategy.
    public class JsonMemberAccessTests
    {
        private static readonly FluidParser _parser = new FluidParser();

        private static string Render(string source, TemplateOptions options, System.Action<TemplateContext> setup)
        {
            Assert.True(_parser.TryParse(source, out var template, out var errors), errors);
            var context = new TemplateContext(options);
            setup(context);
            return template.Render(context);
        }

        [Fact]
        public void JsonOnlyWritesRegisteredMembers()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Customer>("Name");

            var output = Render("{{ c.ApiKey }}|{{ c | json }}", options, c => c.SetValue("c", new Customer()));

            Assert.Equal("|{\"Name\":\"Alice\"}", output);
        }

        [Fact]
        public void JsonOnlyWritesRegisteredMembersOfNestedObjects()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Customer>("Name", "Address");
            options.MemberAccessStrategy.Register<Address>("City");

            var output = Render("{{ c | json }}", options, c => c.SetValue("c", new Customer()));

            Assert.Equal("{\"Name\":\"Alice\",\"Address\":{\"City\":\"Paris\"}}", output);
        }

        [Fact]
        public void JsonDoesNotWriteObjectsOfUnregisteredTypes()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Customer>("Name", "Address");

            var output = Render("{{ c | json }}|{{ c.Address | json }}", options, c => c.SetValue("c", new Customer()));

            Assert.Equal("{\"Name\":\"Alice\",\"Address\":{}}|{}", output);
        }

        [Fact]
        public void JsonOnlyWritesRegisteredMembersOfArrayAndDictionaryItems()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Customer>("Name");

            var output = Render("{{ list | json }}|{{ dict | json }}", options, c =>
            {
                c.SetValue("list", new[] { new Customer() });
                c.SetValue("dict", new Dictionary<string, object> { ["customer"] = new Customer() });
            });

            Assert.Equal("[{\"Name\":\"Alice\"}]|{\"customer\":{\"Name\":\"Alice\"}}", output);
        }

        [Fact]
        public void JsonUsesRegisteredAccessors()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Customer>("Name");
            options.MemberAccessStrategy.Register<Customer, string>("ApiKey", c => "***");

            var output = Render("{{ c.ApiKey }}|{{ c | json }}", options, c => c.SetValue("c", new Customer()));

            Assert.Equal("***|{\"Name\":\"Alice\",\"ApiKey\":\"***\"}", output);
        }

        [Fact]
        public void JsonHidesMembersOfWildcardAccessor()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Customer, FluidValue>((obj, name, context) => name switch
            {
                nameof(Customer.Name) => new StringValue(obj.Name),
                _ => NilValue.Instance
            });

            var output = Render("{{ c | json }}", options, c => c.SetValue("c", new Customer()));

            Assert.Equal("{\"Name\":\"Alice\",\"ApiKey\":null,\"Address\":null}", output);
        }

        [Fact]
        public void JsonUsesMemberNameStrategy()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
            options.MemberAccessStrategy.Register<Customer>("name");

            var output = Render("{{ c.name }}|{{ c | json }}", options, c => c.SetValue("c", new Customer()));

            Assert.Equal("Alice|{\"Name\":\"Alice\"}", output);
        }

        [Fact]
        public void JsonWritesModelMembers()
        {
            var options = new TemplateOptions();

            Assert.True(_parser.TryParse("{{ Name }}|{{ Address | json }}", out var template, out var errors), errors);

            Assert.Equal("Alice|{}", template.Render(new TemplateContext(new Customer(), options)));
        }

        [Fact]
        public void JsonUsesNamingPolicyAndIgnoredMembers()
        {
            var options = new TemplateOptions
            {
                JsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                }
            };
            options.MemberAccessStrategy.Register<IgnoringCustomer>();

            var output = Render("{{ c | json }}", options, c => c.SetValue("c", new IgnoringCustomer { Address = null }));

            Assert.Equal("{\"name\":\"Alice\"}", output);
        }

        [Fact]
        public void JsonDetectsCircularReferences()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Node>();

            var node = new Node();
            node.Next = node;

            Assert.ThrowsAny<JsonException>(() => Render("{{ n | json }}", options, c => c.SetValue("n", node)));
        }

        public class Customer
        {
            public string Name { get; set; } = "Alice";
            public string ApiKey { get; set; } = "APIKEY-xyz";
            public Address Address { get; set; } = new();
        }

        public class IgnoringCustomer
        {
            public string Name { get; set; } = "Alice";
            [JsonIgnore]
            public string ApiKey { get; set; } = "APIKEY-xyz";
            public Address Address { get; set; } = new();
        }

        public class Address
        {
            public string Street { get; set; } = "Secret St 1";
            public string City { get; set; } = "Paris";
        }

        public class Node
        {
            public Node Next { get; set; }
        }
    }
}
