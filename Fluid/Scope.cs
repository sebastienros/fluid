using System;
using System.Collections.Generic;
using Fluid.Values;

namespace Fluid
{
    public class Scope
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
        public FluidValue GetValue(string name)
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
                    return _parent.GetValue(name);
                }
            }

            return UndefinedValue.Instance;
        }

        public void SetValue(string name, FluidValue value)
        {
            _properties[name] = value;
        }

        public void SetValue(string name, object value)
        {
            _properties[name] = FluidValue.Create(value);
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
            return GetValue(index.ToString());
        }
    }
}
