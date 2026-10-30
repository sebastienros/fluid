using Parlot;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid.Values;

public sealed class StringValue : FluidValue, IEquatable<StringValue>
{
    public static readonly StringValue Empty = new StringValue("");
    public static readonly StringValue Space = new StringValue(" ");

    private static readonly StringValue[] CharToString = new StringValue[256];

    private readonly string _value;

    static StringValue()
    {
        for (var i = 0; i < CharToString.Length; ++i)
        {
            var c = (char)i;
            CharToString[i] = c == ' ' ? Space : new StringValue(c.ToString());
        }
    }

    private StringValue(string value)
    {
        _value = value;
    }

    private StringValue(string value, bool encode) : this(value)
    {
        Encode = encode;
    }

    /// <summary>
    /// Gets whether the string is encoded (default) or not when rendered.
    /// </summary>
    public bool Encode { get; } = true;

    public override FluidValues Type => FluidValues.String;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StringValue Create(char c)
    {
        var temp = CharToString;
        if ((uint)c < (uint)temp.Length)
        {
            return temp[c];
        }
        return new StringValue(c.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FluidValue Create(string s)
    {
        if (s is null)
        {
            return NilValue.Instance;
        }

        if (String.IsNullOrEmpty(s))
        {
            return Empty;
        }

        return s.Length == 1
            ? Create(s[0])
            : new StringValue(s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FluidValue Create(string s, bool encode)
    {
        if (encode)
        {
            return Create(s);
        }

        if (s is null)
        {
            return NilValue.Instance;
        }

        return new StringValue(s, encode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StringValue Create(in TextSpan span)
    {
        if (span.Length == 0)
        {
            return Empty;
        }

        return span.Length == 1
            ? Create(span.Buffer[span.Offset])
            : new StringValue(span.ToString());
    }

    public override bool Equals(FluidValue other)
    {
        // Delegating special cases to other types
        if (other == BlankValue.Instance || other == NilValue.Instance || other == EmptyValue.Instance)
        {
            return other.Equals(this);
        }

        if (other.Type != FluidValues.String)
        {
            return false;
        }

        // Built-in values are compared directly, any other value of the same kind compares itself.
        return other is StringValue s ? _value == s._value : other.Equals(this);
    }

    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
    {
        // Indexer on string values should return nil.
        return NilValue.Instance;
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        return name switch
        {
            "size" => NumberValue.Create(_value.Length),
            // Like Shopify, 'first' and 'last' return the first and last characters.
            "first" => _value.Length == 0 ? Empty : Create(_value[0]),
            "last" => _value.Length == 0 ? Empty : Create(_value[_value.Length - 1]),
            _ => NilValue.Instance,
        };
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return true;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        return ParseNumber();
    }

    private decimal ParseNumber()
    {
        return ParseNumber(_value);
    }

    /// <summary>
    /// Converts a string to a number like Liquid: a decimal number such as <c>-1.5</c> or <c>.5</c> is parsed as is,
    /// anything else is read like Ruby's <c>String#to_i</c>, from the leading digits, so <c>"12abc"</c>
    /// is <c>12</c> and <c>"abc"</c> is <c>0</c>.
    /// </summary>
    internal static decimal ParseNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var span = value.AsSpan();
        var trimmed = span.Trim();

#if NET8_0_OR_GREATER
        if (IsDecimalLiteral(trimmed) && decimal.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
#else
        if (IsDecimalLiteral(trimmed) && decimal.TryParse(trimmed.ToString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
#endif
        {
            return d;
        }

        return ParseLeadingInteger(span);
    }

    // -?\d*\.\d+ (Liquid requires a digit before the point, ".5" is accepted for convenience)
    private static bool IsDecimalLiteral(ReadOnlySpan<char> s)
    {
        var i = 0;

        if (i < s.Length && s[i] == '-')
        {
            i++;
        }

        while (i < s.Length && IsDigit(s[i]))
        {
            i++;
        }

        if (i >= s.Length || s[i] != '.')
        {
            return false;
        }

        i++;

        var fractionStart = i;
        while (i < s.Length && IsDigit(s[i]))
        {
            i++;
        }

        return i > fractionStart && i == s.Length;
    }

    // Like Ruby's String#to_i: leading whitespace, an optional sign, then digits that can be separated
    // by single underscores, up to the first other character.
    private static decimal ParseLeadingInteger(ReadOnlySpan<char> s)
    {
        var i = 0;

        while (i < s.Length && s[i] is ' ' or '\t' or '\n' or '\v' or '\f' or '\r')
        {
            i++;
        }

        var negative = false;
        if (i < s.Length && s[i] is '+' or '-')
        {
            negative = s[i] == '-';
            i++;
        }

        // Digits are accumulated in an ulong, which holds any 19 digits, and only then in a decimal.
        ulong small = 0;
        var digits = 0;
        decimal result = 0;
        var hasDigit = false;

        while (i < s.Length)
        {
            var c = s[i];

            if (IsDigit(c))
            {
                if (digits < 19)
                {
                    small = small * 10 + (ulong)(c - '0');
                    digits++;
                }
                else
                {
                    if (digits == 19)
                    {
                        result = small;
                        digits++;
                    }

                    // A decimal that would overflow saturates.
                    result = result <= MaxBeforeAppendingDigit ? result * 10 + (c - '0') : decimal.MaxValue;
                }

                hasDigit = true;
            }
            else if (c != '_' || !hasDigit || i + 1 >= s.Length || !IsDigit(s[i + 1]))
            {
                break;
            }

            i++;
        }

        if (digits <= 19)
        {
            result = small;
        }

        return negative ? -result : result;
    }

    private const decimal MaxBeforeAppendingDigit = (decimal.MaxValue - 9) / 10;

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    /// <summary>
    /// Gets the string, which doesn't depend on a <see cref="TemplateContext"/>.
    /// </summary>
    internal string Value => _value;

    public override string ToStringValue(TemplateContext context)
    {
        return _value;
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);
        if (string.IsNullOrEmpty(_value))
        {
            return default;
        }

        if (Encode)
        {
            output.Write(encoder, _value);
        }
        else
        {
            output.Write(_value);
        }

        return default;
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return _value;
    }

    public override ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
    {
        return new ValueTask<bool>(_value.Contains(value.ToStringValue(context)));
    }

    public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
    {
        yield return this;
        await Task.CompletedTask;
    }

    public override bool Equals(object obj)
    {
        return obj is StringValue s && Equals(s);
    }

    public bool Equals(StringValue other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _value == other._value;
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
}
