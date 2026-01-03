using Fluid.Values;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class ForStatement : TagStatement
    {
        private readonly string _continueSourceLiteral;

        public ForStatement(
            IReadOnlyList<Statement> statements,
            string identifier,
            Expression source,
            Expression limit,
            Expression offset,
            bool reversed,
            ElseStatement elseStatement = null
        ) : base(statements)
        {
            Identifier = identifier;
            Source = source;
            Limit = limit;
            Offset = offset;
            Reversed = reversed;
            Else = elseStatement;

            OffsetIsContinue = Offset is MemberExpression l && l.Segments.Count == 1 && ((IdentifierSegment)l.Segments[0]).Identifier == "continue";
            _continueSourceLiteral = source is MemberExpression m
                ? string.Join(".", m.Segments.Select(s => (s as IdentifierSegment)?.Identifier).Where(s => s != null))
                : null;
        }

        public string Identifier { get; }
        public RangeExpression Range { get; }
        public Expression Source { get; }
        public Expression Limit { get; }
        public Expression Offset { get; }
        public bool Reversed { get; }
        public ElseStatement Else { get; }
        public bool OffsetIsContinue { get; }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            var evaluatedSource = await Source.EvaluateAsync(context);

            // The `offset: continue` feature uses a per-source key to store the absolute index
            // of the next item to render. In Liquid, this state is updated by every `for` loop
            // over the same source, even if the loop itself doesn't specify `offset: continue`.
            var continueOffsetLiteral = await BuildContinueOffsetLiteralAsync(context);

            // Golden Liquid: empty strings are treated as empty collections
            if (evaluatedSource.Type == FluidValues.String && string.IsNullOrEmpty(evaluatedSource.ToStringValue()))
            {
                if (Else != null)
                {
                    await Else.WriteToAsync(output, encoder, context);
                }

                return Completion.Normal;
            }

            // Fast-path: FluidValue.Create(IEnumerable) and many array-like values already materialize as ArrayValue.
            // Avoid re-enumerating and allocating a new List<T> in this very hot path.
            IReadOnlyList<FluidValue> source = evaluatedSource is ArrayValue array
                ? array.Values
                : await evaluatedSource.EnumerateAsync(context).ToListAsync();

            var suppressWhitespaceBody = Statements.Count == 1
                && Statements[0] is TextSpanStatement t
#if NET6_0_OR_GREATER
                && t.Text.Span.IsWhiteSpace();
#else
                && string.IsNullOrWhiteSpace(t.Text.ToString());
#endif

            if (source.Count == 0)
            {
                if (Else != null)
                {
                    await Else.WriteToAsync(output, encoder, context);
                }

                return Completion.Normal;
            }

            // Apply options
            var startIndex = 0;
            if (Offset is not null)
            {
                if (OffsetIsContinue)
                {
                    startIndex = continueOffsetLiteral is null
                        ? 0
                        : (int)context.GetValue(continueOffsetLiteral).ToNumberValue();
                }
                else
                {
                    startIndex = await EvaluateIntegerArgumentAsync("offset", Offset, context);
                }
            }

            var count = Math.Max(0, source.Count - startIndex);

            if (Limit is not null)
            {
                var limit = await EvaluateIntegerArgumentAsync("limit", Limit, context);

                // Limit can be negative
                if (limit >= 0)
                {
                    count = Math.Min(count, limit);
                }
                else
                {
                    count = Math.Max(0, count + limit);
                }
            }

            if (count == 0)
            {
                if (Else != null)
                {
                    await Else.WriteToAsync(output, encoder, context);
                }

                return Completion.Normal;
            }

            var parentLoop = context.LocalScope.GetValue("forloop");

            context.EnterForLoopScope();

            try
            {
                var endIndexExclusive = startIndex + count;

                var forloop = new ForLoopValue
                {
                    Identifier = Identifier,
                    Source = Source is MemberExpression m
                        ? string.Join(".", m.Segments.Select(s => (s as IdentifierSegment)?.Identifier).Where(s => s != null))
                        : Source is RangeExpression r
                            ? $"({Convert.ToInt32((await r.From.EvaluateAsync(context)).ToNumberValue())}..{Convert.ToInt32((await r.To.EvaluateAsync(context)).ToNumberValue())})"
                            : null
                };

                forloop.Length = count;

                context.LocalScope.SetOwnValue("forloop", forloop);

                // Render tag forloops should not be accessible as parentloop from nested for loops
                // (render creates an isolated scope where parent relationships don't cross boundaries)
                if (!parentLoop.IsNil() && parentLoop is ForLoopValue parentForLoop && !parentForLoop.IsRenderLoop)
                {
                    forloop.ParentLoop = parentForLoop;

                    // Legacy Liquid compatibility: expose the parent loop as `parentloop`
                    context.LocalScope.SetOwnValue("parentloop", parentForLoop);
                }

                for (var iteration = 0; iteration < count; iteration++)
                {
                    context.IncrementSteps();

                    // When reversed, iterate the slice in reverse without mutating the underlying list.
                    var itemIndex = Reversed ? endIndexExclusive - 1 - iteration : startIndex + iteration;
                    var item = source[itemIndex];

                    context.LocalScope.SetOwnValue(Identifier, item);

                    // Set helper variables
                    forloop.Index = iteration + 1;
                    forloop.Index0 = iteration;
                    forloop.RIndex = count - iteration;
                    forloop.RIndex0 = count - iteration - 1;
                    forloop.First = iteration == 0;
                    forloop.Last = iteration == count - 1;

                    var completion = Completion.Normal;

                    if (!suppressWhitespaceBody)
                    {
                        for (var index = 0; index < Statements.Count; index++)
                        {
                            var statement = Statements[index];
                            completion = await statement.WriteToAsync(output, encoder, context);

                        //// Restore the forloop property after every statement in case it replaced it,
                        //// for instance if it contains a nested for loop
                        //context.LocalScope.SetOwnValue("forloop", forloop);

                            if (completion != Completion.Normal)
                            {
                                // Stop processing the block statements
                                break;
                            }
                        }
                    }

                    if (completion == Completion.Continue)
                    {
                        // Go to next iteration
                        continue;
                    }

                    if (completion == Completion.Break)
                    {
                        // Leave the loop
                        break;
                    }
                }

                // Persist the continue cursor to the end of the intended slice, regardless of early break.
                if (continueOffsetLiteral != null)
                {
                    context.SetValue(continueOffsetLiteral, endIndexExclusive);
                }
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        private async ValueTask<string> BuildContinueOffsetLiteralAsync(TemplateContext context)
        {
            // Key is based on Liquid's `forloop.name`: "{identifier}-{source}".
            // This means changing the loop variable changes the key.

            string source;

            if (!string.IsNullOrEmpty(_continueSourceLiteral))
            {
                source = _continueSourceLiteral;
            }
            else if (Source is RangeExpression r)
            {
                var from = Convert.ToInt32((await r.From.EvaluateAsync(context)).ToNumberValue());
                var to = Convert.ToInt32((await r.To.EvaluateAsync(context)).ToNumberValue());
                source = $"({from}..{to})";
            }
            else
            {
                // Fallback: stable within the current render (statement instance).
                source = "stmt_" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this).ToString(CultureInfo.InvariantCulture);
            }

            return $"for_continue_{Identifier}-{source}";
        }

        private static async ValueTask<int> EvaluateIntegerArgumentAsync(string name, Expression expression, TemplateContext context)
        {
            var value = await expression.EvaluateAsync(context);

            if (value.Type == FluidValues.Number)
            {
                return Convert.ToInt32(value.ToNumberValue());
            }

            if (value.Type == FluidValues.String)
            {
                var s = value.ToStringValue().Trim();
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }

                throw new LiquidException($"for: {name} is not a number");
            }

            throw new LiquidException($"for: {name} must be a number");
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitForStatement(this);
    }
}