using Fluid.Accessors;
using Fluid.Tests.Domain;
using Fluid.Values;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class MemberAccessStrategyTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public void RegisterByTypeAddPublicFields()
        {
            var strategy = new DefaultMemberAccessStrategy();

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1), StringComparer.Ordinal));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2), StringComparer.Ordinal));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateField), StringComparer.Ordinal));
        }

        [Fact]
        public void RegisterByTypeAddPublicProperties()
        {
            var strategy = new DefaultMemberAccessStrategy();

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1), StringComparer.Ordinal));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2), StringComparer.Ordinal));

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateProperty), StringComparer.Ordinal));
        }

        [Fact]
        public void RegisterByTypeAddAsyncPublicFields()
        {
            var strategy = new DefaultMemberAccessStrategy();

            var accessor = strategy.GetAccessor(typeof(Class1), nameof(Class1.Field3), StringComparer.Ordinal);
            Assert.NotNull(accessor);
            Assert.IsType<AsyncDelegateAccessor>(accessor, exactMatch: false);
        }

        [Fact]
        public void RegisterByTypeAddAsyncPublicProperties()
        {
            var strategy = new DefaultMemberAccessStrategy();

            var accessor = strategy.GetAccessor(typeof(Class1), nameof(Class1.Property3), StringComparer.Ordinal);
            Assert.NotNull(accessor);
            Assert.IsType<AsyncDelegateAccessor>(accessor, exactMatch: false);
        }

        [Fact]
        public void RegisterByTypeIgnoresStaticMembers()
        {
            var strategy = new DefaultMemberAccessStrategy();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.StaticField), StringComparer.Ordinal));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.StaticProperty), StringComparer.Ordinal));
        }

        [Fact]
        public void RegisterByTypeIgnoresPrivateMembers()
        {
            var strategy = new DefaultMemberAccessStrategy();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateField), StringComparer.Ordinal));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateProperty), StringComparer.Ordinal));
        }

        [Fact]
        public async Task ShouldResolvePropertiesWithDots()
        {
            var obj = new JObject(
                new JProperty("a", "1"),
                new JProperty("a.b", "2")
            );

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            var objectValue = FluidValue.Create(obj, options);

            Assert.Equal("1", (await objectValue.GetValueAsync("a", context)).ToObjectValue());
            Assert.Equal("2", (await objectValue.GetValueAsync("a.b", context)).ToObjectValue());
            Assert.Null((await objectValue.GetValueAsync("a.c", context)).ToObjectValue());
        }

        [Fact]
        public async Task ShouldNotBreakJObjectCustomizations()
        {
            var options = new TemplateOptions();

            // When a property of a JObject value is accessed, try to look into its properties
            options.MemberAccessStrategy.Register<JObject, object>((source, name) => source[name]);

            // Convert JToken to FluidValue
            options.ValueConverters.Add(x => x is JObject o ? new ObjectValue(o) : null);
            options.ValueConverters.Add(x => x is JValue v ? v.Value : null);

            var model = JObject.Parse("{\"Name\": \"Bill\",\"Company\":{\"Name\":\"Microsoft\"}}");

            _parser.TryParse("His name is {{ Name }}, Company : {{ Company.Name }}", out var template);
            var context = new TemplateContext(model, options);

            Assert.Equal("His name is Bill, Company : Microsoft", await template.RenderAsync(context));
        }

        [Fact]
        public async Task ShouldRenderReadmeSample()
        {
            var options = new TemplateOptions();

            options.MemberAccessStrategy.Register<Person, object>((p, name) => p.Firstname);
            var model = new Person { Firstname = "Bill" };

            _parser.TryParse("His name is {{ Something }}", out var template);
            var context = new TemplateContext(model, options);

            Assert.Equal("His name is Bill", await template.RenderAsync(context));
        }

        [Fact]
        public async Task ShouldAccessJObject()
        {
            var options = new TemplateOptions();

            var model = JObject.Parse("{\"Name\": \"Bill\",\"Company\":{\"Name\":\"Microsoft\"}}");

            _parser.TryParse("His name is {{ Name }}, Company : {{ Company | json }}", out var template);
            var context = new TemplateContext(model, options);

            Assert.Equal("His name is Bill, Company : {\"Name\":\"Microsoft\"}", await template.RenderAsync(context));
        }

        [Fact]
        public void ShouldResolveModelProperty()
        {
            var options = new TemplateOptions();

            var john = new Person { Firstname = "John", Lastname = "Wick", Address = new Address { City = "Redmond", State = "Washington" } };

            var template = _parser.Parse("{{Firstname}} {{Lastname}}");
            Assert.Equal("John Wick", template.Render(new TemplateContext(john, options)));
        }

        [Fact]
        public void ShouldSkipWriteOnlyProperty()
        {
            var strategy = new DefaultMemberAccessStrategy();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.WriteOnlyProperty), StringComparer.Ordinal));
        }

        [Fact]
        public void ShouldUseDictionaryAsModel()
        {
            var options = new TemplateOptions();

            var model = new Dictionary<string, object>
            {
                { "Firstname", "Bill" },
                { "Lastname", "Gates" }
            };

            var template = _parser.Parse("{{Firstname}} {{Lastname}}");
            
            Assert.Equal("Bill Gates", template.Render(new TemplateContext(model, options)));
        }

        [Fact]
        public void ShouldResolveEnums()
        {
            var options = new TemplateOptions();

            var john = new Person { Firstname = "John", EyesColor = Colors.Yellow };

            var template = _parser.Parse("{{Firstname}} {{EyesColor}}");
            Assert.Equal("John Yellow", template.Render(new TemplateContext(john, options)));
        }

        [Fact]
        public void ShouldResolveStructs()
        {
            var options = new TemplateOptions();

            var circle = new Shape
            {
                Coordinates = new Point(1, 2)
            };

            var template = _parser.Parse("{{Coordinates.X}} {{Coordinates.Y}}");
            Assert.Equal("1 2", template.Render(new TemplateContext(circle, options)));
        }

        [Fact]
        public void ShouldFindBackingFields()
        {
            var options = new TemplateOptions();

            var s = new CustomStruct
            {
                X1 = 1,
                X2 = 2,
                X3 = 3
            };

            var template = _parser.Parse("{{X1}} {{X2}} {{X3}}");
            Assert.Equal("1 2 3", template.Render(new TemplateContext(s, options)));
        }
    }

    public class Class1
    {
        public static string StaticField;
        public static string StaticProperty { get; set; }
        internal string PrivateField = null;
        internal string PrivateProperty { get; set; }
        public string Field1;
        public int Field2;
        public Task<string> Field3;
        public string Property1 { get; set; }
        public int Property2 { get; set; }
        public Task<string> Property3 { get; set; }
        public string WriteOnlyProperty { private get; set; }
    }
}
