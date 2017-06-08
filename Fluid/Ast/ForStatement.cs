using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;

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
            for (var i = 1; i < 3; i++)
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

            return Completion.Normal;
        }
    }
}
