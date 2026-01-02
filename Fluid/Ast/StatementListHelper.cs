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
                        break;

                    default:
                        return false;
                }
            }

            return true;
        }
    }
}
