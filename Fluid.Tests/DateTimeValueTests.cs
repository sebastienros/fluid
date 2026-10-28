using System;
using System.Collections.Generic;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

public class DateTimeValueTests
{
    [Fact]
    public void EqualsAnotherDateTimeValue()
    {
        var date = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        object same = DateTimeValue.Create(date);
        object other = DateTimeValue.Create(date.AddDays(1));

        Assert.True(DateTimeValue.Create(date).Equals(same));
        Assert.False(DateTimeValue.Create(date).Equals(other));
        Assert.True(DateTimeValue.Create(date).Equals(date));

        // Hash-based collections compare with Equals(object).
        Assert.Single(new HashSet<FluidValue> { DateTimeValue.Create(date), DateTimeValue.Create(date) });
    }
}
