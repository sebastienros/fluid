using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Tests.Domain;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

public class StrictVariableTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    [Fact]
    public async Task StrictVariables_DefaultBehaviorNoException()
    {
        // Verify missing variables don't throw by default
        _parser.TryParse("{{ nonExistent }}", out var template, out var _);
        var context = new TemplateContext();
        var result = await template.RenderAsync(context);
        Assert.Equal("", result);
    }

    [Fact]
    public async Task StrictVariables_ThrowsOnMissingVariable()
    {
        _parser.TryParse("{{ missing }}", out var template, out var _);
        var options = new TemplateOptions { StrictVariables = true };
        var context = new TemplateContext(options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => template.RenderAsync(context).AsTask());
    }

    [Fact]
    public async Task StrictVariables_DoesNotThrowWhenVariableExists()
    {
        _parser.TryParse("{{ existing }}", out var template, out var _);
        var options = new TemplateOptions { StrictVariables = true };
        var context = new TemplateContext(options);
        context.SetValue("existing", "value");
        var result = await template.RenderAsync(context);
        Assert.Equal("value", result);
    }

    [Fact]
    public async Task UndefinedSimpleVariable_IsDetected()
    {
        _parser.TryParse("{{ nonExistingProperty }}", out var template, out var _);

        var options = new TemplateOptions();
        var context = new TemplateContext(options);
        var detected = false;
        context.Undefined = (path) =>
        {
            Assert.Equal("nonExistingProperty", path);
            detected = true;
            return ValueTask.FromResult<FluidValue>(NilValue.Instance);
        };

        var result = await template.RenderAsync(context);
        Assert.Equal("", result);
        Assert.True(detected);
    }

    [Fact]
    public async Task UndefinedPropertyAccess_TracksMissingVariable()
    {
        _parser.TryParse("{{ user.nonExistingProperty }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);
        context.SetValue("user", new Person { Firstname = "John" });

        await template.RenderAsync(context);
        Assert.Contains("nonExistingProperty", missingVariables);
    }

    [Fact]
    public async Task MultipleMissingVariables_AllCollected()
    {
        _parser.TryParse("{{ var1 }} {{ var2 }} {{ var3 }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Equal(3, missingVariables.Count);
        Assert.Contains("var1", missingVariables);
        Assert.Contains("var2", missingVariables);
        Assert.Contains("var3", missingVariables);
    }

    [Fact]
    public async Task MissingSubProperties_Tracked()
    {
        // 'Occupation' is not defined on Employee 
        _parser.TryParse("{{ company.Director.Occupation }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        // Note: Not registering Employee type
        var context = new TemplateContext(options);
        context.SetValue("company", new Company { Director = new Employee { Firstname = "John" } });

        await template.RenderAsync(context);
        Assert.Single(missingVariables);
        Assert.Contains("Occupation", missingVariables);
    }

    [Fact]
    public async Task NestedMissingProperties_Tracked()
    {
        // 'Occupation' is not defined on Employee 
        _parser.TryParse("{{ company['Director.Occupation'] }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        // Note: Not registering Employee type
        var context = new TemplateContext(options);
        context.SetValue("company", new Company { Director = new Employee { Firstname = "John" } });

        await template.RenderAsync(context);
        Assert.Single(missingVariables);
        Assert.Contains("Director.Occupation", missingVariables);
    }

    [Fact]
    public async Task MixedValidAndInvalidVariables_OnlyInvalidTracked()
    {
        _parser.TryParse("{{ validVar }} {{ invalidVar }} {{ anotherValid }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);
        context.SetValue("validVar", "value1");
        context.SetValue("anotherValid", "value2");

        await template.RenderAsync(context);
        Assert.Single(missingVariables);
        Assert.Contains("invalidVar", missingVariables);
        Assert.DoesNotContain("validVar", missingVariables);
        Assert.DoesNotContain("anotherValid", missingVariables);
    }

    [Fact]
    public async Task NoExceptionWhenAllVariablesExist()
    {
        _parser.TryParse("{{ name }} {{ age }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);
        context.SetValue("name", "John");
        context.SetValue("age", 25);

        var result = await template.RenderAsync(context);
        Assert.Equal("John 25", result);
        Assert.Empty(missingVariables);
    }

    [Fact]
    public async Task StrictVariables_InIfConditions()
    {
        _parser.TryParse("{% if undefinedVar %}yes{% else %}no{% endif %}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Contains("undefinedVar", missingVariables);
    }

    [Fact]
    public async Task StrictVariables_InForLoops()
    {
        _parser.TryParse("{% for item in undefinedCollection %}{{ item }}{% endfor %}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Contains("undefinedCollection", missingVariables);
    }

    [Fact]
    public async Task DuplicateMissingVariables_ListedAsManyTimes()
    {
        _parser.TryParse("{{ missing }} {{ missing }} {{ missing }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Equal(3, missingVariables.Count);
        Assert.All(missingVariables, item => Assert.Equal("missing", item));
    }

    [Fact]
    public async Task StrictVariables_WithModelFallback()
    {
        _parser.TryParse("{{ existingModelProp }} {{ nonExistentModelProp }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var model = new { existingModelProp = "value" };
        var context = new TemplateContext(model, options);

        await template.RenderAsync(context);
        Assert.Contains("nonExistentModelProp", missingVariables);
    }

    [Fact]
    public async Task StrictVariables_WithFilters()
    {
        _parser.TryParse("{{ undefinedVar | upcase }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Contains("undefinedVar", missingVariables);
    }

    [Fact]
    public async Task MissingVariablesFormat_IsCorrect()
    {
        _parser.TryParse("{{ var1 }} {{ var2 }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Contains("var1", missingVariables);
        Assert.Contains("var2", missingVariables);
    }

    [Fact]
    public async Task RegisteredProperties_DontThrow()
    {
        _parser.TryParse("{{ person.Firstname }} {{ person.Lastname }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);
        context.SetValue("person", new Person { Firstname = "John", Lastname = "Doe" });

        var result = await template.RenderAsync(context);
        Assert.Equal("John Doe", result);
        Assert.Empty(missingVariables);
    }

    [Fact]
    public async Task StrictVariables_WithAssignment()
    {
        _parser.TryParse("{% assign x = undefinedVar %}{{ x }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Contains("undefinedVar", missingVariables);
    }

    [Fact]
    public async Task StrictVariables_WithCase()
    {
        _parser.TryParse("{% case undefinedVar %}{% when 1 %}one{% endcase %}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Contains("undefinedVar", missingVariables);
    }

    [Fact]
    public async Task StrictVariables_EmptyStringNotMissing()
    {
        _parser.TryParse("{{ emptyString }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);
        context.SetValue("emptyString", "");

        var result = await template.RenderAsync(context);
        Assert.Equal("", result);
        Assert.Empty(missingVariables);
    }

    [Fact]
    public async Task StrictVariables_NullValueNotMissing()
    {
        _parser.TryParse("{{ nullValue }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);
        context.SetValue("nullValue", (object)null);

        var result = await template.RenderAsync(context);
        Assert.Equal("", result);
        Assert.Empty(missingVariables);
    }

    [Fact]
    public async Task StrictVariables_NullMemberNotMissing()
    {
        _parser.TryParse("{{ person.Firstname }} {{ person.Lastname }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);
        context.SetValue("person", new Person { Firstname = null, Lastname = "Doe" });

        var result = await template.RenderAsync(context);
        Assert.Equal(" Doe", result);
        Assert.Empty(missingVariables);
    }

    [Fact]
    public async Task StrictVariables_WithBinaryExpression()
    {
        _parser.TryParse("{% if undefinedVar > 5 %}yes{% endif %}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Contains("undefinedVar", missingVariables);
    }

    [Fact]
    public async Task StrictVariables_MultipleRenders_ClearsTracking()
    {
        _parser.TryParse("{{ missing }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        // First render should track missing variable
        await template.RenderAsync(context);
        Assert.Contains("missing", missingVariables);

        // Clear and set the variable
        missingVariables.Clear();
        context.SetValue("missing", "value");

        // Second render should succeed with no missing variables
        var result = await template.RenderAsync(context);
        Assert.Equal("value", result);
        Assert.Empty(missingVariables);
    }

    [Fact]
    public async Task StrictVariables_ComplexTemplate()
    {
        var source = @"
            {% for product in products %}
                Name: {{ product.name }}
                Price: {{ product.price }}
                Stock: {{ product.stock }}
            {% endfor %}
        ";

        _parser.TryParse(source, out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        var products = new[]
        {
            new { name = "Product 1", price = 10 },
            new { name = "Product 2", price = 20 }
        };
        context.SetValue("products", products);

        await template.RenderAsync(context);
        Assert.Contains("stock", missingVariables);
    }

    [Fact]
    public async Task StrictVariables_WithElseIf()
    {
        _parser.TryParse("{% if false %}no{% elsif undefined %}maybe{% else %}yes{% endif %}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Contains("undefined", missingVariables);
    }

    [Fact]
    public async Task StrictVariables_ProducesOutputWithMissing()
    {
        _parser.TryParse("Start {{ missing }} End", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        // Should track missing variable and produce output
        var result = await template.RenderAsync(context);
        Assert.Equal("Start  End", result);
        Assert.Contains("missing", missingVariables);
    }

    [Fact]
    public async Task StrictVariables_WithRange()
    {
        _parser.TryParse("{% for i in (1..5) %}{{ i }}{% endfor %}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        // Should work fine - no missing variables
        var result = await template.RenderAsync(context);
        Assert.Equal("12345", result);
        Assert.Empty(missingVariables);
    }

    [Fact]
    public async Task StrictVariables_WithCapture()
    {
        _parser.TryParse("{% capture foo %}{{ bar }}{% endcapture %}{{ foo }}", out var template, out var _);

        var (options, missingVariables) = CreateStrictOptions();
        var context = new TemplateContext(options);

        await template.RenderAsync(context);
        Assert.Contains("bar", missingVariables);
    }

    [Fact]
    public async Task UndefinedDelegate_ReceivesNotifications()
    {
        _parser.TryParse("{{ first }} {{ first }} {{ second }}", out var template, out var _);

        var paths = new List<string>();
        var options = new TemplateOptions
        {
            Undefined = name =>
            {
                paths.Add(name);
                return ValueTask.FromResult<FluidValue>(NilValue.Instance);
            }
        };

        var context = new TemplateContext(options);

        var result = await template.RenderAsync(context);
        Assert.True(string.IsNullOrWhiteSpace(result));

        Assert.Equal(3, paths.Count);
        Assert.Equal("first", paths[0]);
        Assert.Equal("first", paths[1]);
        Assert.Equal("second", paths[2]);
    }

    [Fact]
    public async Task UndefinedDelegate_CalledForMissingVariable()
    {
        _parser.TryParse("{{ missing }}", out var template, out var _);

        var paths = new List<string>();
        var options = new TemplateOptions
        {
            Undefined = name =>
            {
                paths.Add(name);
                return ValueTask.FromResult<FluidValue>(NilValue.Instance);
            }
        };

        var context = new TemplateContext(options);

        await template.RenderAsync(context);

        Assert.Single(paths);
        Assert.Equal("missing", paths[0]);
    }

    [Fact]
    public async Task UndefinedDelegate_CanReturnCustomValue()
    {
        _parser.TryParse("{{ missing }} {{ another }}", out var template, out var _);

        var options = new TemplateOptions
        {
            Undefined = name =>
            {
                // Return a custom default value for undefined variables
                return ValueTask.FromResult<FluidValue>(new StringValue($"[{name} not found]"));
            }
        };

        var context = new TemplateContext(options);

        var result = await template.RenderAsync(context);
        Assert.Equal("[missing not found] [another not found]", result);
    }

    private (TemplateOptions, List<string>) CreateStrictOptions()
    {
        var missingVariables = new List<string>();

        var options = new TemplateOptions
        {
            Undefined = name =>
            {
                missingVariables.Add(name);
                return ValueTask.FromResult<FluidValue>(NilValue.Instance);
            }
        };

        return (options, missingVariables);
    }
}

