namespace Fluid.Parser
{
    public static class ErrorMessages
    {
        public const string EqualAfterAssignIdentifier = "'=' was expected after the identifier of 'assign'";
        public const string IdentifierAfterAssign = "An identifier was expected after the 'assign' tag";
        public const string IdentifierAfterTagStart = "An identifier was expected after '{%'";
        public const string LogicalExpressionStartsFilter = "A value was expected";
        public const string IdentifierAfterPipe = "An identifier was expected after the '|' sign";
        public const string ExpectedTagEnd = "End of tag '%}' was expected";
        public const string ExpectedOutputEnd = "End of tag '}}' was expected";
        public const string ExpectedStringRender = "A quoted string value is required for the render tag";
        public const string FunctionsNotAllowed = "Functions are not allowed. To enable the feature use the 'AllowFunctions' option.";
        public const string ParenthesesNotAllowed = "Parentheses are not allowed in order to group expressions. To enable the feature use the 'AllowParentheses' option.";
        [Obsolete("Error no longer used")] public const string IdentifierAfterMacro = "An identifier was expected after the 'macro' tag";
        public const string IdentifierAfterTag = "An identifier was expected after the '{0}' tag";
        public const string ParenthesesAfterFunctionName = "Start of arguments '(' is expected after a function name";
    }
}
