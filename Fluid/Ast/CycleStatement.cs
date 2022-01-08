using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    internal sealed class CycleStatement : Statement
    {
        private readonly List<Expression> _values;
        private readonly Expression _group;

        public CycleStatement(Expression group, params Expression[] values) : this(group, new List<Expression>(values))
        {
        }

        public CycleStatement(Expression group, List<Expression> values)
        {
            _group = group;
            _values = values;
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var groupValue = _group == null ? "$cycle_" : "$cycle_" + (await _group.EvaluateAsync(context)).ToStringValue();

            var currentValue = context.GetValue(groupValue);

            if (currentValue.IsNil())
            {
                currentValue = NumberValue.Zero;
            }

            var index = (int) currentValue.ToNumberValue() % _values.Count;
            var value = await _values[index].EvaluateAsync(context);
            context.SetValue(groupValue, NumberValue.Create(index + 1));

            value.WriteTo(writer, encoder, context.CultureInfo);

            return Completion.Normal;
        }
    }
}
