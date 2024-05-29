using Fluid.Ast;

namespace Fluid.Tests.Visitors
{
    internal sealed class IdentifierIsAccessedVisitor : AstVisitor
    {
        private readonly string _identifier;

        public IdentifierIsAccessedVisitor(string identifier)
        {
            _identifier = identifier;
        }

        public bool IsAccessed { get; private set; }

        public override IFluidTemplate VisitTemplate(IFluidTemplate template)
        {
            // Initialize the result before each usage

            IsAccessed = false;
            return base.VisitTemplate(template);
        }

        protected internal override Expression VisitMemberExpression(MemberExpression memberExpression)
        {
            if (memberExpression.Segments.FirstOrDefault() is IdentifierSegment firstSegment)
            {
                IsAccessed |= firstSegment.Identifier == _identifier;
            }

            return base.VisitMemberExpression(memberExpression);
        }
    }
}
