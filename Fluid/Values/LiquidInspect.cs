using System.Globalization;
using System.Text;

namespace Fluid.Values;

/// <summary>
/// Writes values like Liquid's <c>Utils.inspect</c>, which Liquid uses to convert a hash to a string: Ruby's
/// <c>inspect</c> format, like <c>{"k"=>"v", "n"=>1, "a"=>[1, nil]}</c>.
/// </summary>
internal static class LiquidInspect
{
    public static void Append(StringBuilder builder, FluidValue value, TemplateContext context)
    {
        switch (value)
        {
            case StringValue:
                AppendString(builder, value.ToStringValue(context));
                break;
            case NilValue or UndefinedValue:
                builder.Append("nil");
                break;
            case BooleanValue:
                builder.Append(value.ToBooleanValue(context) ? "true" : "false");
                break;
            case DictionaryValue dictionary:
                dictionary.AppendInspected(builder, context);
                break;
            case RangeValue range:
                builder.Append(range.Start.ToString(CultureInfo.InvariantCulture)).Append("..").Append(range.End.ToString(CultureInfo.InvariantCulture));
                break;
            case IFluidArray array when value.Type == FluidValues.Array:
                AppendArray(builder, value, array, context);
                break;
            default:
                builder.Append(value.ToStringValue(context));
                break;
        }
    }

    private static void AppendArray(StringBuilder builder, FluidValue value, IFluidArray array, TemplateContext context)
    {
        using var scope = RecursiveValueGuard.Enter(value);

        builder.Append('[');

        for (var i = 0; i < array.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            Append(builder, array[i] ?? NilValue.Instance, context);
        }

        builder.Append(']');
    }

    /// <summary>
    /// Appends a string like Ruby's <c>String#inspect</c>: quoted, with quotes, backslashes, control characters
    /// and <c>#{</c>, <c>#$</c>, <c>#@</c> escaped. Other characters, Unicode included, are kept.
    /// </summary>
    public static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\t': builder.Append("\\t"); break;
                case '\r': builder.Append("\\r"); break;
                case '\f': builder.Append("\\f"); break;
                case '\v': builder.Append("\\v"); break;
                case '\b': builder.Append("\\b"); break;
                case '\a': builder.Append("\\a"); break;
                case '\u001b': builder.Append("\\e"); break;
                case '#' when i + 1 < value.Length && value[i + 1] is '{' or '$' or '@':
                    builder.Append("\\#");
                    break;
                default:
                    if (char.IsControl(c) || c is '\u2028' or '\u2029')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
