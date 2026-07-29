using System.Runtime.CompilerServices;
using Fluid.Values;

namespace Fluid
{
    public sealed class Scope
    {
        // Most scopes created while rendering hold only a handful of names: a for loop scope holds the
        // loop variable, `forloop` and optionally `parentloop`. Keeping those in inline slots avoids
        // allocating a Dictionary per scope, and replaces hashing the name on every read with a short
        // scan that usually resolves on the first reference comparison, because the name comes from the
        // same AST node every time. The Dictionary is only materialized once the slots overflow.
        // Three, because that is what the motivating case holds: the loop variable, `forloop`, and
        // `parentloop` when loops nest. Slots only cost on a miss -- every occupied one is a comparison
        // before falling through to the parent -- so there is no reason to carry more than that.
        private const int InlineCapacity = 3;

        private string _name0, _name1, _name2;
        private FluidValue _value0, _value1, _value2;
        private int _inlineCount;

        private Dictionary<string, FluidValue> _properties;

        // The comparer this scope's storage uses, or null while the scope is still empty. Mirrors the
        // previous behaviour of creating the dictionary with the parent's comparer when it has one.
        private StringComparer _storageComparer;

        // Both built-in comparers can be compared without a virtual call, which matters most on a miss,
        // where every occupied slot is compared before moving to the parent scope. OrdinalIgnoreCase is
        // what TemplateOptions.ModelNamesComparer defaults to, and Ordinal is what a scope falls back to
        // when nothing in its chain sets one, so between them they cover the common configurations.
        private StringComparison _storageComparison;
        private bool _storageComparisonIsDirect;

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
            if (forLoopScope) ArgumentNullException.ThrowIfNull(parent);

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
        /// <remarks>
        /// A snapshot, so that enumerating it behaves the same whether the scope is small enough to use
        /// the inline slots or has spilled into a dictionary.
        /// </remarks>
        public IEnumerable<string> Properties
        {
            get
            {
                if (_properties != null)
                {
                    var keys = new string[_properties.Count];
                    _properties.Keys.CopyTo(keys, 0);
                    return keys;
                }

                if (_inlineCount == 0)
                {
                    return Array.Empty<string>();
                }

                var names = new string[_inlineCount];
                for (var i = 0; i < names.Length; i++)
                {
                    names[i] = GetInlineName(i);
                }

                return names;
            }
        }

        /// <summary>
        /// Gets the parent scope if any.
        /// </summary>
        public Scope Parent { get; private set; }

        /// <summary>
        /// Returns the value with the specified name in the chain of scopes, or undefined
        /// if it doesn't exist.
        /// </summary>
        /// <param name="name">The name of the value to return.</param>
        public FluidValue GetValue(string name)
        {
            ArgumentNullException.ThrowIfNull(name);

            var scope = this;

            do
            {
                if (scope.TryGetOwnValue(name, out var result))
                {
                    return result;
                }

                scope = scope.Parent;
            }
            while (scope != null);

            return UndefinedValue.Instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetOwnValue(string name, out FluidValue value)
        {
            var count = _inlineCount;

            if (count != 0)
            {
                if (NameEquals(name, _name0)) { value = _value0; return true; }
                if (count > 1 && NameEquals(name, _name1)) { value = _value1; return true; }
                if (count > 2 && NameEquals(name, _name2)) { value = _value2; return true; }

                value = null;
                return false;
            }

            if (_properties != null)
            {
                return _properties.TryGetValue(name, out value);
            }

            value = null;
            return false;
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
        public void DeleteOwn(string name)
        {
            if (_properties != null)
            {
                _properties.Remove(name);
                return;
            }

            if (_storageComparer != null)
            {
                // Once initialized, the dictionary-backed implementation rejected null keys even when
                // the scope was empty after deleting its last value.
                ArgumentNullException.ThrowIfNull(name);
            }

            for (var i = 0; i < _inlineCount; i++)
            {
                var slotName = GetInlineName(i);

                if (NameEquals(name, slotName))
                {
                    // Compact the slots so the scan in TryGetOwnValue can stop at _inlineCount.
                    for (var j = i; j < _inlineCount - 1; j++)
                    {
                        SetInline(j, GetInlineName(j + 1), GetInlineValue(j + 1));
                    }

                    SetInline(_inlineCount - 1, null, null);
                    _inlineCount--;
                    return;
                }
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
        public void SetOwnValue(string name, FluidValue value)
        {
            // The Dictionary this used to delegate to rejected a null key, and callers relied on being
            // told at the offending call rather than when the name later surfaced somewhere else.
            ArgumentNullException.ThrowIfNull(name);

            value ??= NilValue.Instance;

            if (_properties != null)
            {
                _properties[name] = value;
                return;
            }

            var comparer = _storageComparer;

            if (comparer is null)
            {
                _storageComparer = comparer = Parent?._storageComparer ?? _stringComparer;

                if (ReferenceEquals(comparer, StringComparer.Ordinal))
                {
                    _storageComparison = StringComparison.Ordinal;
                    _storageComparisonIsDirect = true;
                }
                else if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase))
                {
                    _storageComparison = StringComparison.OrdinalIgnoreCase;
                    _storageComparisonIsDirect = true;
                }
            }

            for (var i = 0; i < _inlineCount; i++)
            {
                if (NameEquals(name, GetInlineName(i)))
                {
                    // Only the value changes; the slot already holds an equal name.
                    SetInlineValue(i, value);
                    return;
                }
            }

            if (_inlineCount < InlineCapacity)
            {
                SetInline(_inlineCount, name, value);
                _inlineCount++;
                return;
            }

            // Overflow: switch to a dictionary and keep using it from now on. Publish the dictionary
            // before retiring the slots, and leave the slot fields populated -- a reader that still sees
            // the old _inlineCount then resolves from slots that are still valid, instead of finding
            // neither store and reporting a name that is present as undefined.
            var properties = new Dictionary<string, FluidValue>(comparer);
            for (var i = 0; i < _inlineCount; i++)
            {
                properties[GetInlineName(i)] = GetInlineValue(i);
            }

            properties[name] = value;

            _properties = properties;
            _inlineCount = 0;
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

                return;
            }

            for (var i = 0; i < _inlineCount; i++)
            {
                scope.SetValue(GetInlineName(i), GetInlineValue(i));
            }
        }

        /// <summary>
        /// Compares a looked-up name against the name held in a slot, under this scope's comparer.
        /// Reference equality short-circuits only when the caller happens to pass the very string the
        /// value was stored with; the parser allocates a separate string per identifier occurrence, so
        /// reading a loop variable in the body does not hit that and falls through to the comparison.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool NameEquals(string name, string slotName)
        {
            if (ReferenceEquals(name, slotName))
            {
                return true;
            }

            if (slotName is null)
            {
                return false;
            }

            return _storageComparisonIsDirect
                ? string.Equals(name, slotName, _storageComparison)
                : _storageComparer.Equals(name, slotName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetInlineName(int index) => index switch
        {
            0 => _name0,
            1 => _name1,
            _ => _name2,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FluidValue GetInlineValue(int index) => index switch
        {
            0 => _value0,
            1 => _value1,
            _ => _value2,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetInline(int index, string name, FluidValue value)
        {
            switch (index)
            {
                case 0: _name0 = name; _value0 = value; break;
                case 1: _name1 = name; _value1 = value; break;
                default: _name2 = name; _value2 = value; break;
            }
        }

        /// <summary>
        /// Overwrites the value of an occupied slot. The loop variable is rewritten once per iteration,
        /// so this avoids re-storing a name the slot already holds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetInlineValue(int index, FluidValue value)
        {
            switch (index)
            {
                case 0: _value0 = value; break;
                case 1: _value1 = value; break;
                default: _value2 = value; break;
            }
        }
    }
}
