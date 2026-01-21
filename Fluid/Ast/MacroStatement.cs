using Fluid.Utils;
using Fluid.Values;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class MacroStatement : TagStatement
    {
        public MacroStatement(string identifier, IReadOnlyList<FunctionCallArgument> arguments, IReadOnlyList<Statement> statements) : base(statements)
        {
            Identifier = identifier;
            Arguments = arguments ?? [];
        }

        public string Identifier { get; }
        public IReadOnlyList<FunctionCallArgument> Arguments { get; }

        public override bool IsWhitespaceOrCommentOnly => true;

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            // Evaluate all default values only once
            var defaultValues = new Dictionary<string, FluidValue>();

            for (var i = 0; i < Arguments.Count; i++)
            {
                var argument = Arguments[i];
                defaultValues[argument.Name] = argument.Expression == null ? NilValue.Instance : await argument.Expression.EvaluateAsync(context);
            }

            var f = new FunctionValue(async (args, c) =>
            {
                using var macroOutput = new BufferFluidOutput();

                context.EnterChildScope();

                try
                {
                    // Initialize the local context with the default values
                    foreach (var a in defaultValues)
                    {
                        context.SetValue(a.Key, a.Value);
                    }

                    var namedArguments = false;

                    // Apply all arguments from the invocation.
                    // As soon as a named argument is used, all subsequent ones need a name too.

                    for (var i = 0; i < args.Count; i++)
                    {
                        var positionalName = Arguments[i].Name;

                        namedArguments |= args.HasNamed(positionalName);

                        if (!namedArguments)
                        {
                            context.SetValue(positionalName, args.At(i));
                        }
                        else
                        {
                            context.SetValue(positionalName, args[positionalName]);
                        }
                    }

                    for (var i = 0; i < Statements.Count; i++)
                    {
                        var completion = await Statements[i].WriteToAsync(macroOutput, encoder, context);

                        if (completion != Completion.Normal)
                        {
                            // Stop processing the block statements
                            // We return the completion to flow it to the outer loop
                            break;
                        }
                    }

                    var result = macroOutput.ToString();

                    // Don't encode the result
                    return new StringValue(result, false);
                }
                finally
                {
                    context.ReleaseScope();
                }
            });

            context.SetValue(Identifier, f);

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitMacroStatement(this);
    }
}
