using Fluid.Accessors;
using Fluid.Tests.Domain;
using Fluid.Values;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class MemberAccessStrategyTests
    {
        [Fact]
        public void RegisterByTypeAddPublicFields()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateField)));
        }

        [Fact]
        public void RegisterByTypeAddPublicProperties()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2)));

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateProperty)));
        }

        [Fact]
        public void RegisterByTypeAddAsyncPublicFields()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            var accessor = strategy.GetAccessor(typeof(Class1), nameof(Class1.Field3));
            Assert.NotNull(accessor);
            Assert.IsAssignableFrom<AsyncDelegateAccessor>(accessor);
        }

        [Fact]
        public void RegisterByTypeAddAsyncPublicProperties()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            var accessor = strategy.GetAccessor(typeof(Class1), nameof(Class1.Property3));
            Assert.NotNull(accessor);
            Assert.IsAssignableFrom<AsyncDelegateAccessor>(accessor);
        }

        [Fact]
        public void RegisterByTypeIgnoresStaticMembers()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.StaticField)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.StaticProperty)));
        }

        [Fact]
        public void RegisterByTypeIgnoresPrivateMembers()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateField)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateProperty)));
        }

        [Fact]
        public void RegisterByTypeAndName()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>(nameof(Class1.Field1), nameof(Class1.Property1));

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2)));
        }

        [Fact]
        public void RegisterByTypeAndExpression()
        {
            var strategy = new DefaultMemberAccessStrategy();

            strategy.Register<Class1>(x => x.Field1, x => x.Property1);

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2)));
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

            options.MemberAccessStrategy.Register<JObject, object>((o, name) => o[name]);
            options.ValueConverters.Add(x => x is JObject o ? new ObjectValue(o) : null);
            options.ValueConverters.Add(x => x is JValue o ? o.Value : null);

            var objectValue = FluidValue.Create(obj, options);

            Assert.Equal("1", (await objectValue.GetValueAsync("a", context)).ToObjectValue());
            Assert.Equal("2", (await objectValue.GetValueAsync("a.b", context)).ToObjectValue());
            Assert.Null((await objectValue.GetValueAsync("a.c", context)).ToObjectValue());
        }

        [Fact]
        public async Task ShouldRenderReadmeSample()
        {
            var options = new TemplateOptions();

            // When a property of a JObject value is accessed, try to look into its properties
            options.MemberAccessStrategy.Register<JObject, object>((source, name) => source[name]);

            // Convert JToken to FluidValue
            options.ValueConverters.Add(x => x is JObject o ? new ObjectValue(o) : null);
            options.ValueConverters.Add(x => x is JValue v ? v.Value : null);

            var model = JObject.Parse("{\"Name\": \"Bill\"}");

            var parser = new FluidParser();

            parser.TryParse("His name is {{ Name }}", out var template);
            var context = new TemplateContext(model, options);

            Assert.Equal("His name is Bill", await template.RenderAsync(context));
        }

        [Fact]
        public void SubPropertyShouldNotBeAccessible()
        {
            var parser = new FluidParser();
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Person>(x => x.Firstname);

            var john = new Person { Firstname = "John", Lastname = "Wick", Address = new Address { City = "Redmond", State = "Washington" } };

            var template = parser.Parse("{{Firstname}};{{Lastname}};{{Address.City}};{{Address.State}}");
            Assert.Equal("John;;;", template.Render(new TemplateContext(john, options, false)));
        }

        [Fact]
        public void SimblingPropertyShouldNotBeAccessible()
        {
            var parser = new FluidParser();
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Person>(x => x.Firstname);
            // Address is not registered
            options.MemberAccessStrategy.Register<Address>(x => x.State);

            var john = new Person { Firstname = "John", Lastname = "Wick", Address = new Address { City = "Redmond", State = "Washington" } };

            var template = parser.Parse("{{Firstname}};{{Lastname}};{{Address.City}};{{Address.State}}");
            Assert.Equal("John;;;", template.Render(new TemplateContext(john, options, false)));
        }

        [Fact]
        public void ShouldResolveModelProperty()
        {
            var parser = new FluidParser();
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Person>(x => x.Firstname);

            var john = new Person { Firstname = "John", Lastname = "Wick", Address = new Address { City = "Redmond", State = "Washington" } };

            var template = parser.Parse("{{Firstname}}{{Lastname}}");
            Assert.Equal("John", template.Render(new TemplateContext(john, options, false)));
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
    }
}
