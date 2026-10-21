using System.Text;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

public sealed class ArrayValue : FluidValue, IFluidArray
{
    public static readonly ArrayValue Empty = new ArrayValue([]);

    public override FluidValues Type => FluidValues.Array;

    public static ArrayValue Create(IReadOnlyList<FluidValue> values)
    {
        return values is null || values is FluidValue[] { Length: 0 }
            ? Empty
            : new ArrayValue(values);
    }

    private ArrayValue(IReadOnlyList<FluidValue> values)
    {
        Values = values ?? [];
    }

    public override bool Equals(FluidValue other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other.IsNil())
        {
            return Values.Count == 0;
        }

        if (other is IFluidArray otherValues)
        {
            using var scope = RecursiveComparisonGuard.Enter(this, other);

            if (Values.Count != otherValues.Count)
            {
                return false;
            }

            for (var i = 0; i < Values.Count; i++)
            {
                var item = Values[i];
                var otherItem = otherValues[i];

                if (!item.Equals(otherItem))
                {
                    return false;
                }
            }

            return true;
        }
        else if (other.Type == FluidValues.Empty)
        {
            return Values.Count == 0;
        }

        return false;
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        switch (name)
        {
            case "size":
                return NumberValue.Create(Values.Count);

            case "first":
                if (Values.Count > 0)
                {
                    return Values[0];
                }
                break;

            case "last":
                if (Values.Count > 0)
                {
                    return Values[Values.Count - 1];
                }
                break;

        }

        return NilValue.Instance;
    }

    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
    {
        var i = (int)index.ToNumberValue();

        if (i < 0)
        {
            i = Values.Count + i;
        }

        if (i >= 0 && i < Values.Count)
        {
            return Create(Values[i], context.Options);
        }

        return NilValue.Instance;
    }

    public override bool ToBooleanValue()
    {
        return true;
    }

    public override decimal ToNumberValue()
    {
        return Values.Count;
    }

    public IReadOnlyList<FluidValue> Values { get; }

    int IFluidArray.Count => Values.Count;

    FluidValue IFluidArray.this[int index] => Values[index];

    /// <summary>
    /// Returns the indexed items of an array-like value, materializing it only when it doesn't implement <see cref="IFluidArray"/>.
    /// </summary>
    internal static ValueTask<IFluidArray> ToFluidArrayAsync(FluidValue value, TemplateContext context)
    {
        if (value is IFluidArray array)
        {
            return new ValueTask<IFluidArray>(array);
        }

        return Awaited(value, context);

        static async ValueTask<IFluidArray> Awaited(FluidValue value, TemplateContext context)
        {
            return Create(await value.EnumerateAsync(context).ToListAsync(context.CancellationToken));
        }
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
    {
        AssertWriteToParameters(output, encoder, cultureInfo);

        return WriteItemsAsync(this, Values, output, encoder, cultureInfo);
    }

    /// <summary>
    /// Writes the items of an array one after the other, encoding them like any other output.
    /// </summary>
    internal static ValueTask WriteItemsAsync(FluidValue array, IReadOnlyList<FluidValue> values, IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo, int startIndex = 0)
    {
        // The thread-local guard must be released before an asynchronous item suspends.
        var count = values.Count;
        var i = startIndex;
        ValueTask task = default;
        using (RecursiveValueGuard.Enter(array))
        {
            for (; i < count; i++)
            {
                task = values[i].WriteToAsync(output, encoder, cultureInfo);
                if (!task.IsCompletedSuccessfully)
                {
                    break;
                }

                task.GetAwaiter().GetResult();
            }
        }

        return i < count ? Awaited(task, array, values, output, encoder, cultureInfo, i + 1) : default;

        static async ValueTask Awaited(ValueTask task, FluidValue array, IReadOnlyList<FluidValue> values, IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo, int startIndex)
        {
            await task;
            await WriteItemsAsync(array, values, output, encoder, cultureInfo, startIndex);
        }
    }

    public override string ToStringValue()
    {
        using var scope = RecursiveValueGuard.Enter(this);
        return ConcatStringValues(Values);
    }

    internal static string ConcatStringValues(IReadOnlyList<FluidValue> values)
    {
        var count = values.Count;

        if (count == 0)
        {
            return "";
        }

        if (count == 1)
        {
            return values[0].ToStringValue() ?? "";
        }

        var builder = new ValueStringBuilder(stackalloc char[256]);
        for (var i = 0; i < count; i++)
        {
            builder.Append(values[i].ToStringValue());
        }

        return builder.ToString();
    }

    public override object ToObjectValue()
    {
        using var scope = RecursiveValueGuard.Enter(this);
        return Values.Select(x => x.ToObjectValue()).ToArray();
    }

    public override ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
    {
        return new ValueTask<bool>(Values.Contains(value));
    }

    public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
    {
        foreach (var value in Values)
        {
            context?.IncrementSteps();
            yield return value;
        }

        await Task.CompletedTask;
    }

    public override bool Equals(object obj)
    {
        // The is operator will return false if null
        if (obj is FluidValue otherValue)
        {
            return Equals(otherValue);
        }

        return false;
    }

    public override int GetHashCode()
    {
        using var scope = RecursiveValueGuard.Enter(this);
        var hc = new HashCode();

        IReadOnlyList<FluidValue> values = Values;
        int count = values.Count;
        for (int i = 0; i < count; ++i)
        {
            hc.Add(values[i]);
        }

        return hc.ToHashCode();
    }
}
