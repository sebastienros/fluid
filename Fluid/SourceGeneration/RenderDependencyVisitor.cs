using Fluid.Ast;

namespace Fluid.SourceGeneration
{
    internal sealed class RenderDependencyVisitor : AstVisitor
    {
        private readonly List<RenderStatement> _renderStatements = [];

        public IReadOnlyList<RenderStatement> RenderStatements => _renderStatements;

        protected internal override Statement VisitRenderStatement(RenderStatement renderStatement)
        {
            _renderStatements.Add(renderStatement);
            return base.VisitRenderStatement(renderStatement);
        }
    }
}
