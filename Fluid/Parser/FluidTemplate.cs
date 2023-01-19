using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    public class FluidTemplate : IFluidTemplate, IStatementList
    {
        internal readonly IReadOnlyList<Statement> _statements;
        internal volatile int _count;
        internal bool _compilationStarted;
        internal IFluidTemplate _compiledTemplate;
        private object _synLock = new();

        public FluidTemplate(params Statement[] statements)
        {
            _statements = new List<Statement>(statements ?? Array.Empty<Statement>());
        }

        public FluidTemplate(IReadOnlyList<Statement> statements)
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

#if COMPILATION_SUPPORTED

            if (context.TemplateCompilationThreshold > 0 && !_compilationStarted)
            {
                if (++_count >= context.TemplateCompilationThreshold)
                {
                    // For now we compile only if a model is used

                    lock (_synLock)
                    {
                        var modelType = context.Model?.ToObjectValue()?.GetType() ?? typeof(object);

                        if (!_compilationStarted && modelType != null)
                        {
                            // THIS IS ONLY FOR HAVING ALL TESTS RUN WITH COMPILED TEMPLATES
                            // BEGIN
                            if (context.TemplateCompilationThreshold == 1)
                            {
                                // Compile synchronously

                                _compiledTemplate = this.Compile(modelType);
                            }
                            // END
                            else
                            {
                                // Compile the template asynchronously
                                // Queue the compilation on the thread pool
                                ThreadPool.QueueUserWorkItem((state) =>
                                {
                                    _compiledTemplate = this.Compile(modelType);
                                }, (object)null, false);
                            }
                        }

                        _compilationStarted = true;
                    }
                }
            }

            if (_compiledTemplate != null)
            {
                return _compiledTemplate.RenderAsync(writer, encoder, context);
            }
#endif

            return RenderAsyncInternal(writer, encoder, context);
        }

        private ValueTask RenderAsyncInternal(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
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
    }
}
