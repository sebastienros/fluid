using Parlot;
using Parlot.Compilation;
using Parlot.Fluent;
using Parlot.Rewriting;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Fluid.Parser
{
    /// <summary>
    /// Wraps a parser that can recurse into itself, like the body of a block or a nested expression. It enforces the
    /// <see cref="FluidParseContext.MaxRecursionDepth"/> and ensures enough stack space is left, so that a deeply nested
    /// template fails to parse instead of crashing the process with a stack overflow.
    /// </summary>
    internal sealed class NestingGuardParser<T> : Parser<T>, ICompilable, ISeekable
    {
        private static readonly MethodInfo EnterMethod = typeof(NestingGuardParser<T>).GetMethod(nameof(Enter), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo ExitMethod = typeof(NestingGuardParser<T>).GetMethod(nameof(Exit), BindingFlags.NonPublic | BindingFlags.Static);

        private readonly Parser<T> _parser;

        public NestingGuardParser(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));

            if (parser is ISeekable seekable)
            {
                CanSeek = seekable.CanSeek;
                ExpectedChars = seekable.ExpectedChars;
                SkipWhitespace = seekable.SkipWhitespace;
            }
        }

        public bool CanSeek { get; }

        public char[] ExpectedChars { get; } = [];

        public bool SkipWhitespace { get; }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            Enter(context);

            var success = _parser.Parse(context, ref result);

            Exit(context);

            return success;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = context.CreateCompilationResult<T>();

            var parserCompileResult = _parser.Build(context);

            // Enter(context);
            //
            // parser instructions
            //
            // Exit(context);
            // success = parser.success;
            // value = parser.value;

            result.Body.Add(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Call(EnterMethod, context.ParseContext),
                    Expression.Block(parserCompileResult.Body),
                    Expression.Call(ExitMethod, context.ParseContext),
                    Expression.Assign(result.Success, parserCompileResult.Success),
                    context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(result.Value, parserCompileResult.Value)
                    )
                );

            return result;
        }

        private static void Enter(ParseContext context)
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();

            (context as FluidParseContext)?.EnterRecursion();
        }

        // A parser that throws doesn't exit: the exception ends the parsing of the template.
        private static void Exit(ParseContext context)
        {
            (context as FluidParseContext)?.ExitRecursion();
        }

        public override string ToString() => _parser.ToString();
    }
}
