namespace Fluid.Ast
{
    public readonly struct FilterArgument
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
