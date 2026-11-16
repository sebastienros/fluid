using System.Runtime.CompilerServices;
using Fluid.Values;

namespace Fluid.Ast
{
    public abstract class BinaryExpression : Expression
    {
        protected BinaryExpression(Expression left, Expression right)
        {
            Left = left;
            Right = right;
            _nested = left is BinaryExpression or FilterExpression || right is BinaryExpression or FilterExpression;
        }

        // Whether evaluating an operand recurses into another expression that can nest, see EvaluateAsync.
        private readonly bool _nested;

        public Expression Left { get; }

        public Expression Right { get; }

        /// <summary>
        /// Evaluates two operands and tries to avoid state machines.
        /// </summary>
        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            if (_nested)
            {
                // A long chain of operators fails with an exception that can be handled instead of a stack overflow.
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }

            var leftTask = Left.EvaluateAsync(context);
            var rightTask = Right.EvaluateAsync(context);

            if (leftTask.IsCompletedSuccessfully && rightTask.IsCompletedSuccessfully)
            {
                return Evaluate(leftTask.Result, rightTask.Result);
            }

            return Awaited(leftTask, rightTask);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async ValueTask<FluidValue> Awaited(
            ValueTask<FluidValue> leftTask,
            ValueTask<FluidValue> rightTask)
        {
            var leftValue = await leftTask;
            var rightValue = await rightTask;

            return Evaluate(leftValue, rightValue);
        }

        // sub-classes using the default implementation need to override this
        internal virtual FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue)
        {
            throw new NotImplementedException();
        }
    }
}