using System.Runtime.CompilerServices;
using Fluid.Values;

namespace Fluid;

/// <summary>
/// Represents the list of arguments that are passed to a <see cref="FilterDelegate"/>
/// when invoked.
/// </summary>
public sealed class FilterArguments
{
    public static readonly FilterArguments Empty = new FilterArguments();

    private List<FluidValue> _positional;

    // The position of each named argument in the positional list.
    private Dictionary<string, int> _named;

    // Named arguments are stored with the positional ones too, so the position of the first
    // argument that was given without a name is remembered. -1 when there is none.
    private int _firstPositionalIndex = -1;

    public int Count => _positional?.Count ?? 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FluidValue At(int index)
    {
        if (_positional == null || index >= _positional.Count)
        {
            return NilValue.Instance;
        }

        return _positional[index];
    }

    public bool HasNamed(string name)
    {
        return _named != null && _named.ContainsKey(name);
    }

    public FluidValue this[string name]
    {
        get
        {
            if (_named != null && _named.TryGetValue(name, out var index))
            {
                return _positional[index];
            }

            return NilValue.Instance;
        }
    }

    public FilterArguments()
    {
    }

    public FilterArguments(params FluidValue[] values)
    {
        _positional = new List<FluidValue>(values);

        if (_positional.Count > 0)
        {
            _firstPositionalIndex = 0;
        }
    }

    public FilterArguments Add(FluidValue value)
    {
        return Add(null, value);
    }

    public FilterArguments Add(string name, FluidValue value)
    {
        _positional ??= new List<FluidValue>();

        if (name != null)
        {
            _named ??= new Dictionary<string, int>();

            // Like Liquid, where named arguments are a keyword hash, a repeated name keeps its first position
            // and its last value.
            if (_named.TryGetValue(name, out var index))
            {
                _positional[index] = value;
                return this;
            }

            _named.Add(name, _positional.Count);
        }

        if (name == null && _firstPositionalIndex == -1)
        {
            _firstPositionalIndex = _positional.Count;
        }

        _positional.Add(value);

        return this;
    }

    public IEnumerable<string> Names => _named?.Keys ?? Enumerable.Empty<string>();

    public IEnumerable<FluidValue> Values => _positional;

    /// <summary>
    /// Gets the first argument that was given without a name, whatever its value.
    /// </summary>
    public FluidValue GetFirstPositional()
    {
        if (_firstPositionalIndex == -1)
        {
            return NilValue.Instance;
        }

        return _positional[_firstPositionalIndex];
    }

    internal object[] ValuesToObjectArray(TemplateContext context)
    {
        if (_positional == null || _positional.Count == 0)
        {
            return Array.Empty<object>();
        }

        var array = new object[_positional.Count];
        for (var i = 0; i < array.Length; ++i)
        {
            array[i] = _positional[i].ToObjectValue(context);
        }

        return array;
    }
}
