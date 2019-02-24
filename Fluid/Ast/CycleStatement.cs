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

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var groupValue = Group == null ? "$defautGroup" : (await Group.EvaluateAsync(context)).ToStringValue();

            var currentValue = context.GetValue(groupValue);

            if (currentValue.IsNil())
            {
                currentValue = new NumberValue(0);
            }

            var index = (int)currentValue.ToNumberValue() % Values.Count;
            var value = await Values[index].EvaluateAsync(context);
            context.SetValue(groupValue, new NumberValue(index + 1));

            value.WriteTo(writer, encoder, context.CultureInfo);

            return Completion.Normal;
        }
    }
}
