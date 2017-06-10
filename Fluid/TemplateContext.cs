using System;
using System.Collections.Generic;
using Fluid.Ast.Values;

namespace Fluid
{
    public class TemplateContext
    {
        public static Scope GlobalScope = new Scope();

        public Dictionary<string, FilterDelegate> Filters { get; } = new Dictionary<string, FilterDelegate>();
        public static Dictionary<string, FilterDelegate> GlobalFilters { get; } = new Dictionary<string, FilterDelegate>();

        protected Scope _scope;
        public Scope LocalScope { get; private set; }

        static TemplateContext()
        {
            // Global properties
            GlobalScope.SetValue("empty", EmptyValue.Instance);

            // Initialize Global Filters
        }

        public TemplateContext()
        {
            _scope = new Scope(GlobalScope);
            LocalScope = _scope;
        }

        public Scope EnterChildScope()
        {
            return LocalScope = LocalScope.EnterChildScope();
        }

        public void ReleaseScope()
        {
            LocalScope = LocalScope.ReleaseScope();

            if (LocalScope == null)
            {
                throw new InvalidOperationException();
            }
        }

        public FluidValue GetValue(string name)
        {
            return _scope.GetValue(name);
        }

        public void SetValue(string name, FluidValue value)
        {
            _scope.SetValue(name, value);
        }

        public void SetValue(string name, int value)
        {
            SetValue(name, new NumberValue(value));
        }

        public void SetValue(string name, string value)
        {
            SetValue(name, new StringValue(value));
        }

        public void SetValue(string name, bool value)
        {
            SetValue(name, new BooleanValue(value));
        }

        public void SetValue(string name, object value)
        {
            SetValue(name, new ObjectValue(value));
        }
    }
}
