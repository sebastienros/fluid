using System.Collections.Generic;

namespace Fluid.Ast
{
    public class NamedExpressionList
    {
        public static readonly NamedExpressionList Empty = new NamedExpressionList();

        private List<Expression> _positional;
        private Dictionary<string, Expression> _named;

        public int Count => _positional?.Count ?? 0;

        public bool HasNamed(string name)
        {
            return _named != null && _named.ContainsKey(name);
        }

        public Expression this[int index]
        {
            get
            {
                if (_positional == null || index >= _positional.Count)
                {
                    return null;
                }

                return _positional[index];
            }
        }

        public Expression this[string name]
        {
            get
            {
                if (_named != null && _named.TryGetValue(name, out var value))
                {
                    return value;
                }

                return null;
            }
        }

        public Expression this[string name, int index]
        {
            get
            {
                return this[name] ?? this[index];
            }
        }

        public NamedExpressionList()
        {
        }

        public NamedExpressionList(params Expression[] values)
        {
            _positional = new List<Expression>(values);
        }

        public NamedExpressionList(List<FilterArgument> arguments)
        {
            foreach (var argument in arguments)
            {
                Add(argument.Name, argument.Expression);
            }
        }

        public NamedExpressionList Add(string name, Expression value)
        {
            if (name != null)
            {
                if (_named == null)
                {
                    _named = new Dictionary<string, Expression>();
                }

                _named.Add(name, value);
            }

            if (_positional == null)
            {
                _positional = new List<Expression>();
            }

            _positional.Add(value);

            return this;
        }

        public IEnumerable<string> Names => _named?.Keys ?? System.Linq.Enumerable.Empty<string>();

        public IEnumerable<Expression> Values => _positional;
    }
}
