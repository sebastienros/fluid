using Fluid.Values;
using System.Runtime.CompilerServices;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class EndsWithBinaryExpression : BinaryExpression
    {
        public EndsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public static ValueTask<FluidValue> EndsWithAsync(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            if (leftValue is ArrayValue)
            {
                var last = leftValue.GetValueAsync("last", context);

                if (last.IsCompletedSuccessfully)
                {
                    return last.Equals(rightValue)
                            ? BooleanValue.True
                            : BooleanValue.False;
                }
                else
                {
                    return Awaited(last, rightValue);

                    [MethodImpl(MethodImplOptions.NoInlining)]
                    static async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> first, FluidValue rightValue)
                    {
                        return (await first).Equals(rightValue)
                            ? BooleanValue.True
                            : BooleanValue.False;
                    }
                }

            }
            else
            {
                return leftValue.ToStringValue().EndsWith(rightValue.ToStringValue())
                        ? BooleanValue.True
                        : BooleanValue.False;
            }
        }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = Left.EvaluateAsync(context);
            var rightValue = Right.EvaluateAsync(context);

            if (!leftValue.IsCompletedSuccessfully || !rightValue.IsCompletedSuccessfully)
            {
                return Awaited(leftValue, rightValue, context);
            }

            return EndsWithAsync(leftValue.Result, rightValue.Result, context);

            [MethodImpl(MethodImplOptions.NoInlining)]
            static async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> leftTask, ValueTask<FluidValue> rightTask, TemplateContext context)
            {
                var leftValue = await leftTask;
                var rightValue = await rightTask;
                return await EndsWithAsync(leftValue, rightValue, context);
            }
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitEndsWithBinaryExpression(this);
    }
}
