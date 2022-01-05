namespace Fluid.Ast
{
    public readonly struct FunctionCallArgument
    {
        public FunctionCallArgument(string name, Expression expression)
        {
            Name = name;
            Expression = expression;
        }

        /// <summary>
        /// Gets the name of the argument, or <c>null</c> if not defined.
        /// </summary>
        public string Name { get; }

        public Expression Expression { get; }
    }
}
