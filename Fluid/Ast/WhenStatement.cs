using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class WhenStatement : TagStatement
    {
        private readonly List<Expression> _options;

        public WhenStatement(List<Expression> options, List<Statement> statements) : base(statements)
        {
            _options = options;
        }

        public List<Expression> Options
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _options;
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // Process statements until next block or end of statements
            for (var index = 0; index < Statements.Count; index++)
            {
                var completion = await Statements[index].WriteToAsync(writer, encoder, context);

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
