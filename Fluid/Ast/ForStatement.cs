using System;
using System.Collections.Generic;
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
            LiteralExpression limit,
            LiteralExpression offset,
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
            LiteralExpression limit,
            LiteralExpression offset,
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
        public MemberExpression Member { get; }
        public LiteralExpression Limit { get; }
        public LiteralExpression Offset { get; }
        public bool Reversed { get; }

        private IEnumerable<FluidValue> _rangeElements;
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
                    _rangeElements = elements = Enumerable.Range(start, end - start + 1).Select(x => new NumberValue(x));
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
                var forloop = new LoopFluidIndexable();

                forloop.Length = list.Count;
                context.SetValue("forloop", forloop);

                for (var i = 0; i < list.Count; i++)
                {
                    var item = list[i];

                    context.SetValue(Identifier, item);

                    // Set helper variables
                    forloop.Index = i + 1;
                    forloop.Index0 = i;
                    forloop.RIndex = list.Count - i - 1;
                    forloop.RIndex0 = list.Count - i;
                    forloop.First = i == 0;
                    forloop.Last = i == list.Count - 1;

                    Completion completion = Completion.Normal;

                    for (var index = 0; index < Statements.Count; index++)
                    {
                        var statement = Statements[index];
                        completion = await statement.WriteToAsync(writer, encoder, context);

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

        private class LoopFluidIndexable : IFluidIndexable
        {
            private static string[] _keys = new[] { "length", "index", "index0", "rindex", "rindex0", "first", "last" };

            public int Length { get; set; }
            public int Index { get; set; }
            public int Index0 { get; set; }
            public int RIndex { get; set; }
            public int RIndex0 { get; set; }
            public bool First { get; set; }
            public bool Last { get; set; }

            public int Count => Length;

            public IEnumerable<string> Keys => _keys;

            public bool TryGetValue(string name, out FluidValue value)
            {
                switch (name)
                {
                    case "length": value = new NumberValue(Length); break;
                    case "index": value = new NumberValue(Index); break;
                    case "index0": value = new NumberValue(Index0); break;
                    case "rindex": value = new NumberValue(RIndex); break;
                    case "rindex0": value = new NumberValue(RIndex0); break;
                    case "first": value = new BooleanValue(First); break;
                    case "last": value = new BooleanValue(Last); break;
                    default:
                        value = NilValue.Instance;
                        return false;
                }

                return true;
            }
        }
    }
}
