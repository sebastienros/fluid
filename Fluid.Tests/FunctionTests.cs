using Fluid.Values;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class FunctionTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true }).Compile();
#else
        private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });
#endif

        [Fact]
        public async Task FunctionCallsShouldDefaultToNil()
        {
            var source = @"
                {{- a() -}}
            ";

            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext();
            var result = await template.RenderAsync(context);
            Assert.Equal("", result);
        }

        [Fact]
        public async Task FunctionCallsAreInvoked()
        {
            var source = @"{{ a() | append: b()}}";

            _parser.TryParse(source, out var template, out var error);
            Assert.True(template != null, error);
            var context = new TemplateContext();
            context.SetValue("a", new FunctionValue((args, c) => new StringValue("hello")));
            context.SetValue("b", new FunctionValue((args, c) => new ValueTask<FluidValue>(new StringValue("world"))));

            // Use a loop to exercise the arguments cache
            for (var i = 0; i < 10; i++)
            {
                var result = await template.RenderAsync(context);
                Assert.Equal("helloworld", result);
            }
        }

        [Fact]
        public async Task FunctionCallsUseArguments()
        {
            var source = @"{{ a('x', 2) }}";

            _parser.TryParse(source, out var template, out var error);
            Assert.True(template != null, error);
            var context = new TemplateContext();
            context.SetValue("a", new FunctionValue((args, c) => new StringValue(new String(args.At(0).ToStringValue()[0], (int) args.At(1).ToNumberValue()))));

            // Use a loop to exercise the arguments cache
            for (var i = 0; i < 10; i++)
            {
                var result = await template.RenderAsync(context);
                Assert.Equal("xx", result);
            }
        }

        [Fact]
        public async Task FunctionCallsUseNamedArguments()
        {
            var source = @"{{ a(c = 'x', r = 2) }}";

            _parser.TryParse(source, out var template, out var error);
            Assert.True(template != null, error);
            var context = new TemplateContext();
            context.SetValue("a", new FunctionValue((args, c) => new StringValue(new String(args["c"].ToStringValue()[0], (int)args["r"].ToNumberValue()))));

            // Use a loop to exercise the arguments cache
            for (var i = 0; i < 10; i++)
            {
                var result = await template.RenderAsync(context);
                Assert.Equal("xx", result);
            }
        }

        [Fact]
        public async Task FunctionCallsRecursively()
        {
            var source = @"{{ a(b(), r = 2) }}";

            _parser.TryParse(source, out var template, out var error);
            Assert.True(template != null, error);
            var context = new TemplateContext();
            context.SetValue("a", new FunctionValue((args, c) => new StringValue(new String(args.At(0).ToStringValue()[0], (int)args["r"].ToNumberValue()))));
            context.SetValue("b", new FunctionValue((args, c) => new StringValue("hello")));

            // Use a loop to exercise the arguments cache
            for (var i = 0; i < 10; i++)
            {
                var result = await template.RenderAsync(context);
                Assert.Equal("hh", result);
            }
        }

        [Fact]
        public async Task ShouldDefineMacro()
        {
            var source = @"
                {%- macro hello(first, last='Smith') -%}
                Hello {{first | capitalize }} {{last}}
                {%- endmacro -%}

                {{- hello('mike') }} {{ hello(last= 'ros', first='sebastien') -}}
            ";

            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext();
            var result = await template.RenderAsync(context);
            Assert.Equal("Hello Mike Smith Hello Sebastien ros", result);
        }
    }
}