using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    internal sealed class CaseStatement : TagStatement
    {
        private readonly Expression _expression;
        private readonly ElseStatement _else;
        private readonly WhenStatement[] _whenStatements;

        public CaseStatement(
            Expression expression,
            ElseStatement elseStatement = null,
            WhenStatement[] whenStatements = null
        ) : base(new List<Statement>())
        {
            _expression = expression;
            _else = elseStatement;
            _whenStatements = whenStatements ?? Array.Empty<WhenStatement>();
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var value = await _expression.EvaluateAsync(context);

            foreach (var when in _whenStatements)
            {
                for (var i = 0; i < when._options.Count; i++)
                {
                    var option = when._options[i];
                    if (value.Equals(await option.EvaluateAsync(context)))
                    {
                        return await when.WriteToAsync(writer, encoder, context);
                    }
                }
            }

            if (_else != null)
            {
                await _else.WriteToAsync(writer, encoder, context);
            }

            return Completion.Normal;
        }
    }
}