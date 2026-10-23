using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class FilterExpressionTests
{
    private static readonly FluidParser _parser = new FluidParser();

    [Fact]
    public async Task ArgumentsThatAreNotAllLiteralsAreNotCachedWhenAFreshTemplateIsRenderedConcurrently()
    {
        // A filter caches its arguments on its first evaluation when they are all literals. The first renders of a
        // template on several threads at once must never cache arguments that are not: every render would then use
        // the values of the render that cached them.
        const int Threads = 8;
        var failures = 0;

        for (var iteration = 0; iteration < 2000 && failures == 0; iteration++)
        {
            var template = _parser.Parse($"{{{{ 'abcdefghij' | slice: 0, n }}}} v{iteration}");
            using var start = new Barrier(Threads);

            var renders = Enumerable.Range(0, Threads).Select(n => Task.Run(async () =>
            {
                var context = new TemplateContext();
                context.SetValue("n", n + 1);
                start.SignalAndWait();
                return (Expected: $"{"abcdefghij"[..(n + 1)]} v{iteration}", Actual: await template.RenderAsync(context));
            })).ToArray();

            foreach (var (expected, actual) in await Task.WhenAll(renders))
            {
                if (actual != expected)
                {
                    failures++;
                }
            }
        }

        Assert.Equal(0, failures);
    }
}
