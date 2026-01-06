using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class CycleStatement : Statement, ISourceable
    {
        public IReadOnlyList<Expression> Values;

        public Expression Group { get; }

        public CycleStatement(Expression group, IReadOnlyList<Expression> values)
        {
            Group = group;
            Values = values;
        }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
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

            await value.WriteToAsync(output, encoder, context.CultureInfo);

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCycleStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            context.WriteLine($"{context.ContextName}.IncrementSteps();");

            if (Group is null)
            {
                context.WriteLine("var groupValue = \"$cycle_\";");
            }
            else
            {
                var groupExpr = context.GetExpressionMethodName(Group);
                context.WriteLine($"var groupValue = \"$cycle_\" + (await {groupExpr}({context.ContextName})).ToStringValue();");
            }

            context.WriteLine($"var currentValue = {context.ContextName}.GetValue(groupValue);");
            context.WriteLine("if (currentValue.IsNil()) currentValue = NumberValue.Zero;");
            context.WriteLine($"var index = (uint)currentValue.ToNumberValue() % {Values.Count};");

            // Values[index]
            context.WriteLine("FluidValue value;");
            context.WriteLine("switch ((int)index)");
            context.WriteLine("{");
            using (context.Indent())
            {
                for (var i = 0; i < Values.Count; i++)
                {
                    var vExpr = context.GetExpressionMethodName(Values[i]);
                    context.WriteLine($"case {i}: value = await {vExpr}({context.ContextName}); break;");
                }
                context.WriteLine("default: value = NilValue.Instance; break;");
            }
            context.WriteLine("}");

            context.WriteLine($"{context.ContextName}.SetValue(groupValue, NumberValue.Create(index + 1));");
            context.WriteLine($"await value.WriteToAsync({context.WriterName}, {context.EncoderName}, {context.ContextName}.CultureInfo);");
            context.WriteLine("return Completion.Normal;");
        }
    }
}
