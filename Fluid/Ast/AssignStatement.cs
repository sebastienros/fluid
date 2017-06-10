using System;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class AssignStatement : Statement
    {
        public AssignStatement(string identifier, Expression value)
        {
            Identifier = identifier;
            Value = value;
        }

        public string Identifier { get; }
        public Expression Value { get; }

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var value = Value.Evaluate(context);
            context.SetValue(Identifier, value);

            return Completion.Normal;
        }
    }
}
