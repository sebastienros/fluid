﻿using Fluid.Ast;

namespace Fluid.Tests.Visitors
{
    internal sealed class ContinueOffsetVisitor : AstVisitor
    {
        public bool HasContinueForLoop { get; private set; }

        public override IFluidTemplate VisitTemplate(IFluidTemplate template)
        {
            // Initialize the result before each usage

            HasContinueForLoop = false;
            return base.VisitTemplate(template);
        }

        protected internal override Statement VisitForStatement(ForStatement forStatement)
        {
            HasContinueForLoop |= forStatement.OffsetIsContinue;

            return base.VisitForStatement(forStatement);
        }
    }
}
