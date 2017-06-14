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
            IList<Statement> statements, 
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
            IList<Statement> statements, 
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

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
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
                elements = Enumerable.Range(start, end - start + 1).Select(x => new NumberValue(x));
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

            var forScope = context.EnterChildScope();

            try
            {
                var forloop = new Dictionary<string, FluidValue>();
                forloop.Add("length", new NumberValue(list.Count));
                forScope.SetValue("forloop", new DictionaryValue(forloop));


                for (var i = 0; i < list.Count; i++)
                {
                    var item = list[i];

                    forScope.SetValue(Identifier, item);

                    // Set helper variables
                    forloop["index"] = new NumberValue(i + 1);
                    forloop["index0"] = new NumberValue(i);
                    forloop["rindex"] = new NumberValue(list.Count - i - 1);
                    forloop["rindex0"] = new NumberValue(list.Count - i);
                    forloop["first"] = new BooleanValue(i == 0);
                    forloop["last"] = new BooleanValue(i == list.Count - 1);

                    Completion completion = Completion.Normal;

                    foreach (var statement in Statements)
                    {
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
                context.ReleaseScope();
            }

            return Completion.Normal;
        }
    }
}
