using Fluid.Ast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Parser
{
    internal sealed class ParserBlockStatement<T> : TagStatement, IHasTagName, IHasValue<T>
    {
        private readonly Func<T, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public ParserBlockStatement(string tagName, T value, List<Statement> statements, Func<T, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render) : base(statements)
        {
            Value = value;
            TagName = tagName;
            _render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public T Value { get; }
        public string TagName { get; init; }
        object IHasValue.Value => Value;

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(Value, Statements, writer, encoder, context);
        }
    }
}
