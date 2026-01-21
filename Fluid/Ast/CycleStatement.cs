using Fluid.Values;
using System.Text.Encodings.Web;
using System.Runtime.CompilerServices;

namespace Fluid.Ast
{
    public sealed class CycleStatement : Statement
    {
        private const string CycleRegisterKey = "$$cycle$$";

        public IReadOnlyList<Expression> Values;

        public Expression Group { get; }

        private readonly string _unnamedKey;

        public CycleStatement(Expression group, IReadOnlyList<Expression> values)
        {
            Group = group;
            Values = values;

            if (Group is null)
            {
                // Shopify Liquid quirk: unnamed cycles that include lookups have independent counters per call site,
                // even if they evaluate to the same values. Literal-only unnamed cycles share counters.
                var hasLookup = Values.Any(static e => e is MemberExpression);

                _unnamedKey = hasLookup
                    ? "cycle_stmt_" + RuntimeHelpers.GetHashCode(this)
                    : "cycle_" + string.Join(",", Values.Select(static e => e.ToString()));
            }
        }

        public override bool IsWhitespaceOrCommentOnly => true;

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var key = await GetKeyAsync(context);

            if (!context.AmbientValues.TryGetValue(CycleRegisterKey, out var registerObj) || registerObj is not Dictionary<string, int> register)
            {
                register = new Dictionary<string, int>();
                context.AmbientValues[CycleRegisterKey] = register;
            }

            register.TryGetValue(key, out var iteration);

            // Shopify Liquid: do not modulo the index. If the counter is larger than the number of values,
            // it evaluates to nil (renders nothing) and then the counter is reset.
            FluidValue value;

            if ((uint)iteration < (uint)Values.Count)
            {
                value = await Values[iteration].EvaluateAsync(context);
            }
            else
            {
                value = NilValue.Instance;
            }

            await value.WriteToAsync(output, encoder, context.CultureInfo);

            iteration++;
            if (Values.Count == 0 || iteration >= Values.Count)
            {
                iteration = 0;
            }

            register[key] = iteration;

            return Completion.Normal;
        }

        private async ValueTask<string> GetKeyAsync(TemplateContext context)
        {
            if (Group is null)
            {
                return _unnamedKey ?? "cycle_";
            }

            var groupValue = await Group.EvaluateAsync(context);
            // Ruby Liquid uses the evaluated object as a hash key (nil is valid). We approximate this with a string key.
            return "named_" + (groupValue.IsNil() ? string.Empty : groupValue.ToStringValue());
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCycleStatement(this);
    }
}
