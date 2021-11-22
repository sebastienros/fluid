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
        public ForStatement(
            List<Statement> statements,
            string identifier,
            MemberExpression member,
            Expression limit,
            Expression offset,
            bool reversed,
            ElseStatement elseStatement = null
        ) : base(statements)
        {
            Identifier = identifier;
            Member = member;
            Limit = limit;
            Offset = offset;
            Reversed = reversed;
            Else = elseStatement;
        }

        public ForStatement(
            List<Statement> statements,
            string identifier,
            RangeExpression range,
            Expression limit,
            Expression offset,
            bool reversed,
            ElseStatement elseStatement = null
        ) : base(statements)
        {
            Identifier = identifier;
            Range = range;
            Limit = limit;
            Offset = offset;
            Reversed = reversed;
            Else = elseStatement;
        }

        public string Identifier { get; }
        public RangeExpression Range { get; }
        public MemberExpression Member { get; }
        public Expression Limit { get; }
        public Expression Offset { get; }
        public bool Reversed { get; }
        public Statement Else { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            List<FluidValue> list = null;

            if (Member != null)
            {
                var member = await Member.EvaluateAsync(context);
                list = member.Enumerate(context).ToList();
            }
            else if (Range != null)
            {
                var start = Convert.ToInt32((await Range.From.EvaluateAsync(context)).ToNumberValue());
                var end = Convert.ToInt32((await Range.To.EvaluateAsync(context)).ToNumberValue());

                list = new List<FluidValue>(Math.Max(1, end - start));

                for (var i = start; i <= end; i++)
                {
                    list.Add(NumberValue.Create(i));
                }
            }

            if (list is null || list.Count == 0)
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
                var offset = (int) (await Offset.EvaluateAsync(context)).ToNumberValue();
                startIndex = offset;
            }

            var count = Math.Max(0, list.Count - startIndex);
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
                list.Reverse(startIndex, count);
            }

            try
            {
                var forloop = new ForLoopValue();

                var length = forloop.Length = startIndex + count;

                context.SetValue("forloop", forloop);

                for (var i = startIndex; i < length; i++)
                {
                    context.IncrementSteps();

                    var item = list[i];

                    context.SetValue(Identifier, item);

                    // Set helper variables
                    forloop.Index = i + 1;
                    forloop.Index0 = i;
                    forloop.RIndex = length - i - 1;
                    forloop.RIndex0 = length - i;
                    forloop.First = i == 0;
                    forloop.Last = i == length - 1;

                    Completion completion = Completion.Normal;

                    for (var index = 0; index < _statements.Count; index++)
                    {
                        var statement = _statements[index];
                        completion = await statement.WriteToAsync(writer, encoder, context);

                        // Restore the forloop property after every statement in case it replaced it,
                        // for instance if it contains a nested for loop
                        context.SetValue("forloop", forloop);

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
                context.LocalScope.Delete("forloop");
            }

            return Completion.Normal;
        }
    }
}