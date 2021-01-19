using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class WhenStatement : TagStatement
    {
        private readonly IReadOnlyList<Expression> _options;

        public WhenStatement(IReadOnlyList<Expression> options, List<Statement> statements) : base(statements)
        {
            _options = options ?? Array.Empty<Expression>();
        }

        public IReadOnlyList<Expression> Options => _options;

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // Process statements until next block or end of statements
            for (var index = 0; index < _statements.Count; index++)
            {
                var completion = await _statements[index].WriteToAsync(writer, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }
            }

            return Completion.Normal;
        }

    }
}
