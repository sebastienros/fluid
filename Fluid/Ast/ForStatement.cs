using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;

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

        private List<FluidValue> _rangeElements;
        private int _rangeStart, _rangeEnd;

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            List<FluidValue> list = null;

            if (Member != null)
            {
                var member = await Member.EvaluateAsync(context);
                list = member.ToList();
            }
            else if (Range != null)
            {
                int start = Convert.ToInt32((await Range.From.EvaluateAsync(context)).ToNumberValue());
                int end = Convert.ToInt32((await Range.To.EvaluateAsync(context)).ToNumberValue());

                // Cache range
                if (_rangeElements == null || _rangeStart != start || _rangeEnd != end)
                {
                    _rangeElements = new List<FluidValue>(end - start);

                    for (var i = start; i <= end; i++)
                    {
                        _rangeElements.Add(NumberValue.Create(i));
                    }

                    list = _rangeElements;
                    _rangeStart = start;
                    _rangeEnd = end;
                }
                else
                {
                    list = _rangeElements;
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
                count = Math.Min(count, limit);
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

        private sealed class ForLoopValue : FluidValue
        {
            public int Length { get; set; }
            public int Index { get; set; }
            public int Index0 { get; set; }
            public int RIndex { get; set; }
            public int RIndex0 { get; set; }
            public bool First { get; set; }
            public bool Last { get; set; }

            public int Count => Length;

            public override FluidValues Type => FluidValues.Dictionary;

            public override bool Equals(FluidValue other)
            {
                return false;
            }

            public override bool ToBooleanValue()
            {
                return false;
            }

            public override decimal ToNumberValue()
            {
                return Length;
            }

            public override object ToObjectValue()
            {
                return null;
            }

            public override string ToStringValue()
            {
                return "forloop";
            }

            public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
            {
                return name switch
                {
                    "length" => new ValueTask<FluidValue>(NumberValue.Create(Length)),
                    "index" => new ValueTask<FluidValue>(NumberValue.Create(Index)),
                    "index0" => new ValueTask<FluidValue>(NumberValue.Create(Index0)),
                    "rindex" => new ValueTask<FluidValue>(NumberValue.Create(RIndex)),
                    "rindex0" => new ValueTask<FluidValue>(NumberValue.Create(RIndex0)),
                    "first" => new ValueTask<FluidValue>(BooleanValue.Create(First)),
                    "last" => new ValueTask<FluidValue>(BooleanValue.Create(Last)),
                    _ => new ValueTask<FluidValue>(NilValue.Instance),
                };
            }

            public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
            {
            }
        }
    }
}