using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class CycleStatement : Statement
    {
        private readonly Expression[] _values;

        public CycleStatement(Expression group, Expression[] values)
        {
            Group = group;
            _values = values;
        }

        public CycleStatement(Expression group, IList<Expression> values)
        {
            Group = group;
            _values = values.ToArray();
        }

        public Expression Group { get; }
        public IList<Expression> Values2 { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var groupValue = Group == null ? "$cycle_" : "$cycle_" + (await Group.EvaluateAsync(context)).ToStringValue();

            var currentValue = context.GetValue(groupValue);

            if (currentValue.IsNil())
            {
                currentValue = NumberValue.Zero;
            }

            var index = (uint) currentValue.ToNumberValue() % _values.Length;
            var value = await _values[index].EvaluateAsync(context);
            context.SetValue(groupValue, NumberValue.Create(index + 1));

            value.WriteTo(writer, encoder, context.CultureInfo);

            return Completion.Normal;
        }
    }
}
