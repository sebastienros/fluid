using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class DictionaryValue : FluidValue
    {
        private readonly IFluidIndexable _value;

        public DictionaryValue(IFluidIndexable value)
        {
            _value = value;
        }

        public override FluidValues Type => FluidValues.Dictionary;

        public override bool Equals(FluidValue other)
        {
            if (other.IsNil())
            {
                return _value.Count == 0;
            }

            if (other is DictionaryValue otherDictionary)
            {
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
            if (name == "size")
            {
                return NumberValue.Create(_value.Count);
            }

            // Check if the actual property exists first before using synthetic first/last
            if (_value.TryGetValue(name, out var fluidValue))
            {
                return fluidValue;
            }

            // Only use synthetic first/last if the property doesn't exist
            if (name == "first" && _value.Count > 0)
            {
                var firstKey = _value.Keys.First();
                _value.TryGetValue(firstKey, out var firstValue);
                return new ArrayValue(new[] { new StringValue(firstKey), firstValue });
            }

            if (name == "last" && _value.Count > 0)
            {
                var lastKey = _value.Keys.Last();
                _value.TryGetValue(lastKey, out var lastValue);
                return new ArrayValue(new[] { new StringValue(lastKey), lastValue });
            }

            return NilValue.Instance;
        }

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            var name = index.ToStringValue();

            if (!_value.TryGetValue(name, out var value))
            {
                return NilValue.Instance;
            }

            return value;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return 0;
        }

        public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
        {
            return default;
        }

        public override string ToStringValue()
        {
            if (_value.Count == 0)
            {
                return "{}";
            }
            
            var items = new List<string>();
            foreach (var key in _value.Keys)
            {
                if (_value.TryGetValue(key, out var value))
                {
                    items.Add($"\"{key}\":{value.ToStringValue()}");
                }
            }
            return "{" + string.Join(",", items) + "}";
        }

        public override object ToObjectValue()
        {
            return _value;
        }

        public override ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
        {
            foreach (var key in _value.Keys)
            {
                if (_value.TryGetValue(key, out var item) && item.Equals(value.ToObjectValue(context)))
                {
                    return new ValueTask<bool>(true);
                }
            }

            return new ValueTask<bool>(false);
        }

        public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
        {
            foreach (var key in _value.Keys)
            {
                _value.TryGetValue(key, out var value);
                yield return new ArrayValue([new StringValue(key), value]);
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
}
