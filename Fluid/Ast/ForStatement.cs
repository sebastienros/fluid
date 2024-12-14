using Fluid.Values;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class ForStatement : TagStatement
    {
        private readonly string _continueOffsetLiteral;

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
            _continueOffsetLiteral = source is MemberExpression m ? "for_continue_" + ((IdentifierSegment)m.Segments[0]).Identifier : null;
        }

        public string Identifier { get; }
        public RangeExpression Range { get; }
        public Expression Source { get; }
        public Expression Limit { get; }
        public Expression Offset { get; }
        public bool Reversed { get; }
        public ElseStatement Else { get; }
        public bool OffsetIsContinue { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var source = (await Source.EvaluateAsync(context)).Enumerate(context).ToList();

            if (source.Count == 0)
            {
                if (Else != null)
                {
                    await Else.WriteToAsync(writer, encoder, context);
                }

                return Completion.Normal;
            }

            // Apply options
            var startIndex = 0;
            if (Offset is not null)
            {
                if (OffsetIsContinue)
                {
                    startIndex = (int)context.GetValue(_continueOffsetLiteral).ToNumberValue();
                }
                else
                {
                    var offset = (int)(await Offset.EvaluateAsync(context)).ToNumberValue();
                    startIndex = offset;
                }
            }

            var count = Math.Max(0, source.Count - startIndex);

            if (Limit is not null)
            {
                var limit = (int)(await Limit.EvaluateAsync(context)).ToNumberValue();

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
                    await Else.WriteToAsync(writer, encoder, context);
                }

                return Completion.Normal;
            }

            if (Reversed)
            {
                source.Reverse(startIndex, count);
            }

            var parentLoop = context.LocalScope.GetValue("forloop");

            context.EnterForLoopScope();

            try
            {
                var forloop = new ForLoopValue();

                var length = forloop.Length = startIndex + count;

                context.LocalScope.SetOwnValue("forloop", forloop);

                if (!parentLoop.IsNil())
                {
                    context.LocalScope.SetOwnValue("parentloop", parentLoop);
                }

                for (var i = startIndex; i < length; i++)
                {
                    context.IncrementSteps();

                    var item = source[i];

                    context.LocalScope.SetOwnValue(Identifier, item);

                    // Set helper variables
                    forloop.Index = i + 1;
                    forloop.Index0 = i;
                    forloop.RIndex = length - i;
                    forloop.RIndex0 = length - i - 1;
                    forloop.First = i == 0;
                    forloop.Last = i == length - 1;

                    if (_continueOffsetLiteral != null)
                    {
                        context.SetValue(_continueOffsetLiteral, forloop.Index);
                    }

                    var completion = Completion.Normal;

                    for (var index = 0; index < Statements.Count; index++)
                    {
                        var statement = Statements[index];
                        completion = await statement.WriteToAsync(writer, encoder, context);

                        //// Restore the forloop property after every statement in case it replaced it,
                        //// for instance if it contains a nested for loop
                        //context.LocalScope.SetOwnValue("forloop", forloop);

                        if (completion != Completion.Normal)
                        {
                            // Stop processing the block statements
                            break;
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
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitForStatement(this);
    }
}