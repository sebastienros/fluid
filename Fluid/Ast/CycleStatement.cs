using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public class CycleStatement : Statement
    {
        public CycleStatement(Expression group, IList<Expression> values)
        {
            Group = group;
            Values = values;
        }

        public Expression Group { get; }
        public IList<Expression> Values { get; }

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var groupValue = Group == null ? "$defautGroup" : Group.Evaluate(context).ToStringValue();

            var currentValue = context.Scope.GetProperty(groupValue);

            if (currentValue.IsUndefined())
            {
                currentValue = new NumberValue(0);
            }

            var index = (int)currentValue.ToNumberValue() % Values.Count;
            var value = Values[index].Evaluate(context);
            context.Scope.SetProperty(groupValue, new NumberValue(index + 1));

            value.WriteTo(writer, encoder);

            return Completion.Normal;
        }
    }
}
