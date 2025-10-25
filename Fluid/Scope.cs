using Fluid.Values;
using System.Runtime.CompilerServices;

namespace Fluid
{
    public sealed class Scope
    {
        private Dictionary<string, FluidValue> _properties;
        private readonly bool _forLoopScope;
        private readonly StringComparer _stringComparer;

        public Scope() : this(null, false, null)
        {
        }

        public Scope(Scope parent) : this(parent, false, null)
        {
        }

        public Scope(Scope parent, bool forLoopScope, StringComparer stringComparer = null)
        {
            if (forLoopScope && parent == null) ExceptionHelper.ThrowArgumentNullException(nameof(parent));

            // For loops are also ordinal by default
            _stringComparer = stringComparer ?? StringComparer.Ordinal;

            Parent = parent;

            // A ForLoop scope reads and writes its values in the parent scope.
            // Internal accessors to the inner properties grant access to the local properties.
            _forLoopScope = forLoopScope;
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
        /// <param name="context">The optional template context for tracking missing variables.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FluidValue GetValue(string name, TemplateContext context = null)
        {
            if (name == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(name));
            }

            if (_properties != null && _properties.TryGetValue(name, out var result))
            {
                return result;
            }

            if (Parent != null)
            {
                return Parent.GetValue(name, context);
            }

            // Track missing variable if StrictVariables enabled
            if (context?.Options.StrictVariables == true)
            {
                context.TrackMissingVariable(name);
            }

            return NilValue.Instance;
        }

        /// <summary>
        /// Deletes the value with the specified name in the chain of scopes.
        /// </summary>
        /// <param name="name">The name of the value to delete.</param>
        public void Delete(string name)
        {
            if (!_forLoopScope)
            {
                DeleteOwn(name);
            }
            else
            {
                Parent.Delete(name);
            }
        }

        /// <summary>
        /// Deletes the value with the specified name in the current scopes.
        /// </summary>
        /// <param name="name">The name of the value to delete.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeleteOwn(string name)
        {
            if (_properties != null)
            {
                _properties.Remove(name);
            }
        }

        /// <summary>
        /// Sets the value with the specified name in the chain of scopes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(string name, FluidValue value)
        {
            if (!_forLoopScope)
            {
                SetOwnValue(name, value);
            }
            else
            {
                Parent.SetValue(name, value);
            }
        }

        /// <summary>
        /// Sets the value with the specified name in the current scope.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetOwnValue(string name, FluidValue value)
        {
            _properties ??= new Dictionary<string, FluidValue>(Parent?._properties?.Comparer ?? _stringComparer);
            _properties[name] = value ?? NilValue.Instance;
        }

        public FluidValue GetIndex(FluidValue index)
        {
            return GetValue(index.ToString());
        }

        /// <summary>
        /// Copies all the local scope properties to a different one.
        /// </summary>
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
