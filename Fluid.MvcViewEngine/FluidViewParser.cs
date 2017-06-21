using Fluid.Ast;
using Fluid.MvcViewEngine.Statements;
using Irony.Parsing;

namespace Fluid.MvcViewEngine
{
    public class FluidViewParser : IronyFluidParser<FluidViewGrammar>
    {
        protected override Statement BuildTagStatement(ParseTreeNode node)
        {
            var tag = node.ChildNodes[0];

            switch (tag.Term.Name)
            {
                case "layout":
                    return BuildLayoutStatement(tag);

                case "renderbody":
                    return BuildRenderBodyStatement(tag);

                case "rendersection":
                    return BuildRenderSectionStatement(tag);

                case "section":
                    EnterBlock(tag);
                    return null;

                case "endsection":
                    return BuildRegisterSectionStatement("section");

                case "include":
                    return BuildIncludeStatement(tag);

                default:
                    return base.BuildTagStatement(node);
            }
        }

        private LayoutStatement BuildLayoutStatement(ParseTreeNode tag)
        {
            var pathExpression = BuildTermExpression(tag.ChildNodes[0]);
            return new LayoutStatement(pathExpression);
        }

        private RenderBodyStatement BuildRenderBodyStatement(ParseTreeNode tag)
        {
            return new RenderBodyStatement();
        }

        private RenderSectionStatement BuildRenderSectionStatement(ParseTreeNode tag)
        {
            var sectionName = tag.ChildNodes[0].Token.ValueString;
            return new RenderSectionStatement(sectionName);
        }

        private RegisterSectionStatement BuildRegisterSectionStatement(string expectedBeginTag)
        {
            if (_currentContext.Tag.Term.Name != expectedBeginTag)
            {
                throw new ParseException($"Unexpected tag: ${_currentContext.Tag.Term.Name} not matching {expectedBeginTag} tag.");
            }

            var identifier = _currentContext.Tag.ChildNodes[0].Token.Text;

            var registerSectionStatement = new RegisterSectionStatement(identifier, _currentContext.Statements);

            ExitBlock();

            return registerSectionStatement;
        }

        private IncludeStatement BuildIncludeStatement(ParseTreeNode tag)
        {
            var pathExpression = BuildTermExpression(tag.ChildNodes[0]);
            return new IncludeStatement(pathExpression);
        }
    }
}
