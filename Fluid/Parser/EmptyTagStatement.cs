using Fluid.Ast;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Parser
{
    internal sealed class EmptyTagStatement : Statement, IHasTagName
    {
        private readonly Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public EmptyTagStatement(string tagName, Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            _render = render ?? throw new ArgumentNullException(nameof(render));
            TagName = tagName;
        }

        public string TagName { get; init; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(writer, encoder, context);
        }
    }
}
