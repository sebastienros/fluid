using System.Text;
using System.Text.Encodings.Web;

namespace Fluid.Values;

public sealed class DictionaryValue : FluidValue
{
    private readonly IFluidIndexable _value;

    public static DictionaryValue Create(IFluidIndexable value) => new DictionaryValue(value);

    private DictionaryValue(IFluidIndexable value)
    {
        _value = value;
    }

    public override FluidValues Type => FluidValues.Dictionary;

    /// <summary>
    /// Gets the number of entries, which doesn't depend on a <see cref="TemplateContext"/>.
    /// </summary>
    internal int Count => _value.Count;

    public override bool Equals(FluidValue other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // An empty hash is equal to empty and blank, but not to nil.
        if (other.Type is FluidValues.Empty or FluidValues.Blank)
        {
            return _value.Count == 0;
        }

        if (other.IsNil())
        {
            return false;
        }

        if (other is DictionaryValue otherDictionary)
        {
            using var scope = RecursiveComparisonGuard.Enter(this, otherDictionary);

            if (_value.Count != otherDictionary._value.Count)
            {
                return false;
            }

            foreach (var key in _value.Keys)
            {
                if (!otherDictionary._value.TryGetValue(key, out var otherItem))
                {
                    return false;
                }

                _value.TryGetValue(key, out var item);

                if (!item.Equals(otherItem))
                {
                    return false;
                }
            }

            return true;
        }
        else if (other.Type == FluidValues.Empty)
        {
            return _value.Count == 0;
        }

        return false;
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        // Check if the actual property exists first before using synthetic properties
        if (_value.TryGetValue(name, out var fluidValue))
        {
            return fluidValue;
        }

        if (name == "size")
        {
            return NumberValue.Create(_value.Count);
        }

        // Only .first is a synthetic property for dictionaries (not .last)
        if (name == "first" && _value.Count > 0)
        {
            var firstKey = _value.Keys.First();
            _value.TryGetValue(firstKey, out var firstValue);
            return ArrayValue.Create(new[] { StringValue.Create(firstKey), firstValue });
        }

        return NilValue.Instance;
    }

    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
    {
        var name = index.ToStringValue(context);

        if (!_value.TryGetValue(name, out var value))
        {
            return NilValue.Instance;
        }

        return value;
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return true;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        // Like Liquid, a value that isn't a number is 0.
        return 0;
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);

        var value = ToStringValue(context);
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        output.Write(encoder, value);
        return default;
    }

    /// <summary>
    /// Like Liquid, a hash is converted to a string in Ruby's inspect format: <c>{"k"=>"v", "n"=>1}</c>.
    /// </summary>
    public override string ToStringValue(TemplateContext context)
    {
        if (_value.Count == 0)
        {
            return "{}";
        }

        var builder = new StringBuilder();
        AppendInspected(builder, context);
        return builder.ToString();
    }

    internal void AppendInspected(StringBuilder builder, TemplateContext context)
    {
        using var scope = RecursiveValueGuard.Enter(this);

        builder.Append('{');

        var first = true;
        foreach (var key in _value.Keys)
        {
            if (_value.TryGetValue(key, out var value))
            {
                if (!first)
                {
                    builder.Append(", ");
                }

                first = false;
                LiquidInspect.AppendString(builder, key);
                builder.Append("=>");
                LiquidInspect.Append(builder, value, context);
            }
        }

        builder.Append('}');
    }

    public override object ToObjectValue(TemplateContext context)
    {
        using var scope = RecursiveValueGuard.Enter(this);
        return _value;
    }

    public override ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
    {
        // Liquid's `contains` on a hash checks its keys (Ruby's Hash#include?), looked up the same way
        // `hash[key]` is.
        return new ValueTask<bool>(_value.TryGetValue(value.ToStringValue(context), out _));
    }

    public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
    {
        foreach (var key in _value.Keys)
        {
            context?.IncrementSteps();
            _value.TryGetValue(key, out var value);
            yield return ArrayValue.Create([StringValue.Create(key), value]);
        }

        await Task.CompletedTask;
    }

    public override bool Equals(object obj)
    {
        // The is operator will return false if null
        if (obj is DictionaryValue otherValue)
        {
            return Equals(otherValue);
        }

        return false;
    }

    public override int GetHashCode()
    {
        using var scope = RecursiveValueGuard.Enter(this);
        var hc = new HashCode();
        foreach (var key in _value.Keys.OrderBy(k => k))
        {
            hc.Add(key);
            if (_value.TryGetValue(key, out var v))
                hc.Add(v);
        }

        return hc.ToHashCode();
    }
}
