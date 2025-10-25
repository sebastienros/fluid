using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Parser;
using Fluid.Tests.Domain;
using Xunit;

namespace Fluid.Tests
{
    public class StrictVariableTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public void StrictVariables_DefaultIsFalse()
        {
            // Verify TemplateOptions.StrictVariables defaults to false
            var options = new TemplateOptions();
            Assert.False(options.StrictVariables);
        }

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
        public async Task UndefinedSimpleVariable_ThrowsException()
        {
            _parser.TryParse("{{ nonExistingProperty }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("nonExistingProperty", exception.MissingVariables);
            Assert.Contains("nonExistingProperty", exception.Message);
        }

        [Fact]
        public async Task UndefinedPropertyAccess_ThrowsException()
        {
            _parser.TryParse("{{ user.nonExistingProperty }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            options.MemberAccessStrategy.Register<Person>();
            var context = new TemplateContext(options);
            context.SetValue("user", new Person { Firstname = "John" });

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("nonExistingProperty", exception.MissingVariables);
        }

        [Fact]
        public async Task MultipleMissingVariables_AllCollected()
        {
            _parser.TryParse("{{ var1 }} {{ var2 }} {{ var3 }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Equal(3, exception.MissingVariables.Count);
            Assert.Contains("var1", exception.MissingVariables);
            Assert.Contains("var2", exception.MissingVariables);
            Assert.Contains("var3", exception.MissingVariables);
        }

        [Fact]
        public async Task NestedMissingProperties_Tracked()
        {
            _parser.TryParse("{{ company.Director.Firstname }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            options.MemberAccessStrategy.Register<Company>();
            // Note: Not registering Employee type
            var context = new TemplateContext(options);
            context.SetValue("company", new Company { Director = new Employee { Firstname = "John" } });

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Single(exception.MissingVariables);
            Assert.Contains("Firstname", exception.MissingVariables);
        }

        [Fact]
        public async Task MixedValidAndInvalidVariables_OnlyInvalidTracked()
        {
            _parser.TryParse("{{ validVar }} {{ invalidVar }} {{ anotherValid }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);
            context.SetValue("validVar", "value1");
            context.SetValue("anotherValid", "value2");

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Single(exception.MissingVariables);
            Assert.Contains("invalidVar", exception.MissingVariables);
            Assert.DoesNotContain("validVar", exception.MissingVariables);
            Assert.DoesNotContain("anotherValid", exception.MissingVariables);
        }

        [Fact]
        public async Task NoExceptionWhenAllVariablesExist()
        {
            _parser.TryParse("{{ name }} {{ age }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);
            context.SetValue("name", "John");
            context.SetValue("age", 25);

            var result = await template.RenderAsync(context);
            Assert.Equal("John 25", result);
        }

        [Fact]
        public async Task StrictVariables_InIfConditions()
        {
            _parser.TryParse("{% if undefinedVar %}yes{% else %}no{% endif %}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("undefinedVar", exception.MissingVariables);
        }

        [Fact]
        public async Task StrictVariables_InForLoops()
        {
            _parser.TryParse("{% for item in undefinedCollection %}{{ item }}{% endfor %}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("undefinedCollection", exception.MissingVariables);
        }

        [Fact]
        public async Task DuplicateMissingVariables_ListedOnce()
        {
            _parser.TryParse("{{ missing }} {{ missing }} {{ missing }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Single(exception.MissingVariables);
            Assert.Contains("missing", exception.MissingVariables);
        }

        [Fact]
        public async Task StrictVariables_WithModelFallback()
        {
            _parser.TryParse("{{ existingModelProp }} {{ nonExistentModelProp }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var model = new { existingModelProp = "value" };
            var context = new TemplateContext(model, options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("nonExistentModelProp", exception.MissingVariables);
        }

        [Fact]
        public async Task StrictVariables_WithFilters()
        {
            _parser.TryParse("{{ undefinedVar | upcase }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("undefinedVar", exception.MissingVariables);
        }

        [Fact]
        public async Task ExceptionMessageFormat_IsCorrect()
        {
            _parser.TryParse("{{ var1 }} {{ var2 }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.StartsWith("The following variables were not found:", exception.Message);
            Assert.Contains("var1", exception.Message);
            Assert.Contains("var2", exception.Message);
        }

        [Fact]
        public async Task RegisteredProperties_DontThrow()
        {
            _parser.TryParse("{{ person.Firstname }} {{ person.Lastname }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            options.MemberAccessStrategy.Register<Person>();
            var context = new TemplateContext(options);
            context.SetValue("person", new Person { Firstname = "John", Lastname = "Doe" });

            var result = await template.RenderAsync(context);
            Assert.Equal("John Doe", result);
        }

        [Fact]
        public async Task StrictVariables_WithAssignment()
        {
            _parser.TryParse("{% assign x = undefinedVar %}{{ x }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("undefinedVar", exception.MissingVariables);
        }

        [Fact]
        public async Task StrictVariables_WithCase()
        {
            _parser.TryParse("{% case undefinedVar %}{% when 1 %}one{% endcase %}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("undefinedVar", exception.MissingVariables);
        }

        [Fact]
        public async Task StrictVariables_EmptyStringNotMissing()
        {
            _parser.TryParse("{{ emptyString }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);
            context.SetValue("emptyString", "");

            var result = await template.RenderAsync(context);
            Assert.Equal("", result);
        }

        [Fact]
        public async Task StrictVariables_NullValueNotMissing()
        {
            _parser.TryParse("{{ nullValue }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);
            context.SetValue("nullValue", (object)null);

            var result = await template.RenderAsync(context);
            Assert.Equal("", result);
        }

        [Fact]
        public async Task StrictVariables_WithBinaryExpression()
        {
            _parser.TryParse("{% if undefinedVar > 5 %}yes{% endif %}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("undefinedVar", exception.MissingVariables);
        }

        [Fact]
        public async Task StrictVariables_MultipleRenders_ClearsTracking()
        {
            _parser.TryParse("{{ missing }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            // First render should throw
            await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());

            // Now set the variable
            context.SetValue("missing", "value");

            // Second render should succeed
            var result = await template.RenderAsync(context);
            Assert.Equal("value", result);
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

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var products = new[]
            {
                new { name = "Product 1", price = 10 },
                new { name = "Product 2", price = 20 }
            };
            context.SetValue("products", products);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("stock", exception.MissingVariables);
        }

        [Fact]
        public async Task StrictVariables_WithElseIf()
        {
            _parser.TryParse("{% if false %}no{% elsif undefined %}maybe{% else %}yes{% endif %}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("undefined", exception.MissingVariables);
        }

        [Fact]
        public async Task StrictVariables_NoOutputWhenException()
        {
            _parser.TryParse("Start {{ missing }} End", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            // Should throw exception and produce no output
            await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
        }

        [Fact]
        public async Task StrictVariables_WithRange()
        {
            _parser.TryParse("{% for i in (1..5) %}{{ i }}{% endfor %}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            // Should work fine - no missing variables
            var result = await template.RenderAsync(context);
            Assert.Equal("12345", result);
        }

        [Fact]
        public async Task StrictVariables_WithCapture()
        {
            _parser.TryParse("{% capture foo %}{{ bar }}{% endcapture %}{{ foo }}", out var template, out var _);

            var options = new TemplateOptions { StrictVariables = true };
            var context = new TemplateContext(options);

            var exception = await Assert.ThrowsAsync<StrictVariableException>(() => template.RenderAsync(context).AsTask());
            Assert.Contains("bar", exception.MissingVariables);
        }
    }
}
