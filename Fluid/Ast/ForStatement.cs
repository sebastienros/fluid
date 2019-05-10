using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
            // Selz: Support expression for limit and offset instead of just number
            Expression limit,
            Expression offset,
            bool reversed) : base(statements)
        {
            Identifier = identifier;
            Member = member;
            Limit = limit;
            Offset = offset;
            Reversed = reversed;
        }
        public ForStatement(
            List<Statement> statements,
            string identifier, 
            RangeExpression range,
            // Selz: Support expression for limit and offset instead of just number
            Expression limit,
            Expression offset,
            bool reversed) : base(statements)
        {
            Identifier = identifier;
            Range = range;
            Limit = limit;
            Offset = offset;
            Reversed = reversed;
        }

        public string Identifier { get; }
        public RangeExpression Range { get; }
        // Selz: Support expression for limit and offset instead of just number
        public MemberExpression Member { get; }
        public Expression Limit { get; }
        public Expression Offset { get; }
        public bool Reversed { get; }

        private List<FluidValue> _rangeElements;
        private int _rangeStart, _rangeEnd;

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            IEnumerable<FluidValue> elements = Array.Empty<FluidValue>();

            if (Member != null)
            {
                var member = await Member.EvaluateAsync(context);
                elements = member.Enumerate();
            }
            else if (Range != null)
            {
                int start = Convert.ToInt32((await Range.From.EvaluateAsync(context)).ToNumberValue());
                int end = Convert.ToInt32((await Range.To.EvaluateAsync(context)).ToNumberValue());

                // Cache range
                if (_rangeElements == null || _rangeStart != start || _rangeEnd != end)
                {
                    _rangeElements = new List<FluidValue>();

                    for (var i = start; i <= end; i++)
                    {
                        _rangeElements.Add(NumberValue.Create(i));
                    }
                    
                    elements = _rangeElements;
                    _rangeStart = start;
                    _rangeEnd = end;
                }
                else
                {
                    elements = _rangeElements;
                }
            }

            if (!elements.Any())
            {
                return Completion.Normal;
            }

            // Apply options

            if (Offset != null)
            {
                var offset = (int)(await Offset.EvaluateAsync(context)).ToNumberValue();
                elements = elements.Skip(offset);
            }

            if (Limit != null)
            {
                var limit = (int)(await Limit.EvaluateAsync(context)).ToNumberValue();
                elements = elements.Take(limit);
            }

            if (Reversed)
            {
                elements = elements.Reverse();
            }

            var list = elements.ToList();

            if (!list.Any())
            {
                return Completion.Normal;
            }

            try
            {
                var forloop = new ForLoopValue();

                var length = forloop.Length = list.Count;

                context.SetValue("forloop", forloop);

                for (var i = 0; i < length; i++)
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

                    for (var index = 0; index < Statements.Count; index++)
                    {
                        var statement = Statements[index];
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

        private class ForLoopValue : FluidValue
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

            public override double ToNumberValue()
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
                switch (name)
                {
                    case "length": return new ValueTask<FluidValue>(NumberValue.Create(Length));
                    case "index": return new ValueTask<FluidValue>(NumberValue.Create(Index));
                    case "index0": return new ValueTask<FluidValue>(NumberValue.Create(Index0));
                    case "rindex": return new ValueTask<FluidValue>(NumberValue.Create(RIndex));
                    case "rindex0": return new ValueTask<FluidValue>(NumberValue.Create(RIndex0));
                    case "first": return new ValueTask<FluidValue>(BooleanValue.Create(First));
                    case "last": return new ValueTask<FluidValue>(BooleanValue.Create(Last));
                    default:
                        return new ValueTask<FluidValue>(NilValue.Instance);
                }
            }

            public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
            {
                return;
            }
        }
    }
}
