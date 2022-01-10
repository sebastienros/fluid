using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Fluid.Values;

namespace Fluid
{
    public class Scope
    {
        internal Dictionary<string, FluidValue> _properties;
        private readonly bool _forLoopScope;

        public Scope()
        {
            Parent = null;
        }

        public Scope(Scope parent)
        {
            Parent = parent;
        }

        public Scope(Scope parent, bool forLoopScope)
        {
            if (forLoopScope && parent == null) ExceptionHelper.ThrowArgumentNullException(nameof(parent));

            Parent = parent;

            // A ForLoop scope reads and writes its values in the parent scope.
            // Internal accessors to the inner properties grant access to the local properties.
            _forLoopScope = forLoopScope;

            _properties = new Dictionary<string, FluidValue>();
        }

        /// <summary>
        /// Gets the own properties of the scope
        /// </summary>
        public IEnumerable<string> Properties => _properties == null ? Array.Empty<string>() : _properties.Keys;

        /// <summary>
        /// Gets the parent scope if any.
        /// </summary>
        public Scope Parent { get; private set; }

        /// <summary>
        /// Returns the value with the specified name in the chain of scopes, or undefined
        /// if it doesn't exist.
        /// </summary>
        /// <param name="name">The name of the value to return.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluidValue GetValue(string name)
        {
            if (name == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(name));
            }

            if (_properties != null && _properties.TryGetValue(name, out var result))
            {
                return result;
            }

            return Parent != null
                ? Parent.GetValue(name)
                : NilValue.Instance;
        }

        public void Delete(string name)
        {
            if (_properties != null)
            {
                if (!_forLoopScope)
                {
                    _properties.Remove(name);
                }
                else
                {
                    Parent.Delete(name);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(string name, FluidValue value)
        {
            if (!_forLoopScope)
            {
                _properties ??= new Dictionary<string, FluidValue>();

                _properties[name] = value ?? NilValue.Instance;
            }
            else
            {
                Parent.SetValue(name, value);
            }
        }

        public FluidValue GetIndex(FluidValue index)
        {
            return GetValue(index.ToString());
        }

        public void CopyTo(Scope scope)
        {
            if (_properties != null)
            {
                foreach (var property in _properties)
                {
                    scope.SetValue(property.Key, property.Value);
                }
            }
        }
    }
}
