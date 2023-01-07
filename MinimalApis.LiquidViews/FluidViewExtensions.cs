using Fluid.Utils;
using Microsoft.AspNetCore.Http;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid
{
    public static class FluidViewExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task RenderAsync(this IFluidTemplate template, HttpResponse response, TemplateContext context, TextEncoder encoder)
        {
            var textWriter = Utf8BufferTextWriter.Get(response.BodyWriter);
            try
            {
                await template.RenderAsync(textWriter, encoder, context);
                await textWriter.FlushAsync();
            }
            finally
            {
                Utf8BufferTextWriter.Return(textWriter);
            }
        }        
    }
}
