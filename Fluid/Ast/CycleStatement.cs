using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;

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

        public override Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var groupValue = Group == null ? "$defautGroup" : Group.Evaluate(context).ToStringValue();

            var currentValue = context.GetValue(groupValue);

            if (currentValue.IsUndefined())
            {
                currentValue = new NumberValue(0);
            }

            var index = (int)currentValue.ToNumberValue() % Values.Count;
            var value = Values[index].Evaluate(context);
            context.SetValue(groupValue, new NumberValue(index + 1));

            value.WriteTo(writer, encoder);

            return Task.FromResult(Completion.Normal);
        }
    }
}
