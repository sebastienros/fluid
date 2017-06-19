using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    /// <summary>
    /// An instance of this class is used to execute some custom code in a template.
    /// </summary>
    public class CallbackStatement : Statement
    {
        public CallbackStatement(Func<TextWriter, TextEncoder, TemplateContext, Task<Completion>> action)
        {
            Action = action;
        }

        public Func<TextWriter, TextEncoder, TemplateContext, Task<Completion>> Action { get; }

        public override Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Action?.Invoke(writer, encoder, context);
        }
    }
}
