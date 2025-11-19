# Working with Time Zones in Fluid

This guide explains how time zones work in Fluid templates, covering parsing, rendering, and conversion of date/time values.

## Table of Contents
- [Understanding Time Zone Behavior](#understanding-time-zone-behavior)
- [The TimeZone Property](#the-timezone-property)
- [Converting Time Zones During Rendering](#converting-time-zones-during-rendering)
- [Automatic Conversion with Value Converters](#automatic-conversion-with-value-converters)
- [Common Patterns and Examples](#common-patterns-and-examples)

## Understanding Time Zone Behavior

In Fluid, time zones affect two different operations:
1. **Parsing**: When converting strings to DateTime values
2. **Rendering**: When displaying DateTime values in templates

**Important**: The `TimeZone` property in `TemplateContext` and `TemplateOptions` is used **only for parsing**, not for rendering. This is a common source of confusion.

### What Happens During Parsing

When you parse a date string that doesn't include time zone information:

```csharp
var context = new TemplateContext 
{ 
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York") 
};

// This string has no time zone information
var input = new StringValue("2022-12-13T21:02:18.399");

// The date filter will parse it assuming the context's TimeZone
var result = await MiscFilters.Date(input, new FilterArguments(), context);
```

In this case:
- The string "2022-12-13T21:02:18.399" has no time zone
- Fluid assumes it represents a time in the `America/New_York` time zone
- The resulting `DateTimeOffset` will have the offset for New York (-05:00 in winter, -04:00 in summer)

If the string **already includes** time zone information:

```csharp
// This string includes time zone information (+00:00)
var input = new StringValue("2022-12-13T21:02:18.399+00:00");

// The context's TimeZone is ignored - the string's time zone is used
var result = await MiscFilters.Date(input, new FilterArguments(), context);
```

### What Happens During Rendering

When rendering a DateTime value, Fluid displays it in its **own time zone**, not the context's time zone:

```csharp
var date = new DateTime(2022, 2, 2, 12, 0, 0, DateTimeKind.Utc);
var timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Uzhgorod");

var context = new TemplateContext(data, new TemplateOptions
{
    TimeZone = timezone  // This does NOT affect rendering!
});

var template = parser.Parse("{{ BirthDate }}");
var result = template.Render(context);
// Output: "2022-02-02 12:00:00Z" (UTC, not Europe/Uzhgorod)
```

The date is rendered in UTC because that's what the `DateTime` object contains. The context's `TimeZone` property has no effect on rendering.

## The TimeZone Property

The `TimeZone` property is available in both `TemplateOptions` and `TemplateContext`:

```csharp
// Set globally for all templates using these options
var options = new TemplateOptions
{
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time")
};

// Or set per template context
var context = new TemplateContext
{
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York")
};
```

**Use Cases for the TimeZone Property:**
- Parsing date strings without explicit time zone information
- Ensuring consistent date parsing across different server environments
- Interpreting user input in a specific time zone

**What it Does NOT Do:**
- Automatically convert DateTime values during rendering
- Change the time zone of DateTime objects in your model
- Apply to dates that already have time zone information

## Converting Time Zones During Rendering

To display dates in a specific time zone, use the `time_zone` filter:

### Using the time_zone Filter

The `time_zone` filter converts a DateTime to a specific time zone:

```liquid
{{ BirthDate | time_zone: 'America/New_York' | date: '%+' }}
```

### The 'local' Keyword

The special keyword `'local'` converts to the context's configured time zone:

```csharp
var context = new TemplateContext(data, new TemplateOptions
{
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Uzhgorod")
});
```

```liquid
{{ BirthDate | time_zone: 'local' | date: '%c' }}
```

This will convert `BirthDate` to the "Europe/Uzhgorod" time zone before formatting it.

**Important**: The `time_zone` filter should be used in combination with the `date` filter to see the timezone information in the output. When rendering a DateTime without the `date` filter, Fluid uses the ISO 8601 format which always displays in UTC regardless of the timezone conversion.

### Complete Example

Here's a complete example showing the difference:

```csharp
using Fluid;

const string text = @"
Without conversion: {{ BirthDate }}
With time_zone filter: {{ BirthDate | time_zone: 'local' | date: '%c' }}
Explicit timezone: {{ BirthDate | time_zone: 'Europe/Uzhgorod' | date: '%c' }}
";

var date = new DateTime(2022, 2, 2, 12, 0, 0, DateTimeKind.Utc);
var timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Uzhgorod");

var data = new 
{
    BirthDate = date
};

var parser = new FluidParser();
var template = parser.Parse(text);

var context = new TemplateContext(data, new TemplateOptions
{
    TimeZone = timezone
});

var result = template.Render(context);
Console.WriteLine(result);
```

Output:
```
Without conversion: 2022-02-02 12:00:00Z
With time_zone filter: Wednesday, 02 February 2022 14:00:00
Explicit timezone: Wednesday, 02 February 2022 14:00:00
```

Notice that the `time_zone` filter must be combined with the `date` filter to properly display the converted time. The first line shows the UTC time, while the filtered versions show the time converted to 14:00 in the Uzhgorod timezone (UTC+2).

## Automatic Conversion with Value Converters

If you want to automatically convert all DateTime values to a specific time zone without using the `time_zone` filter everywhere, you can use a value converter:

### Creating a Time Zone Value Converter

```csharp
using Fluid;
using Fluid.Values;

var options = new TemplateOptions
{
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Uzhgorod")
};

// Add a value converter that automatically converts all DateTime values
options.ValueConverters.Add(obj =>
{
    if (obj is DateTime dt)
    {
        // Convert to the context's time zone
        var converted = TimeZoneInfo.ConvertTime(dt, options.TimeZone);
        return new DateTimeValue(converted);
    }
    
    if (obj is DateTimeOffset dto)
    {
        // Convert to the context's time zone
        var converted = TimeZoneInfo.ConvertTime(dto, options.TimeZone);
        return new DateTimeValue(converted);
    }
    
    return null; // No conversion needed
});

var data = new 
{
    BirthDate = new DateTime(2022, 2, 2, 12, 0, 0, DateTimeKind.Utc)
};

var context = new TemplateContext(data, options);
```

Now all DateTime values will be automatically converted to the configured time zone. To see the converted time in your output, use the `date` filter:

```liquid
{{ BirthDate | date: '%c' }}
```

Output: `Wednesday, 02 February 2022 14:00:00` (converted to Europe/Uzhgorod time zone, UTC+2)

**Note**: When rendering a DateTime without a format filter (e.g., `{{ BirthDate }}`), Fluid uses the ISO 8601 universal sortable format (`yyyy-MM-dd HH:mm:ssZ`) which displays in UTC regardless of the timezone conversion. Always use the `date` filter with a format string to see the timezone-aware output.

### Advantages of Value Converters
- Centralized time zone conversion logic
- Automatic conversion of all dates in your models
- No need to remember to use `time_zone` filter on every date (though you still need the `date` filter for formatted output)

### Disadvantages of Value Converters
- Less flexibility - all dates are converted the same way
- May not be appropriate if you need different time zones for different dates
- Less explicit - the conversion happens behind the scenes
- Still requires using the `date` filter to see timezone-aware formatted output

## Common Patterns and Examples

### Pattern 1: Display Server Time in User's Time Zone

```csharp
// In your application setup
var options = new TemplateOptions();

// Add a converter to display all dates in the user's time zone
options.ValueConverters.Add(obj =>
{
    if (obj is DateTime dt && dt.Kind == DateTimeKind.Utc)
    {
        // This would typically come from user preferences
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var converted = TimeZoneInfo.ConvertTime(dt, userTimeZone);
        return new DateTimeValue(converted);
    }
    return null;
});
```

Template:
```liquid
Server time: {{ ServerTime }}
```

### Pattern 2: Display Multiple Time Zones

```liquid
UTC: {{ EventTime }}
New York: {{ EventTime | time_zone: 'America/New_York' | date: '%c' }}
London: {{ EventTime | time_zone: 'Europe/London' | date: '%c' }}
Tokyo: {{ EventTime | time_zone: 'Asia/Tokyo' | date: '%c' }}
```

### Pattern 3: User-Specific Time Zone

```csharp
var context = new TemplateContext(model, options);

// Set the user's preferred time zone
context.TimeZone = userPreferredTimeZone;
```

Template:
```liquid
Your local time: {{ EventTime | time_zone: 'local' | date: '%c' }}
```

### Pattern 4: Parsing User Input

```csharp
var context = new TemplateContext
{
    // User is entering times in Pacific time
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles")
};
```

Template (parsing a user-entered date):
```liquid
{% assign meeting_time = '2024-03-15 14:30' | date: '%+' %}
Meeting scheduled for: {{ meeting_time }}
```

The date string will be interpreted as Pacific time, not UTC.

## Time Zone Identifiers

Fluid uses the [TimeZoneConverter](https://github.com/mattjohnsonpint/TimeZoneConverter) library, which supports:
- IANA time zone IDs (e.g., "America/New_York", "Europe/London")
- Windows time zone IDs (e.g., "Pacific Standard Time", "GMT Standard Time")
- Cross-platform conversion between IANA and Windows identifiers

### Finding Time Zone IDs

**IANA IDs** (recommended for cross-platform apps):
```csharp
"America/New_York"    // Eastern Time
"America/Los_Angeles" // Pacific Time
"Europe/London"       // British Time
"Asia/Tokyo"          // Japan Time
"UTC"                 // Coordinated Universal Time
```

**Windows IDs**:
```csharp
"Eastern Standard Time"
"Pacific Standard Time"
"GMT Standard Time"
"Tokyo Standard Time"
```

You can get a list of all available time zones:
```csharp
foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
{
    Console.WriteLine($"{tz.Id}: {tz.DisplayName}");
}
```

## Summary

| Scenario | Solution |
|----------|----------|
| Parse date strings without time zone info | Set `TemplateContext.TimeZone` |
| Display dates in a specific time zone | Use `{{ date \| time_zone: 'timezone-id' }}` filter |
| Display dates in the context's time zone | Use `{{ date \| time_zone: 'local' }}` filter |
| Automatically convert all dates | Use a `ValueConverter` |
| Display dates in multiple time zones | Use multiple `time_zone` filters with different IDs |

## Additional Resources

- [Ruby date and time format strings](https://ruby-doc.org/core-3.0.0/Time.html#method-i-strftime) - Used by the `date` filter
- [TimeZoneConverter Library](https://github.com/mattjohnsonpint/TimeZoneConverter) - Cross-platform time zone support
- [IANA Time Zone Database](https://www.iana.org/time-zones) - Standard time zone identifiers
- [Fluid README - Time zones section](README.md#time-zones) - Quick reference
- [Fluid README - Value Converters section](README.md#adding-a-value-converter) - More on value converters
