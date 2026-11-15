namespace Fluid.Values;

/// <summary>
/// Orders dates like Liquid, where a Ruby <c>Time</c> or <c>Date</c> is <c>Comparable</c>: two moments, or two dates,
/// compare chronologically, and comparing one with a value of another kind fails like Ruby.
/// </summary>
/// <remarks>
/// A moment, like a <c>Time</c>, is a <see cref="DateTimeValue"/> or a <see cref="PlainDateTimeValue"/> of a
/// <see cref="DateTime"/>, read in the time zone of the context. A date, like a <c>Date</c>, is a
/// <see cref="PlainDateTimeValue"/> of a <c>DateOnly</c>. A time of day (a <c>TimeOnly</c>) or a <see cref="TimeSpan"/>
/// has no Ruby equivalent: it is ordered with a value of the same kind only, and any other comparison is false.
/// </remarks>
public static class DateComparer
{
    private const string Time = "Time";
    private const string Date = "Date";

    // The astronomical Julian day of 2000-01-01, at midnight UTC. Ruby compares a Date with a number with it.
    private const decimal JulianDayOf2000 = 2451544.5m;
    private static readonly DateTime Year2000 = new(2000, 1, 1);

    /// <summary>
    /// Compares two values when one of them is an ordered date.
    /// </summary>
    /// <returns><c>true</c> with the comparison when both can be ordered, <c>false</c> when the comparison is
    /// false like in Liquid, for instance with <c>nil</c> or an array.</returns>
    /// <exception cref="LiquidException">The values can't be compared, like a date and a string.</exception>
    public static bool TryCompare(FluidValue left, FluidValue right, TemplateContext context, out int comparison)
    {
        if (TryCompareTimes(left, right, out comparison))
        {
            return true;
        }

        var leftKind = GetKind(left);
        var rightKind = GetKind(right);

        if (leftKind == null && rightKind == null)
        {
            return false;
        }

        if (leftKind == null)
        {
            // Like Integer#< or String#<, which don't compare with a Time or a Date.
            if (left is NumberValue or BigNumberValue)
            {
                throw new LiquidException($"comparison of {NumberClass(left, context)} with {rightKind} failed");
            }

            if (left is StringValue)
            {
                throw new LiquidException($"comparison of String with {rightKind} failed");
            }

            // Like nil or an array, which aren't Comparable.
            return false;
        }

        if (rightKind == leftKind)
        {
            return TryCompareSameKind(left, right, context, out comparison);
        }

        if (rightKind != null)
        {
            throw new LiquidException($"comparison of {leftKind} with {rightKind} failed");
        }

        switch (right)
        {
            case StringValue:
                throw new LiquidException($"comparison of {leftKind} with String failed");

            case NumberValue or BigNumberValue when leftKind == Date:
                // Like Ruby, which compares the astronomical Julian day of a Date with a number.
                var julianDay = JulianDayOf2000 + (decimal)(GetDate(left) - Year2000).TotalDays;
                comparison = right is BigNumberValue
                    ? BigNumberValue.Compare(NumberValue.Create(julianDay), right)
                    : julianDay.CompareTo(right.ToNumberValue(context));
                return true;

            case NumberValue or BigNumberValue:
                // Ruby shows the number itself, like "comparison of Time with 5 failed".
                throw new LiquidException($"comparison of {leftKind} with {(right is NumberValue ? right.ToStringValue(context) : "Integer")} failed");

            default:
                return false;
        }
    }

    /// <summary>
    /// Compares two dates of the same kind, chronologically, without failing on other values.
    /// </summary>
    internal static bool TryCompareSameKind(FluidValue left, FluidValue right, TemplateContext context, out int comparison)
    {
        if (TryCompareTimes(left, right, out comparison))
        {
            return true;
        }

        var kind = GetKind(left);

        if (kind == null || kind != GetKind(right))
        {
            comparison = 0;
            return false;
        }

        comparison = kind == Date
            ? GetDate(left).CompareTo(GetDate(right))
            : GetMoment(left, context).CompareTo(GetMoment(right, context));
        return true;
    }

    // Two times of day, or two time spans. As text, "1.02:00:00" (26 hours) would come before "13:00:00", and a time of
    // day used to be a string like "13:00", ordered as text: they are compared by value instead.
    private static bool TryCompareTimes(FluidValue left, FluidValue right, out int comparison)
    {
        if (left is PlainDateTimeValue { IsTimeOfDay: true } leftTime && right is PlainDateTimeValue { IsTimeOfDay: true } rightTime)
        {
            comparison = leftTime.ClockReading.CompareTo(rightTime.ClockReading);
            return true;
        }

        if (left is TimeSpanValue leftSpan && right is TimeSpanValue rightSpan)
        {
            comparison = leftSpan.Value.CompareTo(rightSpan.Value);
            return true;
        }

        comparison = 0;
        return false;
    }

    // The Ruby class the value is like, when it is ordered.
    private static string GetKind(FluidValue value) => value switch
    {
        DateTimeValue => Time,
        PlainDateTimeValue { IsDate: true } => Date,
        PlainDateTimeValue { IsDateTime: true } => Time,
        _ => null,
    };

    private static DateTime GetDate(FluidValue value) => ((PlainDateTimeValue)value).ClockReading;

    private static DateTimeOffset GetMoment(FluidValue value, TemplateContext context)
    {
        value.TryGetDateTimeInput(context, out var moment);
        return moment;
    }

    private static string NumberClass(FluidValue value, TemplateContext context)
    {
        return value is NumberValue && NumberValue.GetScale(value.ToNumberValue(context)) != 0 ? "Float" : "Integer";
    }
}
