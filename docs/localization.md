# Cultures, dates and time zones

How the culture and the time zone affect rendering, and how strings are converted to dates.

## Culture

By default, templates are rendered using an _invariant_ culture so that the results are consistent across systems. This is important, for instance, when rendering dates, times, and numbers.

However, you can define a specific culture to use when rendering a template using the `TemplateContext.CultureInfo` property. 

#### Source

```csharp
var options = new TemplateOptionsBuilder()
    .WithCultureInfo(new CultureInfo("en-US"))
    .Build();
var context = new TemplateContext(options);
var result = template.Render(context);
```

```Liquid
{{ 1234.56 }}
{{ "now" | date: "%A, %B %-d, %Y" }}
```

#### Result
```html
1234.56
Tuesday, August 1, 2017
```

### Ordering strings

The culture doesn't change how strings are ordered. Like in Shopify, `sort` and the `<`, `<=`, `>` and `>=` operators compare strings by code point, so uppercase letters come before lowercase ones and an emoji comes after any other character: `{{ "b,a,B,😀,é" | split: "," | sort | join: " " }}` renders `B a b é 😀`.

`sort_natural` compares the lowercase strings the same way, whatever the alphabet: `{{ "b,_,É,a,B,é" | split: "," | sort_natural | join: " " }}` renders `_ a b B É é`. Accented letters are still after `z`, and the characters that are between `Z` and `a` in ASCII (``[ \ ] ^ _ ` ``) are before the letters. Like Ruby's `downcase`, `İ` is lowercased to `i` followed by a combining dot (U+0307), so `{{ "İ,j,i,k" | split: "," | sort_natural | join: " " }}` renders `i İ j k`.

## Time zones

> For a complete guide on working with time zones, see [Working with time zones](time-zones.md).

### System time zone

`TemplateOptions` and `TemplateContext` provide a property to define a default time zone to use when parsing dates and times. The default value is the current system's time zone. Setting a custom one can also prevent different environments (data centers) from generating different results.

- When dates and times are parsed and don't specify a time zone, the configured one is assumed. 
- When a time zone is provided in the source string, the resulting date time uses it.

Strings are converted to dates like Liquid does with Ruby's `Time.parse`. `now` and `today`, in any casing, are the current time, and a string of digits is a number of seconds since 1970. Other strings can be in many formats: `2020-06-15T12:00:00Z`, `2020-06-15 12:00:00 UTC`, `Mon, 15 Jun 2020 12:00:00 +0100`, `Fri Jul 16 01:00:00 2004`, `June 15, 2020`, `15 June 2020`, `15/06/2020` (day first), `20200615T120000`. Zones are offsets like `+01:00`, `Z`, `UTC`, `GMT` or the US abbreviations (`EST`, `PDT`, ...); others, like `CET`, are ignored. .NET offsets are whole minutes, so an offset with seconds keeps the instant (`%s`) but drops its seconds from the zone, like `%z` in Ruby, and moves them to the time: `2020-06-15 12:00:00 +05:30:15` is `11:59:45 +0530`. The fields missing before the first one in the string come from the current time (`'5 May'` is May 5 of this year, `'12:30'` is today), and strings without a date or time like `'1,2,3'` are not dates: `date` returns them unchanged. With a specific culture, the formats of the culture are tried first, so `08/01/2017` is August 1 in `en-US` and January 8 in `fr-FR`.

> **Important**: The `TimeZone` property is used for **parsing** date strings, not for automatically converting dates during rendering. To convert dates to a specific timezone for display, use the `time_zone` filter.

> Note: The `date` filter conforms to the Ruby date and time formats https://docs.ruby-lang.org/en/master/strftime_formatting_rdoc.html, including flags, widths and composite formats like `%c` (`%a %b %e %H:%M:%S %Y`), `%x` and `%X`. Day and month names and AM/PM come from the culture. `%Z` is `UTC` for UTC, and the offset for other time zones since .NET has no zone abbreviations. A format that ends inside a conversion (`%`, `%-`) is a template error. To use the .NET standard date formats, use the `format_date` filter.

#### Source

```csharp
var context = new TemplateContext { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time") } ;
var result = template.Render(context);
```

```Liquid
{{ '1970-01-01 00:00:00' | date: '%c %z' }}
```

#### Result
```html
Thu Jan  1 00:00:00 1970 -0800
```

### Converting time zones

Dates and times can be converted to specific time zones using the `time_zone` filter. On .NET 6 and later, identifiers are resolved with `TimeZoneInfo.FindSystemTimeZoneById`. The `netstandard2.0` asset uses TimeZoneConverter by default.

#### Example

```csharp
var context = new TemplateContext();
context.SetValue("published", DateTime.UtcNow);
```

```Liquid
{{ published | time_zone: 'America/New_York' | date: '%+' }}
```

On Windows, IANA identifiers require ICU globalization data. Resolution can fail on Windows versions that do not include ICU unless the application deploys it app-locally with the `Microsoft.ICU.ICU4C.Runtime` package. IANA identifiers are also unavailable when globalization invariant mode is enabled (`System.Globalization.Invariant`) or Windows NLS is forced (`System.Globalization.UseNls`). In these configurations, configure a custom resolver. For example, with the `TimeZoneConverter` package:

```csharp
var options = new TemplateOptionsBuilder()
    .WithTimeZoneResolver(id => TimeZoneConverter.TZConvert.GetTimeZoneInfo(id))
    .Build();
var context = new TemplateContext(options);
```

#### Result
```html
Tue Aug  1 17:04:36 -05:00 2017
```
