﻿using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class BreakStatement : Statement
    {
        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Break();
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitBreakStatement(this);
    }
}
