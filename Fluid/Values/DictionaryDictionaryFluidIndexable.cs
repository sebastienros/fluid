using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;

namespace Fluid.Values;

public sealed class DictionaryDictionaryFluidIndexable : IFluidIndexable
{
    // The integer type of the keys for each dictionary type, or TypeCode.Empty when the keys aren't
    // all of one integer type. A wrapper is created every time a dictionary is read from a model, so
    // the interfaces are only inspected once per type.
    private static readonly ConcurrentDictionary<Type, TypeCode> _integerKeyTypes = new();

    private readonly IDictionary _dictionary;
    private readonly TemplateOptions _options;
    private readonly TypeCode _integerKeyType;

    public DictionaryDictionaryFluidIndexable(IDictionary dictionary, TemplateOptions options)
    {
        _dictionary = dictionary;
        _options = options;
        _integerKeyType = _integerKeyTypes.GetOrAdd(dictionary.GetType(), static type => GetIntegerKeyType(type));
    }

    public int Count => _dictionary.Count;

    public IEnumerable<string> Keys
    {
        get
        {
            foreach (var key in _dictionary.Keys)
            {
                // Only handle string keys since this is what TryGetValue returns
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

        if (_integerKeyType != TypeCode.Empty)
        {
            // Every key is an integer of a known type, so the name can be turned back into the one
            // key it could stand for instead of converting each key to a string to compare.
            if (TryConvertNameToIntegerKey(name, out var integerKey) && _dictionary.Contains(integerKey))
            {
                value = FluidValue.Create(_dictionary[integerKey], _options);
                return true;
            }

            value = NilValue.Instance;
            return false;
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

    private bool TryConvertNameToIntegerKey(string name, out object key)
    {
        key = null;

        if (_integerKeyType == TypeCode.UInt64)
        {
            if (!ulong.TryParse(name, NumberStyles.None, _options.CultureInfo, out var unsigned))
            {
                return false;
            }

            key = unsigned;
        }
        else
        {
            if (!long.TryParse(name, NumberStyles.AllowLeadingSign, _options.CultureInfo, out var signed))
            {
                return false;
            }

            switch (_integerKeyType)
            {
                case TypeCode.SByte when signed >= sbyte.MinValue && signed <= sbyte.MaxValue: key = (sbyte)signed; break;
                case TypeCode.Byte when signed >= byte.MinValue && signed <= byte.MaxValue: key = (byte)signed; break;
                case TypeCode.Int16 when signed >= short.MinValue && signed <= short.MaxValue: key = (short)signed; break;
                case TypeCode.UInt16 when signed >= ushort.MinValue && signed <= ushort.MaxValue: key = (ushort)signed; break;
                case TypeCode.Int32 when signed >= int.MinValue && signed <= int.MaxValue: key = (int)signed; break;
                case TypeCode.UInt32 when signed >= uint.MinValue && signed <= uint.MaxValue: key = (uint)signed; break;
                case TypeCode.Int64: key = signed; break;
                default: return false;
            }
        }

        // Parsing accepts spellings a key is never written as, like "+5" or "05".
        return ConvertKeyToString(key) == name;
    }

    private static TypeCode GetIntegerKeyType(Type dictionaryType)
    {
        var keyType = TypeCode.Empty;

        foreach (var i in dictionaryType.GetInterfaces())
        {
            if (!i.IsGenericType || i.GetGenericTypeDefinition() != typeof(IDictionary<,>))
            {
                continue;
            }

            // Keys of several types can't be mapped back from a name.
            if (keyType != TypeCode.Empty)
            {
                return TypeCode.Empty;
            }

            var type = i.GetGenericArguments()[0];
            if (type.IsEnum)
            {
                return TypeCode.Empty;
            }

            keyType = Type.GetTypeCode(type);
        }

        switch (keyType)
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
                return keyType;
            default:
                return TypeCode.Empty;
        }
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
