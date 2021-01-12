using Fluid.Ast;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Parser
{
    public class IdentifierTagStatement : Statement
    {
        private readonly Func<IdentifierTagStatement, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public IdentifierTagStatement(string identifier, Func<IdentifierTagStatement, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            _render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public string Identifier { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(this, writer, encoder, context);
        }
    }
}
