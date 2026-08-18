using Fluid.Ast;
using Fluid.Parser;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid
{
    internal static class FluidTemplateRenderer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask RenderAsync(
            IFluidTemplate template,
            IFluidOutput output,
            TextEncoder encoder,
            TemplateContext context)
        {
            if (template is FluidTemplate fluidTemplate)
            {
                return fluidTemplate.RenderInternalAsync(output, encoder, context);
            }

            if (template is CompositeFluidTemplate compositeTemplate)
            {
                return compositeTemplate.RenderInternalAsync(output, encoder, context);
            }

            return template.RenderAsync(output, encoder, context);
        }

        public static ValueTask<Completion> RenderWithCompletionAsync(
            IFluidTemplate template,
            IFluidOutput output,
            TextEncoder encoder,
            TemplateContext context)
        {
            if (template is IStatementList statementList)
            {
                return statementList.Statements.RenderStatementsAsync(output, encoder, context);
            }

            return Awaited(template.RenderAsync(output, encoder, context));

            static async ValueTask<Completion> Awaited(ValueTask task)
            {
                await task;
                return Completion.Normal;
            }
        }
    }
}
