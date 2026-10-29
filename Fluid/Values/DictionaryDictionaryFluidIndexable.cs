using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;

namespace Fluid.Values;

public sealed class DictionaryDictionaryFluidIndexable : IFluidIndexable
{
    private enum KeyKind
    {
        // Keys of unknown or several types: a name can only be found by converting each key.
        Other,
        Integer,
        Guid,
        Char,
        Enum
    }

    private sealed class KeyType
    {
        public static readonly KeyType Other = new(KeyKind.Other, TypeCode.Empty, null);

        public KeyType(KeyKind kind, TypeCode typeCode, Type type)
        {
            Kind = kind;
            TypeCode = typeCode;
            Type = type;
        }

        public KeyKind Kind { get; }
        public TypeCode TypeCode { get; }
        public Type Type { get; }
    }

    private enum NameConversion
    {
        // The name is how this key is written.
        Key,
        // No key of this type is written like the name.
        NotAKey,
        // Can't tell without looking at the keys.
        Unknown
    }

    // What the keys of each dictionary type are. A wrapper is created every time a dictionary is
    // read from a model, so the interfaces are only inspected once per type.
    private static readonly ConcurrentDictionary<Type, KeyType> _keyTypes = new();

    private readonly IDictionary _dictionary;
    private readonly TemplateOptions _options;
    private readonly KeyType _keyType;

    public DictionaryDictionaryFluidIndexable(IDictionary dictionary, TemplateOptions options)
    {
        _dictionary = dictionary;
        _options = options;
        _keyType = _keyTypes.GetOrAdd(dictionary.GetType(), static type => GetKeyType(type));
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

        if (_keyType.Kind != KeyKind.Other)
        {
            // Every key is of one known type, so the name can be turned back into the one key it
            // could stand for instead of converting each key to a string to compare.
            var conversion = ConvertNameToKey(name, out var convertedKey);

            if (conversion == NameConversion.Key && _dictionary.Contains(convertedKey))
            {
                value = FluidValue.Create(_dictionary[convertedKey], _options);
                return true;
            }

            if (conversion != NameConversion.Unknown)
            {
                value = NilValue.Instance;
                return false;
            }
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

    private NameConversion ConvertNameToKey(string name, out object key)
    {
        key = null;

        switch (_keyType.Kind)
        {
            case KeyKind.Integer:
                if (!TryConvertNameToInteger(name, out key))
                {
                    return NameConversion.NotAKey;
                }

                break;

            case KeyKind.Guid:
                if (!Guid.TryParse(name, out var guid))
                {
                    return NameConversion.NotAKey;
                }

                key = guid;
                break;

            case KeyKind.Char:
                if (name.Length != 1)
                {
                    return NameConversion.NotAKey;
                }

                key = name[0];
                break;

            case KeyKind.Enum:
                // A value without a name, or a combination of flags, is written in ways that
                // can't be mapped back without the keys.
                if (!Enum.IsDefined(_keyType.Type, name))
                {
                    return NameConversion.Unknown;
                }

                key = Enum.Parse(_keyType.Type, name);
                break;

            default:
                return NameConversion.Unknown;
        }

        // Parsing accepts spellings a key is never written as, like "+5", "05" or a Guid in braces.
        if (ConvertKeyToString(key) != name)
        {
            return NameConversion.NotAKey;
        }

        return NameConversion.Key;
    }

    private bool TryConvertNameToInteger(string name, out object key)
    {
        key = null;

        if (_keyType.TypeCode == TypeCode.UInt64)
        {
            if (!ulong.TryParse(name, NumberStyles.None, _options.CultureInfo, out var unsigned))
            {
                return false;
            }

            key = unsigned;
            return true;
        }

        if (!long.TryParse(name, NumberStyles.AllowLeadingSign, _options.CultureInfo, out var signed))
        {
            return false;
        }

        switch (_keyType.TypeCode)
        {
            case TypeCode.SByte when signed >= sbyte.MinValue && signed <= sbyte.MaxValue: key = (sbyte)signed; return true;
            case TypeCode.Byte when signed >= byte.MinValue && signed <= byte.MaxValue: key = (byte)signed; return true;
            case TypeCode.Int16 when signed >= short.MinValue && signed <= short.MaxValue: key = (short)signed; return true;
            case TypeCode.UInt16 when signed >= ushort.MinValue && signed <= ushort.MaxValue: key = (ushort)signed; return true;
            case TypeCode.Int32 when signed >= int.MinValue && signed <= int.MaxValue: key = (int)signed; return true;
            case TypeCode.UInt32 when signed >= uint.MinValue && signed <= uint.MaxValue: key = (uint)signed; return true;
            case TypeCode.Int64: key = signed; return true;
            default: return false;
        }
    }

    private static KeyType GetKeyType(Type dictionaryType)
    {
        Type keyType = null;

        foreach (var i in dictionaryType.GetInterfaces())
        {
            if (!i.IsGenericType || i.GetGenericTypeDefinition() != typeof(IDictionary<,>))
            {
                continue;
            }

            // Keys of several types can't be mapped back from a name.
            if (keyType != null)
            {
                return KeyType.Other;
            }

            keyType = i.GetGenericArguments()[0];
        }

        if (keyType == null)
        {
            return KeyType.Other;
        }

        if (keyType.IsEnum)
        {
            return new KeyType(KeyKind.Enum, TypeCode.Empty, keyType);
        }

        if (keyType == typeof(Guid))
        {
            return new KeyType(KeyKind.Guid, TypeCode.Empty, keyType);
        }

        var typeCode = Type.GetTypeCode(keyType);

        switch (typeCode)
        {
            case TypeCode.Char:
                return new KeyType(KeyKind.Char, typeCode, keyType);
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
                return new KeyType(KeyKind.Integer, typeCode, keyType);
            default:
                return KeyType.Other;
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
