using Fluid.Values;
using System.Runtime.CompilerServices;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class StartsWithBinaryExpression : BinaryExpression
    {
        public StartsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public static ValueTask<FluidValue> StartsWithAsync(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            if (leftValue is ArrayValue)
            {
                var first = leftValue.GetValueAsync("first", context);

                if (first.IsCompletedSuccessfully)
                {
                    return first.Equals(rightValue)
                            ? BooleanValue.True
                            : BooleanValue.False;
                }
                else
                {
                    return Awaited(first, rightValue);

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
                return leftValue.ToStringValue().StartsWith(rightValue.ToStringValue())
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

            return StartsWithAsync(leftValue.Result, rightValue.Result, context);

            [MethodImpl(MethodImplOptions.NoInlining)]
            static async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> leftTask, ValueTask<FluidValue> rightTask, TemplateContext context)
            {
                var leftValue = await leftTask;
                var rightValue = await rightTask;
                return await StartsWithAsync(leftValue, rightValue, context);
            }
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitStartsWithBinaryExpression(this);
    }
}
