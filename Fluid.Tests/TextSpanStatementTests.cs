using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class TextSpanStatementTests
{
    private static readonly FluidParser _parser = new FluidParser();

    [Fact]
    public async Task TextIsWrittenWhenAFreshTemplateIsRenderedConcurrently()
    {
        // A text statement prepares its text on its first render: another thread rendering it at the same time must
        // never see it prepared without its text (which a weak memory model, such as ARM64's, allows without barriers).
        const int Threads = 8;
        var failures = 0;

        for (var iteration = 0; iteration < 2000 && failures == 0; iteration++)
        {
            var template = _parser.Parse($"<main>{{{{ content }}}}</main><footer>v{iteration} {{{{ section }}}}</footer>");
            var expected = $"<main>c</main><footer>v{iteration} s</footer>";
            using var start = new Barrier(Threads);

            var renders = Enumerable.Range(0, Threads).Select(_ => Task.Run(async () =>
            {
                var context = new TemplateContext();
                context.SetValue("content", "c");
                context.SetValue("section", "s");
                start.SignalAndWait();
                return await template.RenderAsync(context);
            })).ToArray();

            foreach (var rendered in await Task.WhenAll(renders))
            {
                if (rendered != expected)
                {
                    failures++;
                }
            }
        }

        Assert.Equal(0, failures);
    }
}
