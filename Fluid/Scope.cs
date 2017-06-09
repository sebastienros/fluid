using System;
using System.Collections.Generic;
using Fluid.Ast.Values;

namespace Fluid
{
    public class Scope : INamedSet
    {
        private Dictionary<string, FluidValue> _properties = new Dictionary<string, FluidValue>();
        private readonly Scope _parent;

        public Scope()
        {
            _parent = null;
        }

        public Scope(Scope parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Returns the value with the specified name in the chain of scopes, or undefined 
        /// if it doesn't exist.
        /// </summary>
        /// <param name="name">The name of the value to return.</param>
        public FluidValue GetProperty(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (_properties.TryGetValue(name, out var result))
            {
                return result;
            }
            else
            {
                if (_parent != null)
                {
                    return _parent.GetProperty(name);
                }
            }

            return UndefinedValue.Instance;
        }

        public void SetProperty(string name, FluidValue value)
        {
            _properties[name] = value;
        }

        public Scope EnterChildScope()
        {
            return new Scope(this);
        }

        public Scope ReleaseScope()
        {
            return _parent;
        }

        public FluidValue GetIndex(FluidValue index)
        {
            return GetProperty(index.ToString());
        }
    }
}
