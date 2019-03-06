using Xunit;

namespace Fluid.Tests
{
    public class MemberAccessStrategyTests
    {
        [Fact]
        public void RegisterByTypeAddPublicFields()
        {
            var strategy = new MemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateField)));
        }

        [Fact]
        public void RegisterByTypeAddPublicProperties()
        {
            var strategy = new MemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2)));

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateProperty)));
        }

        [Fact]
        public void RegisterByTypeIgnoresStaticMembers()
        {
            var strategy = new MemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.StaticField)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.StaticProperty)));
        }

        [Fact]
        public void RegisterByTypeIgnoresPrivateMembers()
        {
            var strategy = new MemberAccessStrategy();

            strategy.Register<Class1>();

            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateField)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.PrivateProperty)));
        }

        [Fact]
        public void RegisterByTypeAndName()
        {
            var strategy = new MemberAccessStrategy();

            strategy.Register<Class1>(nameof(Class1.Field1), nameof(Class1.Property1));

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2)));
        }

        [Fact]
        public void RegisterByTypeAndExpression()
        {
            var strategy = new MemberAccessStrategy();

            strategy.Register<Class1>(x => x.Field1, x => x.Property1);

            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Field2)));
            Assert.NotNull(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property1)));
            Assert.Null(strategy.GetAccessor(typeof(Class1), nameof(Class1.Property2)));
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
        public string Property1 { get; set; }
        public int Property2 { get; set; }
    }
}
