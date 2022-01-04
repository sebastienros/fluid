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
        public const string FunctionsNotAllowed = "Functions are not allowed.";
    }
}
