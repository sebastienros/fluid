﻿using Fluid.Values;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class CycleStatement : Statement
    {
        public IReadOnlyList<Expression> Values;

        public Expression Group { get; }

        public CycleStatement(Expression group, IReadOnlyList<Expression> values)
        {
            Group = group;
            Values = values;
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var groupValue = Group == null ? "$cycle_" : "$cycle_" + (await Group.EvaluateAsync(context)).ToStringValue();

            var currentValue = context.GetValue(groupValue);

            if (currentValue.IsNil())
            {
                currentValue = NumberValue.Zero;
            }

            var index = (uint)currentValue.ToNumberValue() % Values.Count;
            var value = await Values[(int)index].EvaluateAsync(context);
            context.SetValue(groupValue, NumberValue.Create(index + 1));

            await value.WriteToAsync(writer, encoder, context.CultureInfo);

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCycleStatement(this);
    }
}
