using System.Runtime.CompilerServices;
using Fluid.Values;

namespace Fluid;

/// <summary>
/// Represents the list of arguments of a function.
/// </summary>
public sealed class FunctionArguments
{
    public static readonly FunctionArguments Empty = new FunctionArguments();

    private List<FluidValue> _positional;
    private Dictionary<string, FluidValue> _named;

    // The position of the first named argument, or -1 when every argument is positional.
    private int _firstNamedIndex = -1;

    public int Count => _positional?.Count ?? 0;

    /// <summary>
    /// Gets the number of arguments given without a name before the first named one. Named arguments
    /// are also stored in order, so <see cref="At(int)"/> below this count reads a positional one.
    /// </summary>
    public int PositionalCount
    {
        get
        {
            if (_firstNamedIndex == -1)
            {
                return Count;
            }

            return _firstNamedIndex;
        }
    }

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
            if (_named != null && _named.TryGetValue(name, out var value))
            {
                return value;
            }

            return NilValue.Instance;
        }
    }

    public FunctionArguments()
    {
    }

    public FunctionArguments(params FluidValue[] values)
    {
        _positional = new List<FluidValue>(values);
    }

    public FunctionArguments Add(FluidValue value)
    {
        return Add(null, value);
    }

    public FunctionArguments Add(string name, FluidValue value)
    {
        if (name != null)
        {
            _named ??= new Dictionary<string, FluidValue>();

            _named.Add(name, value);

            if (_firstNamedIndex == -1)
            {
                _firstNamedIndex = Count;
            }
        }

        _positional ??= new List<FluidValue>();

        _positional.Add(value);

        return this;
    }

    public IEnumerable<string> Names => _named?.Keys ?? Enumerable.Empty<string>();

    public IEnumerable<FluidValue> Values => _positional;

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
