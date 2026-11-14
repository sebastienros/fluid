---
title: Working with time zones
description: "This guide explains how time zones work in Fluid templates, covering parsing, rendering, and conversion of date/time values."
---

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
var input = StringValue.Create("2022-12-13T21:02:18.399");

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
var input = StringValue.Create("2022-12-13T21:02:18.399+00:00");

// The context's TimeZone is ignored - the string's time zone is used
var result = await MiscFilters.Date(input, new FilterArguments(), context);
```

### What Happens During Rendering

When rendering a DateTime value, Fluid displays it in its **own time zone**, not the context's time zone:

```csharp
var date = new DateTime(2022, 2, 2, 12, 0, 0, DateTimeKind.Utc);
var timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Uzhgorod");

var context = new TemplateContext(data, new TemplateOptionsBuilder()
    .WithTimeZone(timezone) // This does NOT affect rendering!
    .Build());

var template = parser.Parse("{{ BirthDate }}");
var result = template.Render(context);
// Output: "2022-02-02 12:00:00Z" (UTC, not Europe/Uzhgorod)
```

The date is rendered in UTC because that's what the `DateTime` object contains. The context's `TimeZone` property has no effect on rendering.

### Dates and Times Without a Time Zone

A `DateTime` of `Unspecified` kind, a `DateOnly` and a `TimeOnly` are clock readings rather than moments, like a date read from a database column without offset. Fluid keeps them as they are, a `PlainDateTimeValue`, and reads them in the time zone of the context only when a date filter needs a moment, whatever the time zone of the machine:

```csharp
var context = new TemplateContext
{
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York")
};

context.SetValue("starts", new DateTime(2026, 3, 4, 13, 0, 0)); // Unspecified kind
context.SetValue("day", new DateOnly(2026, 3, 4));
context.SetValue("opening", new TimeOnly(13, 0));
```

```liquid
{{ starts }}                                → 2026-03-04 13:00:00
{{ starts | date: '%Y-%m-%d %H:%M %z' }}    → 2026-03-04 13:00 -0500
{{ starts | time_zone: 'Europe/Berlin' | date: '%H:%M' }} → 19:00
{{ day }}                                   → 2026-03-04
{{ day | date: '%Y-%m-%d %H:%M %z' }}       → 2026-03-04 00:00 -0500
{{ opening }}                               → 13:00:00
{{ opening | date: '%H:%M %z' }}            → 13:00 -0500 (-0400 in summer)
```

- On their own, they render as ISO 8601 clock readings without offset, so they sort chronologically, and `json` writes them as `"2026-03-04T13:00:00"`, `"2026-03-04"` and `"13:00:00"`.
- A `DateOnly` is read at midnight. A `TimeOnly` is read on the current date of the context, `TemplateContext.Now()` in its time zone, like Ruby's `Time.parse("13:00")`, so its offset follows daylight saving time.
- In .NET, `ToObjectValue()` returns the `DateTime`, `DateOnly` or `TimeOnly`.

A `DateTime` of `Utc` or `Local` kind is a moment, a `DateTimeValue`: a `Utc` one keeps the `+00:00` offset and a `Local` one, like `DateTime.Now`, the offset of the machine. The context's time zone doesn't change them.

> **Breaking changes:**
> - A `DateTime` of `Unspecified` kind was read in the time zone of the machine, ignoring the options and the context: `13:00` was formatted as `13:00 +0100` on a server in Paris whatever `TemplateContext.TimeZone` was, and rendered in UTC as `2026-03-04 12:00:00Z`. `ToObjectValue()` returned a `DateTimeOffset`.
> - A `DateOnly` was converted to a string with the culture of the options, `03/04/2026` with the invariant culture, which the date filters read as April 3.
> - A `TimeOnly` was converted to a string like `13:00`.
> - A date was its number of .NET ticks in math filters: `{{ starts | plus: 0 }}` rendered `639082260000000000`, and now renders `0` like in Liquid.
> - A number with a fraction was read as a timestamp to the millisecond: `{{ 0.5 | date: '%L' }}` rendered `500`. Like in Liquid, only an integer is a timestamp now, and `date` returns `0.5` unchanged.
>
> `DateTimeValue.Create(DateTime)` is unchanged and still reads an `Unspecified` `DateTime` in the time zone of the machine: use `PlainDateTimeValue.Create()` in value converters.

### Date Math

Like in Liquid, a date isn't a number: it is `0` in math filters like `plus` or `minus`, whatever its type. Do the math on its Unix time instead, which `date: '%s'` returns, and read the result with `date` in the time zone of the context:

```liquid
{{ starts | date: '%s' | plus: 86400 | date: '%Y-%m-%d %H:%M' }}  → 2026-03-05 13:00
{% assign from = day | date: '%s' %}
{{ '2026-12-25' | date: '%s' | minus: from | divided_by: 86400 }} → 296
```

Adding `86400` adds 24 hours, which is a different clock time on the day daylight saving time starts or ends.

### Time Spans

A `TimeSpan` is a time of day or a duration, not a moment, so it has no time zone of its own. It renders in its constant format, like `13:00:00` or `1.02:00:00` for 26 hours:

```csharp
var context = new TemplateContext
{
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York")
};

context.SetValue("opening", TimeSpan.FromHours(13));
```

```liquid
{{ opening }}                              → 13:00:00
{{ opening | date: '%H:%M %z' }}           → 13:00 -0500
{{ opening | format_date: 'HH:mm K' }}     → 13:00 -05:00
{{ opening | time_zone: 'Europe/Berlin' | date: '%H:%M' }} → 19:00
```

The date filters (`date`, `format_date` and `time_zone`) read it as that time on January 1, 1970, at the standard offset of the context's time zone, so the clock time is kept whatever the time zone. Since a time of day isn't a moment, the offset is the one the time zone has today without daylight saving time, not the one it had in 1970: `+03:00` in Istanbul, `+10:00` in Sydney. As a number, for instance with `plus`, it is a number of seconds: `{{ opening | divided_by: 60 }}` renders `780`. In .NET, `ToObjectValue()` returns the `TimeSpan`.

> **Breaking change:** Fluid used to convert a `TimeSpan` to the moment this long after 1970-01-01 00:00 UTC, in the time zone of the options, ignoring `TemplateContext.TimeZone`. It rendered like `1970-01-01 13:00:00Z`, `ToObjectValue()` returned a `DateTimeOffset`, and `13:00` was formatted as `08:00 -05:00` in New York.

## The TimeZone Property

The `TimeZone` property is available in both `TemplateOptions` and `TemplateContext`:

```csharp
// Set globally for all templates using these options
var options = new TemplateOptionsBuilder()
    .WithTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"))
    .Build();

// Or set per template context
var context = new TemplateContext
{
    TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York")
};
```

**Use Cases for the TimeZone Property:**
- Parsing date strings without explicit time zone information
- Reading [dates and times without a time zone](#dates-and-times-without-a-time-zone): `DateTime` values of `Unspecified` kind, `DateOnly`, `TimeOnly` and `TimeSpan`
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
var context = new TemplateContext(data, new TemplateOptionsBuilder()
    .WithTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Uzhgorod"))
    .Build());
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

var context = new TemplateContext(data, new TemplateOptionsBuilder()
    .WithTimeZone(timezone)
    .Build());

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

## Resolving Time Zone Identifiers

On .NET 6 and later, the `time_zone` filter resolves identifiers with `TimeZoneInfo.FindSystemTimeZoneById`. Both IANA identifiers such as `America/Los_Angeles` and Windows identifiers such as `Pacific Standard Time` are supported across platforms when ICU globalization data is available. Fluid's `netstandard2.0` asset uses [TimeZoneConverter](https://github.com/mattjohnsonpint/TimeZoneConverter) as its default resolver for compatibility with older runtimes.

```csharp
var options = new TemplateOptionsBuilder().Build();
var context = new TemplateContext(options);

var template = parser.Parse(
    "{{ published | time_zone: 'America/Los_Angeles' | date: '%+' }}");
```

IANA identifiers are not available through `TimeZoneInfo` on Windows when globalization invariant mode or NLS mode is enabled. You can replace the resolver through `TemplateOptionsBuilder.WithTimeZoneResolver()`. For example, an application using the [TimeZoneConverter](https://github.com/mattjohnsonpint/TimeZoneConverter) package can configure:

```csharp
using TimeZoneConverter;

var options = new TemplateOptionsBuilder()
    .WithTimeZoneResolver(id => TZConvert.GetTimeZoneInfo(id))
    .Build();

var context = new TemplateContext(options);
```

The resolver is used for explicit identifiers passed to the `time_zone` filter. The special `local` identifier still uses `TemplateContext.TimeZone`.

## Automatic Conversion with Value Converters

If you want to automatically convert all DateTime values to a specific time zone without using the `time_zone` filter everywhere, you can use a value converter:

### Creating a Time Zone Value Converter

```csharp
using Fluid;
using Fluid.Values;

var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Uzhgorod");

// Add a value converter that automatically converts all DateTime values
var options = new TemplateOptionsBuilder()
    .WithTimeZone(timeZone)
    .AddValueConverter(obj =>
    {
        // A DateTime of Unspecified kind has no time zone to convert from, Fluid reads it in the context's one.
        if (obj is DateTime dt && dt.Kind != DateTimeKind.Unspecified)
        {
            // Convert to the options' time zone
            var converted = TimeZoneInfo.ConvertTime(new DateTimeOffset(dt), timeZone);
            return DateTimeValue.Create(converted);
        }

        if (obj is DateTimeOffset dto)
        {
            // Convert to the options' time zone
            var converted = TimeZoneInfo.ConvertTime(dto, timeZone);
            return DateTimeValue.Create(converted);
        }

        return null; // No conversion needed
    })
    .Build();

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
// Add a converter to display all dates in the user's time zone
var options = new TemplateOptionsBuilder()
    .AddValueConverter(obj =>
    {
        if (obj is DateTime dt && dt.Kind == DateTimeKind.Utc)
        {
            // This would typically come from user preferences
            var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
            var converted = TimeZoneInfo.ConvertTime(dt, userTimeZone);
            return DateTimeValue.Create(converted);
        }
        return null;
    })
    .Build();
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

Fluid uses `TimeZoneInfo` on .NET 6 and later and TimeZoneConverter on `netstandard2.0`. The default resolver supports:
- IANA time zone IDs (e.g., "America/New_York", "Europe/London")
- Windows time zone IDs (e.g., "Pacific Standard Time", "GMT Standard Time")
- Cross-platform resolution on .NET 6 and later when ICU globalization data is available

Use `TemplateOptionsBuilder.WithTimeZoneResolver()` when your runtime does not provide the identifiers your templates need.

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
| Format a `TimeSpan` as a time of day | Use the `date` or `format_date` filter, in the context's time zone |
| Read `DateOnly`, `TimeOnly` and `Unspecified` `DateTime` values in a time zone | Set `TemplateContext.TimeZone`, the machine's one is never used |
| Display dates in a specific time zone | Use `{{ date \| time_zone: 'timezone-id' }}` filter |
| Display dates in the context's time zone | Use `{{ date \| time_zone: 'local' }}` filter |
| Resolve identifiers with a custom library | Set `TemplateOptionsBuilder.WithTimeZoneResolver()` |
| Automatically convert all dates | Use a `ValueConverter` |
| Display dates in multiple time zones | Use multiple `time_zone` filters with different IDs |

## Additional Resources

- [Ruby date and time format strings](https://ruby-doc.org/core-3.0.0/Time.html#method-i-strftime) - Used by the `date` filter
- [TimeZoneInfo.FindSystemTimeZoneById](https://learn.microsoft.com/dotnet/api/system.timezoneinfo.findsystemtimezonebyid) - Default time zone resolution
- [TimeZoneConverter Library](https://github.com/mattjohnsonpint/TimeZoneConverter) - Optional resolver for additional compatibility
- [IANA Time Zone Database](https://www.iana.org/time-zones) - Standard time zone identifiers
- [Cultures, dates and time zones](localization.md) - How date strings are parsed
- [Adding a value converter](models-and-values.md#adding-a-value-converter) - More on value converters
