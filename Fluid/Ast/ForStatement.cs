using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public class ForStatement : TagStatement
    {
        public ForStatement(IList<Statement> statements, string identifier, MemberExpression member) :base (statements)
        {
            Identifier = identifier;
            Member = member;
        }
        public ForStatement(IList<Statement> statements, string identifier, RangeExpression range) : base(statements)
        {
            Identifier = identifier;
            Range = range;
        }

        public string Identifier { get; }
        public RangeExpression Range { get; }
        public MemberExpression Member { get; }

        public override Completion WriteTo(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            IEnumerable<FluidValue> list = Enumerable.Empty<FluidValue>();

            if (Member != null)
            {
                var member = Member.Evaluate(context);
                var objectValue = member.ToObjectValue();

                switch (objectValue)
                {
                    case IEnumerable<FluidValue> l:
                        list = l;
                        break;
                    case IEnumerable<object> o:
                        list = o.Select(FluidValue.Create).ToArray();
                        break;
                    case IEnumerable e:
                        var es = new List<FluidValue>();
                        foreach(var item in e)
                        {
                            es.Add(FluidValue.Create(item));
                        }
                        list = es;
                        break;
                }
            }
            else if (Range != null)
            {
                int start = Convert.ToInt32(Range.From.Evaluate(context).ToNumberValue());
                int end = Convert.ToInt32(Range.To.Evaluate(context).ToNumberValue());
                list = Enumerable.Range(start, end - start + 1).Select(x => new NumberValue(x)).ToArray();
            }

            foreach (var item in list)
            {
                context.EnterChildScope();
                context.SetValue(Identifier, item);
                try
                {
                    Completion completion = Completion.Normal;

                    foreach (var statement in Statements)
                    {
                        completion = statement.WriteTo(writer, encoder, context);

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
                finally
                {
                    context.ReleaseScope();
                }
            }

            return Completion.Normal;
        }
    }
}
