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
        public ForStatement(IList<Statement> statements, string identifier, MemberExpression member) : base(statements)
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
            IList<FluidValue> list = Array.Empty<FluidValue>();

            if (Member != null)
            {
                var member = Member.Evaluate(context);
                var objectValue = member.ToObjectValue();

                switch (objectValue)
                {
                    case IEnumerable<FluidValue> l:
                        list = l.ToArray();
                        break;

                    case IEnumerable<object> o:
                        list = o.Select(FluidValue.Create).ToArray();
                        break;

                    case IEnumerable e:
                        var es = new List<FluidValue>();
                        foreach (var item in e)
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

            var length = list.Count;

            context.EnterChildScope();

            try
            {
                var forloop = new Dictionary<string, FluidValue>();
                forloop.Add("length", new NumberValue(length));
                context.Scope.SetProperty("forloop", new DictionaryValue(forloop));

                for (var i = 0; i < list.Count; i++)
                {
                    var item = list[i];

                    context.SetValue(Identifier, item);

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
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }
    }
}
