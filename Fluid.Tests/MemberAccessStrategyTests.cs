using Fluid.Accessors;
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
