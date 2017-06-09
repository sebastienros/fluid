using System;
using System.Collections.Generic;
using Fluid.Ast.Values;

namespace Fluid
{
    public class TemplateContext
    {
        public Scope Scope { get; private set; } = new Scope();

        public Dictionary<string, FilterDelegate> Filters { get; } = new Dictionary<string, FilterDelegate>();
        public static Dictionary<string, FilterDelegate> GlobalFilters { get; } = new Dictionary<string, FilterDelegate>();

        static TemplateContext()
        {
            // Initialize Global Filters
        }

        public void EnterChildScope()
        {
            Scope = Scope.EnterChildScope();
        }

        public void ReleaseScope()
        {
            Scope = Scope.ReleaseScope();

            if (Scope == null)
            {
                throw new InvalidOperationException();
            }
        }

        public void SetValue(string name, FluidValue value)
        {
            Scope.SetProperty(name, value);
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
