using Fluid.Ast.BinaryExpressions;
using Fluid.Parser;

namespace Fluid.Ast
{
    public class AstVisitor
    {
        public virtual IFluidTemplate VisitTemplate(IFluidTemplate template)
        {
            if (template is IStatementList list)
            {
                foreach (var statement in list.Statements)
                {
                    Visit(statement);
                }

                return template;
            }

            throw new NotSupportedException("The template cannot be visited as it doesn't implement IStatementList");
        }

        public virtual Statement Visit(Statement statement)
        {
            return statement?.Accept(this);
        }

        public virtual Expression Visit(Expression expression)
        {
            return expression?.Accept(this);
        }

        public virtual Expression VisitOtherExpression(Expression expression)
        {
            // Custom visitor implementations need to override this method to handle
            // AST nodes that are not part of the core types.

            return expression;
        }

        public virtual Statement VisitOtherStatement(Statement statement)
        {
            // Custom visitor implementations need to override this method to handle
            // AST nodes that are not part of the core types.

            return statement;
        }

        protected internal virtual Statement VisitAssignStatement(AssignStatement assignStatement)
        {
            Visit(assignStatement.Value);

            return assignStatement;
        }

        protected internal virtual Expression VisitAddBinaryExpression(AddBinaryExpression addBinaryExpression)
        {
            Visit(addBinaryExpression.Left);

            Visit(addBinaryExpression.Right);

            return addBinaryExpression;
        }

        protected internal virtual Expression VisitAndBinaryExpression(AndBinaryExpression andBinaryExpression)
        {
            Visit(andBinaryExpression.Left);

            Visit(andBinaryExpression.Right);

            return andBinaryExpression;
        }

        protected internal virtual Expression VisitContainsBinaryExpression(ContainsBinaryExpression containsBinaryExpression)
        {
            Visit(containsBinaryExpression.Left);

            Visit(containsBinaryExpression.Right);

            return containsBinaryExpression;
        }

        protected internal virtual Expression VisitDivideBinaryExpression(DivideBinaryExpression divideBinaryExpression)
        {
            Visit(divideBinaryExpression.Left);

            Visit(divideBinaryExpression.Right);

            return divideBinaryExpression;
        }

        protected internal virtual Expression VisitEndsWithBinaryExpression(EndsWithBinaryExpression endsWithBinaryExpression)
        {
            Visit(endsWithBinaryExpression.Left);

            Visit(endsWithBinaryExpression.Right);

            return endsWithBinaryExpression;
        }

        protected internal virtual Expression VisitEqualBinaryExpression(EqualBinaryExpression equalBinaryExpression)
        {
            Visit(equalBinaryExpression.Left);

            Visit(equalBinaryExpression.Right);

            return equalBinaryExpression;
        }

        protected internal virtual Expression VisitGreaterThanBinaryExpression(GreaterThanBinaryExpression greaterThanBinaryExpression)
        {
            Visit(greaterThanBinaryExpression.Left);

            Visit(greaterThanBinaryExpression.Right);

            return greaterThanBinaryExpression;
        }

        protected internal virtual Expression VisitLowerThanBinaryExpression(LowerThanBinaryExpression lowerThanBinaryExpression)
        {
            Visit(lowerThanBinaryExpression.Left);

            Visit(lowerThanBinaryExpression.Right);

            return lowerThanBinaryExpression;
        }

        protected internal virtual Expression VisitModuloBinaryExpression(ModuloBinaryExpression moduloBinaryExpression)
        {
            Visit(moduloBinaryExpression.Left);

            Visit(moduloBinaryExpression.Right);

            return moduloBinaryExpression;
        }

        protected internal virtual Expression VisitMultiplyBinaryExpression(MultiplyBinaryExpression multiplyBinaryExpression)
        {
            Visit(multiplyBinaryExpression.Left);

            Visit(multiplyBinaryExpression.Right);

            return multiplyBinaryExpression;
        }

        protected internal virtual Expression VisitNotEqualBinaryExpression(NotEqualBinaryExpression notEqualBinaryExpression)
        {
            Visit(notEqualBinaryExpression.Left);

            Visit(notEqualBinaryExpression.Right);

            return notEqualBinaryExpression;
        }

        protected internal virtual Expression VisitOrBinaryExpression(OrBinaryExpression orBinaryExpression)
        {
            Visit(orBinaryExpression.Left);

            Visit(orBinaryExpression.Right);

            return orBinaryExpression;
        }

        protected internal virtual Expression VisitSubtractBinaryExpression(SubtractBinaryExpression subtractBinaryExpression)
        {
            Visit(subtractBinaryExpression.Left);

            Visit(subtractBinaryExpression.Right);

            return subtractBinaryExpression;
        }

        protected internal virtual Expression VisitStartsWithBinaryExpression(StartsWithBinaryExpression startsWithBinaryExpression)
        {
            Visit(startsWithBinaryExpression.Left);

            Visit(startsWithBinaryExpression.Right);

            return startsWithBinaryExpression;
        }

        protected internal virtual Statement VisitBreakStatement(BreakStatement breakStatement)
        {
            return breakStatement;
        }

        protected internal virtual Statement VisitCallbackStatement(CallbackStatement callbackStatement)
        {
            return callbackStatement;
        }

        protected internal virtual Statement VisitCaptureStatement(CaptureStatement captureStatement)
        {
            foreach (var statement in captureStatement.Statements)
            {
                Visit(statement);
            }

            return captureStatement;
        }

        protected internal virtual Statement VisitCaseStatement(CaseStatement caseStatement)
        {

            Visit(caseStatement.Expression);

            foreach (var block in caseStatement.Blocks)
            {
                if (block is WhenBlock whenBlock)
                {
                    foreach (var option in whenBlock.Options)
                    {
                        Visit(option);
                    }
                    foreach (var statement in whenBlock.Statements)
                    {
                        Visit(statement);
                    }
                }
                else if (block is ElseBlock elseBlock)
                {
                    foreach (var statement in elseBlock.Statements)
                    {
                        Visit(statement);
                    }
                }
            }

            return caseStatement;
        }

        protected internal virtual Statement VisitCommentStatement(CommentStatement commentStatement)
        {
            return commentStatement;
        }

        protected internal virtual Statement VisitContinueStatement(ContinueStatement continueStatement)
        {
            return continueStatement;
        }

        protected internal virtual Statement VisitCycleStatement(CycleStatement cycleStatement)
        {
            Visit(cycleStatement.Group);

            foreach (var value in cycleStatement.Values)
            {
                Visit(value);
            }

            return cycleStatement;
        }

        protected internal virtual Statement VisitDecrementStatement(DecrementStatement decrementStatement)
        {
            return decrementStatement;
        }

        protected internal virtual Statement VisitElseIfStatement(ElseIfStatement elseIfStatement)
        {
            Visit(elseIfStatement.Condition);

            foreach (var statement in elseIfStatement.Statements)
            {
                Visit(statement);
            }

            return elseIfStatement;
        }

        protected internal virtual Statement VisitElseStatement(ElseStatement elseStatement)
        {
            foreach (var statement in elseStatement.Statements)
            {
                Visit(statement);
            }

            return elseStatement;
        }

        protected internal virtual Statement VisitEmptyBlockStatement(EmptyBlockStatement emptyBlockStatement)
        {
            foreach (var statement in emptyBlockStatement.Statements)
            {
                Visit(statement);
            }

            return emptyBlockStatement;
        }

        protected internal virtual Statement VisitEmptyTagStatement(EmptyTagStatement emptyTagStatement)
        {
            return emptyTagStatement;
        }

        protected internal virtual Expression VisitFilterExpression(FilterExpression filterExpression)
        {
            Visit(filterExpression.Input);

            foreach (var parameter in filterExpression.Parameters)
            {
                Visit(parameter.Expression);
            }

            return filterExpression;
        }

        protected internal virtual Statement VisitForStatement(ForStatement forStatement)
        {
            Visit(forStatement.Source);
            Visit(forStatement.Limit);
            Visit(forStatement.Offset);

            foreach (var statement in forStatement.Statements)
            {
                Visit(statement);
            }

            Visit(forStatement.Else);

            return forStatement;
        }

        protected internal virtual Statement VisitTableRowStatement(TableRowStatement tableRowStatement)
        {
            Visit(tableRowStatement.Source);
            Visit(tableRowStatement.Limit);
            Visit(tableRowStatement.Offset);
            Visit(tableRowStatement.Cols);

            foreach (var statement in tableRowStatement.Statements)
            {
                Visit(statement);
            }

            return tableRowStatement;
        }

        protected internal virtual Statement VisitFromStatement(FromStatement fromStatement)
        {
            Visit(fromStatement.Path);

            return fromStatement;
        }

        protected internal virtual Statement VisitIfStatement(IfStatement ifStatement)
        {
            Visit(ifStatement.Condition);
            Visit(ifStatement.Else);

            foreach (var statement in ifStatement.Statements)
            {
                Visit(statement);
            }

            foreach (var statement in ifStatement.ElseIfs)
            {
                Visit(statement);
            }

            return ifStatement;
        }

        protected internal virtual Statement VisitIncludeStatement(IncludeStatement includeStatement)
        {
            Visit(includeStatement.For);
            foreach (var statement in includeStatement.AssignStatements)
            {
                Visit(statement);
            }

            Visit(includeStatement.With);
            Visit(includeStatement.For);
            Visit(includeStatement.Path);

            return includeStatement;
        }

        protected internal virtual Statement VisitIncrementStatement(IncrementStatement incrementStatement)
        {
            return incrementStatement;
        }

        protected internal virtual Statement VisitLiquidStatement(LiquidStatement liquidStatement)
        {
            foreach (var statement in liquidStatement.Statements)
            {
                Visit(statement);
            }

            return liquidStatement;
        }

        protected internal virtual Expression VisitLiteralExpression(LiteralExpression literalExpression)
        {
            return literalExpression;
        }

        protected internal virtual Statement VisitMacroStatement(MacroStatement macroStatement)
        {

            foreach (var argument in macroStatement.Arguments)
            {
                Visit(argument.Expression);
            }

            foreach (var statement in macroStatement.Statements)
            {
                Visit(statement);
            }

            return macroStatement;
        }

        protected internal virtual Expression VisitMemberExpression(MemberExpression memberExpression)
        {
            return memberExpression;
        }

        protected internal virtual Statement VisitNoOpStatement(NoOpStatement noOpStatement)
        {
            return noOpStatement;
        }

        protected internal virtual Statement VisitParserBlockStatement<T>(ParserBlockStatement<T> parserBlockStatement)
        {
            foreach (var statement in parserBlockStatement.Statements)
            {
                Visit(statement);
            }

            return parserBlockStatement;
        }

        protected internal virtual Statement VisitParserTagStatement<T>(ParserTagStatement<T> parserTagStatement)
        {
            return parserTagStatement;
        }

        protected internal virtual Statement VisitOutputStatement(OutputStatement outputStatement)
        {
            Visit(outputStatement.Expression);

            return outputStatement;
        }

        protected internal virtual Expression VisitRangeExpression(RangeExpression rangeExpression)
        {
            Visit(rangeExpression.From);
            Visit(rangeExpression.To);

            return rangeExpression;
        }

        protected internal virtual Statement VisitRawStatement(RawStatement rawStatement)
        {
            return rawStatement;
        }

        protected internal virtual Statement VisitRenderStatement(RenderStatement renderStatement)
        {
            Visit(renderStatement.With);
            Visit(renderStatement.For);

            foreach (var statement in renderStatement.AssignStatements)
            {
                Visit(statement);
            }

            return renderStatement;
        }

        protected internal virtual Statement VisitTextSpanStatement(TextSpanStatement textSpanStatement)
        {
            return textSpanStatement;
        }

        protected internal virtual Statement VisitUnlessStatement(UnlessStatement unlessStatement)
        {
            Visit(unlessStatement.Condition);

            foreach (var elseIf in unlessStatement.ElseIfs)
            {
                Visit(elseIf);
            }

            Visit(unlessStatement.Else);

            foreach (var statement in unlessStatement.Statements)
            {
                Visit(statement);
            }
            return unlessStatement;
        }

        protected internal virtual Statement VisitWhenStatement(WhenStatement whenStatement)
        {
            foreach (var option in whenStatement.Options)
            {
                Visit(option);
            }

            foreach (var statement in whenStatement.Statements)
            {
                Visit(statement);
            }
            return whenStatement;
        }
    }
}
