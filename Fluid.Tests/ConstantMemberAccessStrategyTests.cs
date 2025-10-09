using System;
using System.Threading.Tasks;
using Fluid;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class SampleModel
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string City { get; set; } = "";
    }

    public class ConstantMemberAccessStrategy : DefaultMemberAccessStrategy
    {
        private readonly string _interceptProperty;
        private readonly FluidValue _constantValue;

        public ConstantMemberAccessStrategy(string interceptProperty, object constantReturnValue)
        {
            _interceptProperty = interceptProperty;
            _constantValue = FluidValue.Create(constantReturnValue, TemplateOptions.Default);
            MemberAccessStrategyExtensions.Register<SampleModel>(this);
        }

        public override IMemberAccessor GetAccessor(Type type, string name)
        {
            // If this is the property we want to intercept, return our custom accessor
            if (type == typeof(SampleModel) && string.Equals(name, _interceptProperty, StringComparison.OrdinalIgnoreCase))
            {
                return new Accessors.DelegateAccessor((obj, propertyName) => _constantValue.ToObjectValue());
            }

            // Otherwise, use the default behavior
            return base.GetAccessor(type, name);
        }
    }

    public class ConstantMemberAccessStrategyTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public async Task ShouldReturnConstantValueForInterceptedProperty()
        {
            var model = new SampleModel
            {
                Name = "John",
                Age = 30,
                City = "Seattle"
            };

            var strategy = new ConstantMemberAccessStrategy("Name", "ConstantName");
            var options = new TemplateOptions { MemberAccessStrategy = strategy };

            _parser.TryParse("{{ Name }}", out var template, out var error);
            var context = new TemplateContext(model, options);

            var result = await template.RenderAsync(context);
            Assert.Equal("ConstantName", result);
        }

        [Fact]
        public async Task ShouldReturnRealValueForNonInterceptedProperty()
        {
            var model = new SampleModel
            {
                Name = "John",
                Age = 30,
                City = "Seattle"
            };

            var strategy = new ConstantMemberAccessStrategy("Name", "ConstantName");
            var options = new TemplateOptions { MemberAccessStrategy = strategy };

            _parser.TryParse("{{ Age }}", out var template, out var error);
            var context = new TemplateContext(model, options);

            var result = await template.RenderAsync(context);
            Assert.Equal("30", result);
        }

        [Fact]
        public async Task ShouldHandleMultiplePropertiesInTemplate()
        {
            var model = new SampleModel
            {
                Name = "John",
                Age = 30,
                City = "Seattle"
            };

            var strategy = new ConstantMemberAccessStrategy("Name", "ConstantName");
            var options = new TemplateOptions { MemberAccessStrategy = strategy };

            _parser.TryParse("{{ Name }}, {{ Age }}, {{ City }}", out var template, out var error);
            var context = new TemplateContext(model, options);

            var result = await template.RenderAsync(context);
            Assert.Equal("ConstantName, 30, Seattle", result);
        }

        [Fact]
        public async Task ShouldInterceptDifferentProperty()
        {
            var model = new SampleModel
            {
                Name = "John",
                Age = 30,
                City = "Seattle"
            };

            var strategy = new ConstantMemberAccessStrategy("City", "ConstantCity");
            var options = new TemplateOptions { MemberAccessStrategy = strategy };

            _parser.TryParse("{{ Name }}, {{ Age }}, {{ City }}", out var template, out var error);
            var context = new TemplateContext(model, options);

            var result = await template.RenderAsync(context);
            Assert.Equal("John, 30, ConstantCity", result);
        }

        [Fact]
        public async Task ShouldInterceptNumericProperty()
        {
            var model = new SampleModel
            {
                Name = "John",
                Age = 30,
                City = "Seattle"
            };

            var strategy = new ConstantMemberAccessStrategy("Age", 99);
            var options = new TemplateOptions { MemberAccessStrategy = strategy };

            _parser.TryParse("{{ Name }}, {{ Age }}, {{ City }}", out var template, out var error);
            var context = new TemplateContext(model, options);

            var result = await template.RenderAsync(context);
            Assert.Equal("John, 99, Seattle", result);
        }
    }
}
