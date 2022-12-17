using Fluid.Ast;
using Fluid.Compilation;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    public class FluidTemplate : IFluidTemplate, ICompilable, IStatementList
    {
        internal readonly List<Statement> _statements;

        public FluidTemplate(params Statement[] statements)
        {
            _statements = new List<Statement>(statements ?? Array.Empty<Statement>());
        }

        public FluidTemplate(List<Statement> statements)
        {
            _statements = statements ?? throw new ArgumentNullException(nameof(statements));
        }

        public IReadOnlyList<Statement> Statements => _statements;

        public ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            if (context == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(context));
            }

            var count = Statements.Count;
            for (var i = 0; i < count; i++)
            {
                var task = Statements[i].WriteToAsync(writer, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(
                        task,
                        writer,
                        encoder,
                        context,
                        Statements,
                        startIndex: i + 1);
                }
            }

#if NET5_0_OR_GREATER
            return ValueTask.CompletedTask;
#else
            return new ValueTask();
#endif
        }

        internal static async ValueTask Awaited(
            ValueTask<Completion> task,
            TextWriter writer,
            TextEncoder encoder,
            TemplateContext context,
            IReadOnlyList<Statement> statements,
            int startIndex)
        {
            await task;
            for (var i = startIndex; i < statements.Count; i++)
            {
                await statements[i].WriteToAsync(writer, encoder, context);
            }
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = context.CreateCompilationResult();
            
            var caller = context.Caller;

            for (var i = 0; i < Statements.Count; i++)
            {
                var statementAccessor = $"{caller}.Statements[{i}]";

                var statementResult = CompilationHelpers.CompileStatement(Statements[i], statementAccessor, context);
                result.AppendLine(statementResult.ToString());
            }

            return result;
        }
    }
}
