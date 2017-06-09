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
                list = (Member.Evaluate(context).ToObjectValue() as IEnumerable<object>)?.Select(FluidValue.Create).ToArray();
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

                        switch (completion)
                        {
                            case Completion.Continue:
                            case Completion.Break:
                                break;
                        }
                    }

                    if (completion == Completion.Continue)
                    {
                        continue;
                    }
                    if (completion == Completion.Break)
                    {
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
