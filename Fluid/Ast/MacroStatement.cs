using Fluid.Utils;
using Fluid.Values;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class MacroStatement : TagStatement
    {
        public MacroStatement(string identifier, IReadOnlyList<FunctionCallArgument> arguments, List<Statement> statements): base(statements)
        {
            Identifier = identifier;
            Arguments = arguments;
        }

        public string Identifier { get; }
        public IReadOnlyList<FunctionCallArgument> Arguments { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
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
                using var sb = StringBuilderPool.GetInstance();
                using var sw = new StringWriter(sb.Builder);

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

                    for (var i = 0; i < _statements.Count; i++)
                    {
                        var completion = await _statements[i].WriteToAsync(sw, encoder, context);

                        if (completion != Completion.Normal)
                        {
                            // Stop processing the block statements
                            // We return the completion to flow it to the outer loop
                            break;
                        }
                    }

                    var result = sw.ToString();

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
    }
}
