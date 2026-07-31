using System.Text.Encodings.Web;
using Fluid.Values;
using Fluid.Utils;

namespace Fluid.Ast
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public sealed class FromStatement : Statement
#pragma warning restore CA1001
    {
        public const string ViewExtension = ".liquid";

        public FromStatement(FluidParser parser, Expression path, IReadOnlyList<string> functions = null)
        {
            Parser = parser;
            Path = path;
            Functions = functions ?? [];
        }

        public FluidParser Parser { get; }
        public Expression Path { get; }
        public IReadOnlyList<string> Functions { get; }

        public override bool IsWhitespaceOrCommentOnly => true;

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();
            if (!relativePath.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ViewExtension;
            }

            var loadedTemplate = await TemplateLoader.LoadAsync(Parser, relativePath, context, defaultFileExtension: null);
            var template = loadedTemplate.Template;

            var parentScope = context.LocalScope;

            // Create a dedicated scope so we can list all macros defined in this template
            context.EnterChildScope();

            try
            {
                await template.RenderAsync(NullFluidOutput.Instance, encoder, context);

                if (Functions.Count > 0)
                {
                    foreach (var functionName in Functions)
                    {
                        var value = context.LocalScope.GetValue(functionName);
                        if (value is FunctionValue)
                        {
                            parentScope.SetValue(functionName, value);
                        }
                    }
                }
                else
                {
                    foreach (var property in context.LocalScope.Properties)
                    {
                        var value = context.LocalScope.GetValue(property);
                        if (value is FunctionValue)
                        {
                            parentScope.SetValue(property, value);
                        }
                    }
                }
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitFromStatement(this);
    }
}
