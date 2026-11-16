using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

// The json filter must only write the members a template can read through the member access strategy.
public class JsonMemberAccessTests
{
    private static readonly FluidParser _parser = new FluidParser();

    private static string Render(string source, TemplateOptions options, Action<TemplateContext> setup)
    {
        Assert.True(_parser.TryParse(source, out var template, out var errors), errors);
        var context = new TemplateContext(options);
        setup(context);
        return template.Render(context);
    }

    private static TemplateOptions AllowList(params string[] members)
        => new TemplateOptionsBuilder().WithMemberAccessStrategy(() => new AllowListStrategy(members)).Build();

    [Fact]
    public void JsonOnlyWritesAllowedMembers()
    {
        var options = AllowList("Customer.Name");

        var output = Render("{{ c.ApiKey }}|{{ c | json }}", options, c => c.SetValue("c", new Customer()));

        Assert.Equal("|{\"Name\":\"Alice\"}", output);
    }

    [Fact]
    public void JsonOnlyWritesAllowedMembersOfNestedObjects()
    {
        var options = AllowList("Customer.Name", "Customer.Address", "Address.City");

        var output = Render("{{ c | json }}", options, c => c.SetValue("c", new Customer()));

        Assert.Equal("{\"Name\":\"Alice\",\"Address\":{\"City\":\"Paris\"}}", output);
    }

    [Fact]
    public void JsonDoesNotWriteObjectsOfUnregisteredTypes()
    {
        var options = AllowList("Customer.Name", "Customer.Address");

        var output = Render("{{ c | json }}|{{ c.Address | json }}", options, c => c.SetValue("c", new Customer()));

        Assert.Equal("{\"Name\":\"Alice\",\"Address\":{}}|{}", output);
    }

    [Fact]
    public void JsonOnlyWritesAllowedMembersOfArrayAndDictionaryItems()
    {
        var options = AllowList("Customer.Name");

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
        var options = new TemplateOptionsBuilder()
            .ConfigureMemberAccess(s => s.Register<Customer, object>("ApiKey", (c, ctx) => "***"))
            .Build();

        var output = Render("{{ c.ApiKey }}|{{ c | json }}", options, c => c.SetValue("c", new Customer()));

        Assert.DoesNotContain("APIKEY", output);
        Assert.Equal("***|{\"Name\":\"Alice\",\"ApiKey\":\"***\",\"Address\":{\"Street\":\"Secret St 1\",\"City\":\"Paris\"}}", output);
    }

    [Fact]
    public void JsonHidesMembersOfWildcardAccessor()
    {
        var options = new TemplateOptionsBuilder()
            .ConfigureMemberAccess(s => s.Register<Customer, FluidValue>((obj, name, context) => name switch
            {
                nameof(Customer.Name) => StringValue.Create(obj.Name),
                _ => NilValue.Instance
            }))
            .Build();

        var output = Render("{{ c | json }}", options, c => c.SetValue("c", new Customer()));

        Assert.Equal("{\"Name\":\"Alice\",\"ApiKey\":null,\"Address\":null}", output);
    }

    [Fact]
    public void JsonUsesNamingPolicyAndIgnoredMembers()
    {
        var options = new TemplateOptionsBuilder()
            .WithMemberAccessStrategy(() => new AllowListStrategy("IgnoringCustomer.Name", "IgnoringCustomer.ApiKey", "IgnoringCustomer.Address"))
            .WithJsonSerializerOptions(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            })
            .Build();

        var output = Render("{{ c | json }}", options, c => c.SetValue("c", new IgnoringCustomer { Address = null }));

        Assert.Equal("{\"name\":\"Alice\"}", output);
    }

    [Fact]
    public void JsonDetectsCircularReferences()
    {
        var node = new Node();
        node.Next = node;

        Assert.ThrowsAny<JsonException>(() => Render("{{ n | json }}", TemplateOptions.Default, c => c.SetValue("n", node)));
    }

    private sealed class AllowListStrategy : DefaultMemberAccessStrategy
    {
        private readonly HashSet<string> _members;

        public AllowListStrategy(params string[] members) => _members = new HashSet<string>(members);

        public override MemberAccessor GetAccessor(Type type, string name, StringComparer stringComparer)
            => _members.Contains(type.Name + "." + name) ? base.GetAccessor(type, name, stringComparer) : null;
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
