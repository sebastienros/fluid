﻿using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    internal sealed class EmptyBlockStatement : Statement
    {
        private readonly Func<IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public EmptyBlockStatement(IReadOnlyList<Statement> statements, Func<IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            _render = render ?? throw new ArgumentNullException(nameof(render));
            Statements = statements ?? [];
        }

        public IReadOnlyList<Statement> Statements { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(Statements, writer, encoder, context);
        }
    }
}
