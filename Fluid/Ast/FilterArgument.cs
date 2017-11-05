namespace Fluid.Ast
{
    public struct FilterArgument
    {
        public FilterArgument(string name, Expression expression)
        {
            Name = name;
            Expression = expression;
        }

        public string Name { get; }

        public Expression Expression { get; }
    }
}
