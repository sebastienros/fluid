using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using Fluid.Utils;

namespace Fluid.Values;

/// <summary>
/// A number literal beyond the range of a decimal (about ±7.9e28), which Liquid accepts because its integers are
/// unbounded and its floats are doubles: <c>99999999999999999999999999999</c> or <c>1e29</c> written as
/// <c>100000000000000000000000000000.0</c>. An integer is written as is and a float like a Ruby float
/// (<c>1.0e+29</c>). It is ordered and compared with other numbers by its sign and magnitude. The math filters compute
/// with integers exactly and with floats as doubles, like Ruby; elsewhere it is the nearest decimal
/// (<see cref="decimal.MaxValue"/> or <see cref="decimal.MinValue"/>), like a string of digits too large for a decimal.
/// </summary>
public sealed class BigNumberValue : FluidValue
{
    private readonly string _text;
    private readonly double _value;

    private BigNumberValue(string text, double value, bool isInteger)
    {
        _text = text;
        _value = value;
        IsIntegral = isInteger;
    }

    /// <summary>
    /// Creates the number of a literal: a <see cref="BigNumberValue"/> when it is beyond the range of a decimal,
    /// otherwise a <see cref="NumberValue"/>.
    /// </summary>
    /// <param name="text">An integer like <c>-99999999999999999999999999999</c>, or a float like <c>1.5</c>,
    /// <c>1.0e+29</c> or <c>Infinity</c>.</param>
    public static FluidValue Create(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var isInteger = IsIntegralText(text);
        var styles = isInteger ? NumberStyles.AllowLeadingSign : NumberStyles.Float;

        if (decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out var number))
        {
            return NumberValue.Create(number);
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            // .NET Framework fails to parse a double out of range instead of returning an infinity.
            if (!isInteger && !IsFloatText(text))
            {
                throw new FormatException($"'{text}' is not a number.");
            }

            value = text[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
        }

        if (double.IsNaN(value))
        {
            throw new FormatException($"'{text}' is not a number.");
        }

        return new BigNumberValue(isInteger ? TrimLeadingZeros(text) : FormatFloat(value), value, isInteger);
    }

    /// <summary>
    /// Creates the number of an integer: a <see cref="BigNumberValue"/> when it is beyond the range of a decimal,
    /// otherwise a <see cref="NumberValue"/>.
    /// </summary>
    internal static FluidValue Create(BigInteger value)
    {
        if (value >= MinDecimal && value <= MaxDecimal)
        {
            return NumberValue.Create((decimal)value);
        }

        // An integer too large for a double is an infinity, like in Ruby's Integer#to_f.
        return new BigNumberValue(value.ToString(CultureInfo.InvariantCulture), (double)value, isInteger: true);
    }

    /// <summary>
    /// Creates the number of a float: a <see cref="BigNumberValue"/> when it is beyond the range of a decimal, otherwise a
    /// <see cref="NumberValue"/> with a decimal point, like <c>2.0</c>.
    /// </summary>
    /// <exception cref="LiquidException">The value is NaN, which only a <see cref="BigNumberValue"/> can't hold.</exception>
    internal static FluidValue CreateFloat(double value)
    {
        if (double.IsNaN(value))
        {
            throw new LiquidException("The result is not a number.");
        }

        if (Math.Abs(value) < (double)decimal.MaxValue)
        {
            var number = NumberConversions.ToDecimal(value);

            // Like Ruby's floats, a whole number keeps a decimal place: 2.0.
            return NumberValue.Create(NumberValue.GetScale(number) == 0 ? number * 1.0m : number);
        }

        return new BigNumberValue(FormatFloat(value), value, isInteger: false);
    }

    /// <summary>
    /// The size of a number like Ruby's <c>Integer#size</c>, which the <c>size</c> filter returns: the number of bytes
    /// of the magnitude of an integer, and at least 8 like a 64-bit integer. A float has no size, which is 0.
    /// </summary>
    internal static int GetIntegerSize(FluidValue value, TemplateContext context)
    {
        BigInteger integer;

        if (value is BigNumberValue big)
        {
            if (!big.IsIntegral)
            {
                return 0;
            }

            integer = big.ToBigInteger();
        }
        else
        {
            var number = value.ToNumberValue(context);

            if (NumberValue.GetScale(number) != 0)
            {
                return 0;
            }

            if (Math.Abs(number) <= ulong.MaxValue)
            {
                return 8;
            }

            integer = new BigInteger(number);
        }

        // The bytes of a two's complement, with a last byte for the sign when the highest bit of the magnitude is set.
        var bytes = BigInteger.Abs(integer).ToByteArray();
        var length = bytes.Length > 1 && bytes[bytes.Length - 1] == 0 ? bytes.Length - 1 : bytes.Length;

        return Math.Max(8, length);
    }

    private static readonly BigInteger MaxDecimal = new(decimal.MaxValue);

    private static readonly BigInteger MinDecimal = new(decimal.MinValue);

    /// <summary>
    /// Whether the number is an integer rather than a float.
    /// </summary>
    internal bool IsIntegral { get; }

    /// <summary>
    /// 1 for a positive number, -1 for a negative one. It is never 0.
    /// </summary>
    internal int Sign => _value > 0 ? 1 : -1;

    /// <summary>
    /// Whether the number is too large for a double, which can only be the case of an integer or of
    /// a float written as <c>Infinity</c>.
    /// </summary>
    internal bool IsInfinity => double.IsInfinity(_value);

    /// <summary>
    /// The nearest double, an infinity for an integer too large for a double.
    /// </summary>
    internal double DoubleValue => _value;

    /// <summary>
    /// The integer, or the whole number of a float, which is too large to have a fraction.
    /// </summary>
    /// <exception cref="LiquidException">The number is a float written as <c>Infinity</c>.</exception>
    internal BigInteger ToBigInteger()
    {
        if (!TryGetInteger(out var value))
        {
            // Like Ruby's FloatDomainError.
            throw new LiquidException(_text);
        }

        return value;
    }

    public override FluidValues Type => FluidValues.Number;

    public override bool Equals(FluidValue other)
    {
        // A decimal is never this large.
        return other is BigNumberValue big && CompareTo(big) == 0;
    }

    public override bool Equals(object obj)
    {
        return obj is FluidValue value && Equals(value);
    }

    public override int GetHashCode()
    {
        // An integer and a float that compare equal have the same double.
        return _value.GetHashCode();
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return true;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        return _value > 0 ? decimal.MaxValue : decimal.MinValue;
    }

    public override string ToStringValue(TemplateContext context)
    {
        return _text;
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return IsIntegral ? BigInteger.Parse(_text, CultureInfo.InvariantCulture) : _value;
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);

        // Digits, a point, a sign, an exponent or Infinity, which are safe in any context: an HTML encoder
        // would encode the + of 1.0e+29.
        output.Write(_text);
        return default;
    }

    /// <summary>
    /// Compares two numbers, at least one of them a <see cref="BigNumberValue"/>, the other one a
    /// <see cref="NumberValue"/> or a <see cref="BigNumberValue"/>. Public for source-generated templates.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static int Compare(FluidValue left, FluidValue right)
    {
        if (left is BigNumberValue big)
        {
            // A decimal is always closer to zero.
            return right is BigNumberValue other ? big.CompareTo(other) : big.Sign;
        }

        return -((BigNumberValue)right).Sign;
    }

    private int CompareTo(BigNumberValue other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        // Exact, like in Ruby. A float this large is always a whole number.
        if (TryGetInteger(out var left) && other.TryGetInteger(out var right))
        {
            return left.CompareTo(right);
        }

        return _value.CompareTo(other._value);
    }

    private bool TryGetInteger(out BigInteger value)
    {
        if (IsIntegral)
        {
            value = BigInteger.Parse(_text, CultureInfo.InvariantCulture);
            return true;
        }

        if (double.IsInfinity(_value))
        {
            value = default;
            return false;
        }

        value = new BigInteger(_value);
        return true;
    }

    private static bool IsIntegralText(string text)
    {
        var start = text.Length > 0 && text[0] == '-' ? 1 : 0;

        if (start == text.Length)
        {
            return false;
        }

        for (var i = start; i < text.Length; i++)
        {
            if (text[i] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    // Like -?\d+\.\d+
    private static bool IsFloatText(string text)
    {
        var point = text.IndexOf('.');

        return point > 0 && point < text.Length - 1
            && IsIntegralText(text.Remove(point, 1))
            && text[point - 1] != '-';
    }

    private static string TrimLeadingZeros(string text)
    {
        var start = text[0] == '-' ? 1 : 0;
        var first = start;

        while (first < text.Length - 1 && text[first] == '0')
        {
            first++;
        }

        return first == start ? text : text.Remove(start, first - start);
    }

    // Like Ruby's Float#to_s for the magnitudes beyond a decimal, which always have an exponent: 1.0e+29,
    // 7.922816251426434e+28.
    private static string FormatFloat(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        // The exponent has a sign and at least two digits, like in Ruby: 1E+29.
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var hasPoint = text.AsSpan().IndexOf('.') >= 0;

        if (text.AsSpan().IndexOf('E') < 0)
        {
            return hasPoint ? text : text + ".0";
        }

        return text.Replace("E", hasPoint ? "e" : ".0e");
    }
}
