using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class CaseStatement : TagStatement
    {
        private readonly WhenStatement[] _whenStatements;

        public CaseStatement(
            Expression expression,
            ElseStatement elseStatement = null,
            WhenStatement[] whenStatements = null
        ) : base(new List<Statement>())
        {
            Expression = expression;
            Else = elseStatement;
            _whenStatements = whenStatements ?? Array.Empty<WhenStatement>();
        }

        public Expression Expression { get; }

        public ElseStatement Else { get; }

        public IReadOnlyList<WhenStatement> Whens => _whenStatements;

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var value = await Expression.EvaluateAsync(context);

            foreach (var when in _whenStatements)
            {
                foreach (var option in when.Options)
                {
                    if (value.Equals(await option.EvaluateAsync(context)))
                    {
                        return await when.WriteToAsync(writer, encoder, context);
                    }
                }
            }

            if (Else != null)
            {
                await Else.WriteToAsync(writer, encoder, context);
            }

            return Completion.Normal;
        }
    }
}