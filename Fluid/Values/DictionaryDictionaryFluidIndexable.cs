using System;
using System.Collections;

namespace Fluid.Values
{
    public sealed class DictionaryDictionaryFluidIndexable : IFluidIndexable
    {
        private readonly IDictionary _dictionary;
        private readonly TemplateOptions _options;

        public DictionaryDictionaryFluidIndexable(IDictionary dictionary, TemplateOptions options)
        {
            _dictionary = dictionary;
            _options = options;
        }

        public int Count => _dictionary.Count;

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var key in _dictionary.Keys)
                {
                    if (key is string str)
                    {
                        yield return str;
                    }
                    else
                    {
                        yield return ConvertKeyToString(key);
                    }
                }
            }
        }

        public bool TryGetValue(string name, out FluidValue value)
        {
            if (_dictionary.Contains(name))
            {
                var obj = _dictionary[name];
                value = FluidValue.Create(obj, _options);
                return true;
            }

            foreach (var key in _dictionary.Keys)
            {
                if (key is not string && ConvertKeyToString(key) == name)
                {
                    value = FluidValue.Create(_dictionary[key], _options);
                    return true;
                }
            }

            value = NilValue.Instance;
            return false;
        }

        private string ConvertKeyToString(object key)
        {
            return key switch
            {
                IFormattable formattable => formattable.ToString(null, _options.CultureInfo),
                IConvertible convertible => convertible.ToString(_options.CultureInfo),
                _ => key.ToString()
            };
        }
    }
}
