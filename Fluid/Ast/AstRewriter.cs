using Fluid.Ast.BinaryExpressions;
using Fluid.Parser;

namespace Fluid.Ast
{
    public class AstRewriter : AstVisitor
    {
        protected bool TryRewriteStatement<T>(T statement, out T result) where T : Statement
        {
            var newStatement = Visit(statement);

            if (newStatement != statement)
            {
                result = newStatement as T;
                return true;
            }

            result = statement;
            return false;
        }

        protected bool TryRewriteExpression<T>(T expression, out T result) where T : Expression
        {
            var newExpression = Visit(expression);

            if (newExpression != expression)
            {
                result = newExpression as T;
                return true;
            }

            result = expression;
            return false;
        }

        protected bool TryRewriteStatements<T>(IReadOnlyList<T> statements, out IReadOnlyList<T> result) where T : Statement
        {
            var updated = false;

            var newStatements = new List<T>(statements.Count);

            foreach (var statement in statements)
            {
                var newStatement = Visit(statement) as T;
                updated |= newStatement != statement;

                if (newStatement != null)
                {
                    newStatements.Add(newStatement);
                }
            }

            result = updated ? newStatements : statements;

            return updated;
        }

        protected bool TryRewriteExpressions<T>(IReadOnlyList<T> expressions, out IReadOnlyList<T> result) where T : Expression
        {
            var updated = false;

            var newExpressions = new List<T>(expressions.Count);

            foreach (var expression in expressions)
            {
                var newStatement = Visit(expression) as T;
                updated |= newStatement != expression;

                if (newStatement != null)
                {
                    newExpressions.Add(newStatement);
                }
            }

            result = updated ? newExpressions : expressions;

            return updated;
        }

        public override IFluidTemplate VisitTemplate(IFluidTemplate template)
        {
            if (template is IStatementList list)
            {
                if (TryRewriteStatements(list.Statements, out var statements))
                {
                    return new FluidTemplate(statements);
                }
                else
                {
                    return template;
                }
            }

            throw new NotSupportedException("The template cannot be visited as it doesn't implement IStatementList");
        }

        public override Statement Visit(Statement statement)
        {
            return statement?.Accept(this);
        }

        public override Expression Visit(Expression expression)
        {
            return expression?.Accept(this);
        }

        protected internal override Statement VisitAssignStatement(AssignStatement assignStatement)
        {
            if (TryRewriteExpression(assignStatement.Value, out var newValue))
            {
                return new AssignStatement(assignStatement.Identifier, newValue);
            }

            return assignStatement;
        }

        protected internal override Expression VisitAddBinaryExpression(AddBinaryExpression addBinaryExpression)
        {
            if (TryRewriteExpression(addBinaryExpression.Left, out var newLeft) | TryRewriteExpression(addBinaryExpression.Right, out var newRight))
            {
                return new AddBinaryExpression(newLeft, newRight);
            }

            return addBinaryExpression;
        }

        protected internal override Expression VisitAndBinaryExpression(AndBinaryExpression andBinaryExpression)
        {
            if (TryRewriteExpression(andBinaryExpression.Left, out var newLeft) | TryRewriteExpression(andBinaryExpression.Right, out var newRight))
            {
                return new AndBinaryExpression(newLeft, newRight);
            }

            return andBinaryExpression;
        }

        protected internal override Expression VisitContainsBinaryExpression(ContainsBinaryExpression containsBinaryExpression)
        {
            if (TryRewriteExpression(containsBinaryExpression.Left, out var newLeft) | TryRewriteExpression(containsBinaryExpression.Right, out var newRight))
            {
                return new ContainsBinaryExpression(newLeft, newRight);
            }

            return containsBinaryExpression;
        }

        protected internal override Expression VisitDivideBinaryExpression(DivideBinaryExpression divideBinaryExpression)
        {
            if (TryRewriteExpression(divideBinaryExpression.Left, out var newLeft) | TryRewriteExpression(divideBinaryExpression.Right, out var newRight))
            {
                return new DivideBinaryExpression(newLeft, newRight);
            }

            return divideBinaryExpression;
        }

        protected internal override Expression VisitEndsWithBinaryExpression(EndsWithBinaryExpression endsWithBinaryExpression)
        {
            if (TryRewriteExpression(endsWithBinaryExpression.Left, out var newLeft) | TryRewriteExpression(endsWithBinaryExpression.Right, out var newRight))
            {
                return new EndsWithBinaryExpression(newLeft, newRight);
            }

            return endsWithBinaryExpression;
        }

        protected internal override Expression VisitEqualBinaryExpression(EqualBinaryExpression equalBinaryExpression)
        {
            if (TryRewriteExpression(equalBinaryExpression.Left, out var newLeft) | TryRewriteExpression(equalBinaryExpression.Right, out var newRight))
            {
                return new EqualBinaryExpression(newLeft, newRight);
            }

            return equalBinaryExpression;
        }

        protected internal override Expression VisitGreaterThanBinaryExpression(GreaterThanBinaryExpression greaterThanBinaryExpression)
        {
            if (TryRewriteExpression(greaterThanBinaryExpression.Left, out var newLeft) | TryRewriteExpression(greaterThanBinaryExpression.Right, out var newRight))
            {
                return new GreaterThanBinaryExpression(newLeft, newRight, greaterThanBinaryExpression.Strict);
            }

            return greaterThanBinaryExpression;
        }

        protected internal override Expression VisitLowerThanBinaryExpression(LowerThanBinaryExpression lowerThanBinaryExpression)
        {
            if (TryRewriteExpression(lowerThanBinaryExpression.Left, out var newLeft) | TryRewriteExpression(lowerThanBinaryExpression.Right, out var newRight))
            {
                return new LowerThanBinaryExpression(newLeft, newRight, lowerThanBinaryExpression.Strict);
            }

            return lowerThanBinaryExpression;
        }

        protected internal override Expression VisitModuloBinaryExpression(ModuloBinaryExpression moduloBinaryExpression)
        {
            if (TryRewriteExpression(moduloBinaryExpression.Left, out var newLeft) | TryRewriteExpression(moduloBinaryExpression.Right, out var newRight))
            {
                return new ModuloBinaryExpression(newLeft, newRight);
            }

            return moduloBinaryExpression;
        }

        protected internal override Expression VisitMultiplyBinaryExpression(MultiplyBinaryExpression multiplyBinaryExpression)
        {
            if (TryRewriteExpression(multiplyBinaryExpression.Left, out var newLeft) | TryRewriteExpression(multiplyBinaryExpression.Right, out var newRight))
            {
                return new MultiplyBinaryExpression(newLeft, newRight);
            }

            return multiplyBinaryExpression;
        }

        protected internal override Expression VisitNotEqualBinaryExpression(NotEqualBinaryExpression notEqualBinaryExpression)
        {
            if (TryRewriteExpression(notEqualBinaryExpression.Left, out var newLeft) | TryRewriteExpression(notEqualBinaryExpression.Right, out var newRight))
            {
                return new NotEqualBinaryExpression(newLeft, newRight);
            }

            return notEqualBinaryExpression;
        }

        protected internal override Expression VisitOrBinaryExpression(OrBinaryExpression orBinaryExpression)
        {
            if (TryRewriteExpression(orBinaryExpression.Left, out var newLeft) | TryRewriteExpression(orBinaryExpression.Right, out var newRight))
            {
                return new OrBinaryExpression(newLeft, newRight);
            }

            return orBinaryExpression;
        }

        protected internal override Expression VisitSubtractBinaryExpression(SubtractBinaryExpression subtractBinaryExpression)
        {
            if (TryRewriteExpression(subtractBinaryExpression.Left, out var newLeft) | TryRewriteExpression(subtractBinaryExpression.Right, out var newRight))
            {
                return new SubtractBinaryExpression(newLeft, newRight);
            }

            return subtractBinaryExpression;
        }

        protected internal override Expression VisitStartsWithBinaryExpression(StartsWithBinaryExpression startsWithBinaryExpression)
        {
            if (TryRewriteExpression(startsWithBinaryExpression.Left, out var newLeft) | TryRewriteExpression(startsWithBinaryExpression.Right, out var newRight))
            {
                return new StartsWithBinaryExpression(newLeft, newRight);
            }

            return startsWithBinaryExpression;
        }

        protected internal override Statement VisitBreakStatement(BreakStatement breakStatement)
        {
            return breakStatement;
        }

        protected internal override Statement VisitCallbackStatement(CallbackStatement callbackStatement)
        {
            return callbackStatement;
        }

        protected internal override Statement VisitCaptureStatement(CaptureStatement captureStatement)
        {
            if (TryRewriteStatements(captureStatement.Statements, out var newStatements))
            {
                return new CaptureStatement(captureStatement.Identifier, newStatements.ToList());
            }

            return captureStatement;
        }

        protected internal override Statement VisitCaseStatement(CaseStatement caseStatement)
        {
            var hasChanges = false;
            var newBlocks = new List<CaseBlock>();
            
            if (TryRewriteExpression(caseStatement.Expression, out var newExpression))
            {
                hasChanges = true;
            }
            else
            {
                newExpression = caseStatement.Expression;
            }

            foreach (var block in caseStatement.Blocks)
            {
                if (block is WhenBlock whenBlock)
                {
                    var optionsChanged = TryRewriteExpressions(whenBlock.Options, out var newOptions);
                    var statementsChanged = TryRewriteStatements(whenBlock.Statements, out var newStatements);
                    
                    if (optionsChanged || statementsChanged)
                    {
                        hasChanges = true;
                        newBlocks.Add(new WhenBlock(newOptions.ToArray(), newStatements.ToArray()));
                    }
                    else
                    {
                        newBlocks.Add(block);
                    }
                }
                else if (block is ElseBlock elseBlock)
                {
                    if (TryRewriteStatements(elseBlock.Statements, out var newStatements))
                    {
                        hasChanges = true;
                        newBlocks.Add(new ElseBlock(newStatements.ToArray()));
                    }
                    else
                    {
                        newBlocks.Add(block);
                    }
                }
            }

            if (hasChanges)
            {
                return new CaseStatement(newExpression, newBlocks.ToArray());
            }

            return caseStatement;
        }

        protected internal override Statement VisitCommentStatement(CommentStatement commentStatement)
        {
            return commentStatement;
        }

        protected internal override Statement VisitContinueStatement(ContinueStatement continueStatement)
        {
            return continueStatement;
        }

        protected internal override Statement VisitCycleStatement(CycleStatement cycleStatement)
        {
            if (TryRewriteExpression(cycleStatement.Group, out var newGroup) | TryRewriteExpressions(cycleStatement.Values, out var newValues))
            {
                return new CycleStatement(newGroup, newValues.ToArray());
            }

            return cycleStatement;
        }

        protected internal override Statement VisitDecrementStatement(DecrementStatement decrementStatement)
        {
            return decrementStatement;
        }

        protected internal override Statement VisitElseIfStatement(ElseIfStatement elseIfStatement)
        {
            if (TryRewriteExpression(elseIfStatement.Condition, out var newCondition) | TryRewriteStatements(elseIfStatement.Statements, out var newStatements))
            {
                return new ElseIfStatement(newCondition, newStatements.ToList());
            }

            return elseIfStatement;
        }

        protected internal override Statement VisitElseStatement(ElseStatement elseStatement)
        {
            if (TryRewriteStatements(elseStatement.Statements, out var newStatements))
            {
                return new ElseStatement(newStatements.ToList());
            }

            return elseStatement;
        }

        protected internal override Statement VisitEmptyBlockStatement(EmptyBlockStatement emptyBlockStatement)
        {
            if (TryRewriteStatements(emptyBlockStatement.Statements, out var newStatements))
            {
                return new EmptyBlockStatement(emptyBlockStatement.TagName, newStatements.ToList(), emptyBlockStatement.Render);
            }

            return emptyBlockStatement;
        }

        protected internal override Statement VisitEmptyTagStatement(EmptyTagStatement emptyTagStatement)
        {
            return emptyTagStatement;
        }

        protected internal override Expression VisitFilterExpression(FilterExpression filterExpression)
        {
            var updated = false;
            var newParameters = new List<FilterArgument>(filterExpression.Parameters.Count);

            foreach (var parameter in filterExpression.Parameters)
            {
                if (TryRewriteExpression(parameter.Expression, out var newExpression))
                {
                    updated = true;
                }

                newParameters.Add(new FilterArgument(parameter.Name, newExpression));
            }

            if (updated | TryRewriteExpression(filterExpression.Input, out var newInputExpression))
            {
                return new FilterExpression(newInputExpression, filterExpression.Name, newParameters);
            }

            return filterExpression;
        }

        protected internal override Statement VisitForStatement(ForStatement forStatement)
        {
            if (TryRewriteExpression(forStatement.Source, out var newSource) |
                TryRewriteExpression(forStatement.Limit, out var newLimit) |
                TryRewriteExpression(forStatement.Offset, out var newOffset) |
                TryRewriteStatements(forStatement.Statements, out var newStatements) |
                TryRewriteStatement(forStatement.Else, out var newElse))
            {
                return new ForStatement(newStatements.ToList(), forStatement.Identifier, newSource, newLimit, newOffset, forStatement.Reversed, newElse);
            }

            return forStatement;
        }

        protected internal override Statement VisitFromStatement(FromStatement fromStatement)
        {
            if (TryRewriteExpression(fromStatement.Path, out var newPath))
            {
                return new FromStatement(fromStatement.Parser, newPath, fromStatement.Functions);
            }

            return fromStatement;
        }

        protected internal override Statement VisitIfStatement(IfStatement ifStatement)
        {
            if (TryRewriteExpression(ifStatement.Condition, out var newCondition) |
                TryRewriteStatement(ifStatement.Else, out var newElse) |
                TryRewriteStatements(ifStatement.Statements, out var newStatements) |
                TryRewriteStatements(ifStatement.ElseIfs, out var newElseIfs))
            {
                return new IfStatement(newCondition, newStatements.ToList(), newElse, newElseIfs.ToList());
            }

            return ifStatement;
        }

        protected internal override Statement VisitIncludeStatement(IncludeStatement includeStatement)
        {
            if (TryRewriteExpression(includeStatement.For, out var newFor) |
                TryRewriteStatements(includeStatement.AssignStatements, out var newAssignStatements) |
                TryRewriteExpression(includeStatement.With, out var newWith) |
                TryRewriteExpression(includeStatement.Path, out var newPath))
            {
                return new IncludeStatement(includeStatement.Parser, newPath, newWith, newFor, includeStatement.Alias, newAssignStatements.ToList());
            }

            return includeStatement;
        }

        protected internal override Statement VisitIncrementStatement(IncrementStatement incrementStatement)
        {
            return incrementStatement;
        }

        protected internal override Statement VisitLiquidStatement(LiquidStatement liquidStatement)
        {
            if (TryRewriteStatements(liquidStatement.Statements, out var newStatements))
            {
                return new LiquidStatement(newStatements.ToList());
            }

            return liquidStatement;
        }

        protected internal override Expression VisitLiteralExpression(LiteralExpression literalExpression)
        {
            return literalExpression;
        }

        protected internal override Statement VisitMacroStatement(MacroStatement macroStatement)
        {
            var updated = false;
            var newArguments = new List<FunctionCallArgument>(macroStatement.Arguments.Count);

            foreach (var argument in macroStatement.Arguments)
            {
                if (TryRewriteExpression(argument.Expression, out var newExpression))
                {
                    updated = true;
                }

                newArguments.Add(new FunctionCallArgument(argument.Name, newExpression));
            }

            if (updated | TryRewriteStatements(macroStatement.Statements, out var newStatements))
            {
                return new MacroStatement(macroStatement.Identifier, newArguments, newStatements.ToList());
            }

            return macroStatement;
        }

        protected internal override Expression VisitMemberExpression(MemberExpression memberExpression)
        {
            return memberExpression;
        }

        protected internal override Statement VisitNoOpStatement(NoOpStatement noOpStatement)
        {
            return noOpStatement;
        }

        protected internal override Statement VisitOutputStatement(OutputStatement outputStatement)
        {
            if (TryRewriteExpression(outputStatement.Expression, out var newExpression))
            {
                return new OutputStatement(newExpression);
            }

            return outputStatement;
        }

        protected internal override Statement VisitParserBlockStatement<T>(ParserBlockStatement<T> parserBlockStatement)
        {
            if (TryRewriteStatements(parserBlockStatement.Statements, out var newStatements))
            {
                return new ParserBlockStatement<T>(parserBlockStatement.TagName, parserBlockStatement.Value, newStatements.ToList(), parserBlockStatement.Render);
            }

            return parserBlockStatement;
        }

        protected internal override Statement VisitParserTagStatement<T>(ParserTagStatement<T> parserTagStatement)
        {
            return parserTagStatement;
        }

        protected internal override Expression VisitRangeExpression(RangeExpression rangeExpression)
        {
            if (TryRewriteExpression(rangeExpression.From, out var newFrom) |
                TryRewriteExpression(rangeExpression.To, out var newTo))
            {
                return new RangeExpression(newFrom, newTo);
            }

            return rangeExpression;
        }

        protected internal override Statement VisitRawStatement(RawStatement rawStatement)
        {
            return rawStatement;
        }

        protected internal override Statement VisitRenderStatement(RenderStatement renderStatement)
        {
            if (TryRewriteExpression(renderStatement.With, out var newWith) |
                TryRewriteExpression(renderStatement.For, out var newFor) |
                TryRewriteStatements(renderStatement.AssignStatements, out var newAssignStatements))
            {
                return new RenderStatement(renderStatement.Parser, renderStatement.Path, newWith, newFor, renderStatement.Alias, newAssignStatements.ToList());
            }

            return renderStatement;
        }

        protected internal override Statement VisitTextSpanStatement(TextSpanStatement textSpanStatement)
        {
            return textSpanStatement;
        }

        protected internal override Statement VisitUnlessStatement(UnlessStatement unlessStatement)
        {
            var rewriteElseIfs = TryRewriteStatements<ElseIfStatement>(unlessStatement.ElseIfs, out var newElseIfs);

            if (TryRewriteExpression(unlessStatement.Condition, out var newCondition) |
                TryRewriteStatement(unlessStatement.Else, out var newElse) |
                TryRewriteStatements(unlessStatement.Statements, out var newStatements) |
                rewriteElseIfs)
            {
                return new UnlessStatement(newCondition, newStatements.ToList(), newElse, newElseIfs?.ToList());
            }
            return unlessStatement;
        }

        protected internal override Statement VisitWhenStatement(WhenStatement whenStatement)
        {
            if (TryRewriteExpressions(whenStatement.Options, out var newOptions) |
                TryRewriteStatements(whenStatement.Statements, out var newStatements))
            {
                return new WhenStatement(newOptions, newStatements.ToList());
            }

            return whenStatement;
        }
    }
}
