namespace Fluid.Ast
{
    internal static class StatementListHelper
    {
        internal static bool IsWhitespaceOrCommentOnly(IReadOnlyList<Statement> statements)
        {
            for (var i = 0; i < statements.Count; i++)
            {
                switch (statements[i])
                {
                    case TextSpanStatement t:
#if NET6_0_OR_GREATER
                        if (!t.Text.Span.IsWhiteSpace())
#else
                        if (!string.IsNullOrWhiteSpace(t.Text.ToString()))
#endif
                        {
                            return false;
                        }
                        break;

                    case CommentStatement:
                    case AssignStatement:
                    case IncrementStatement:
                    case DecrementStatement:
                    case CycleStatement:
                        break;

                    case IfStatement ifStatement:
                        {
                            if (!IsWhitespaceOrCommentOnly(ifStatement.Statements)) return false;
                            if (ifStatement.Else != null && !IsWhitespaceOrCommentOnly(ifStatement.Else.Statements)) return false;
                            foreach (var elseIf in ifStatement.ElseIfs)
                            {
                                if (!IsWhitespaceOrCommentOnly(elseIf.Statements)) return false;
                            }
                            break;
                        }

                    case UnlessStatement unlessStatement:
                        {
                            if (!IsWhitespaceOrCommentOnly(unlessStatement.Statements)) return false;
                            if (unlessStatement.Else != null && !IsWhitespaceOrCommentOnly(unlessStatement.Else.Statements)) return false;
                            foreach (var elseIf in unlessStatement.ElseIfs)
                            {
                                if (!IsWhitespaceOrCommentOnly(elseIf.Statements)) return false;
                            }
                            break;
                        }

                    case CaseStatement caseStatement:
                        {
                            foreach (var block in caseStatement.Blocks)
                            {
                                if (!IsWhitespaceOrCommentOnly(block.Statements)) return false;
                            }
                            break;
                        }

                    default:
                        return false;
                }
            }

            return true;
        }
    }
}
