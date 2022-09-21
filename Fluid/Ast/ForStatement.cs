using Fluid.Values;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class ForStatement : TagStatement
    {
        private bool _isContinueOffset;
        private string _continueOffsetLiteral;

        public ForStatement(
            List<Statement> statements,
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

            _isContinueOffset = Offset is MemberExpression l && l.Segments.Count == 1 && ((IdentifierSegment)l.Segments[0]).Identifier == "continue";
            _continueOffsetLiteral = source is MemberExpression m ? "for_continue_" + ((IdentifierSegment)m.Segments[0]).Identifier : null;
        }

        public string Identifier { get; }
        public RangeExpression Range { get; }
        public Expression Source { get; }
        public Expression Limit { get; }
        public Expression Offset { get; }
        public bool Reversed { get; }
        public Statement Else { get; }

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
                if (_isContinueOffset)
                {
                    startIndex = (int) context.GetValue(_continueOffsetLiteral).ToNumberValue();
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
                var limit = (int) (await Limit.EvaluateAsync(context)).ToNumberValue();

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

                context.LocalScope._properties["forloop"] = forloop;

                if (!parentLoop.IsNil())
                {
                    context.LocalScope._properties["parentloop"] = parentLoop;
                }

                for (var i = startIndex; i < length; i++)
                {
                    context.IncrementSteps();

                    var item = source[i];

                    context.LocalScope._properties[Identifier] = item;

                    // Set helper variables
                    forloop.Index = i + 1;
                    forloop.Index0 = i;
                    forloop.RIndex = length - i - 1;
                    forloop.RIndex0 = length - i;
                    forloop.First = i == 0;
                    forloop.Last = i == length - 1;

                    if (_continueOffsetLiteral != null)
                    {
                        context.SetValue(_continueOffsetLiteral, forloop.Index);
                    }

                    Completion completion = Completion.Normal;

                    for (var index = 0; index < _statements.Count; index++)
                    {
                        var statement = _statements[index];
                        completion = await statement.WriteToAsync(writer, encoder, context);

                        //// Restore the forloop property after every statement in case it replaced it,
                        //// for instance if it contains a nested for loop
                        //context.LocalScope._properties["forloop"] = forloop;

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
    }
}