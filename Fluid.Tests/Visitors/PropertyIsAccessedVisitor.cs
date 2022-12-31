using Fluid.Ast;
using System.Linq;

namespace Fluid.Tests.Visitors
{
    internal class IdentifierIsAccessedVisitor : AstVisitor
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

        protected override Expression VisitMemberExpression(MemberExpression memberExpression)
        {
            var firstSegment = memberExpression.Segments.FirstOrDefault() as IdentifierSegment;

            if (firstSegment != null)
            {
                IsAccessed |= firstSegment.Identifier == _identifier;
            }

            return base.VisitMemberExpression(memberExpression);
        }
    }
}
