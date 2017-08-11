using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid
{
    public interface ITag
    {
        /// <summary>
        /// Called when the tag name is found in the template.
        /// </summary>
        /// <param name="node">The <see cref="ParseTreeNode"/> instance representing the tag.</param>
        Statement Parse(ParseTreeNode node, ParserContext context);

        /// <summary>
        /// Customizes the grammar.
        /// </summary>
        /// <param name="grammar"></param>
        BnfTerm GetSyntax(FluidGrammar grammar);
    }

    public abstract class IdentifierTag : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Identifier;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var identifier = node.Token.ValueString;
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, identifier));
        }

        public abstract Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, string identifier);
    }

    public abstract class ExpressionTag : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Expression;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var expression = DefaultFluidParser.BuildExpression(node);
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, expression));
        }

        public abstract Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, Expression term);
    }

    public class DelegateStatement : Statement
    {
        private readonly Func<TextWriter, TextEncoder, TemplateContext, Task<Completion>> _writeAsync;

        public DelegateStatement(Func<TextWriter, TextEncoder, TemplateContext, Task<Completion>> writeAsync)
        {
            _writeAsync = writeAsync;
        }

        public override Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _writeAsync(writer, encoder, context);
        }
    }
}
