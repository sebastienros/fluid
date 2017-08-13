using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
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
}
