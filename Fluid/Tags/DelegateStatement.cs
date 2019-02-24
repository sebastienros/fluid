using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;

namespace Fluid.Tags
{
    public class DelegateStatement : Statement
    {
        private readonly Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _writeAsync;

        public DelegateStatement(Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> writeAsync)
        {
            _writeAsync = writeAsync;
        }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _writeAsync(writer, encoder, context);
        }
    }
}
