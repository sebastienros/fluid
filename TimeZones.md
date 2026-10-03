# Time zone resolution in Fluid 2.x

The `time_zone` filter converts dates using an explicit time zone identifier:

```liquid
{{ published | time_zone: 'America/New_York' | date: '%+' }}
```

Fluid 2.x retains [TimeZoneConverter](https://github.com/mattjohnsonpint/TimeZoneConverter)
as a dependency on every target framework. The default `TemplateOptions.TimeZoneResolver`
uses `TZConvert.TryGetTimeZoneInfo`, preserving its IANA and Windows identifier resolution
and its behavior on Windows with NLS enabled. An identifier that cannot be resolved leaves
the date unchanged.

## Custom resolvers

Applications can assign `TemplateOptions.TimeZoneResolver` without changing other options
or the context's time zone. For example, to opt into the platform resolver:

```csharp
var options = new TemplateOptions
{
    TimeZoneResolver = TimeZoneInfo.FindSystemTimeZoneById
};
var context = new TemplateContext(options);
```

`TimeZoneInfo.FindSystemTimeZoneById` depends on the operating system's time zone data
and globalization configuration. In particular, IANA identifier support on Windows requires
ICU and is unavailable when NLS or globalization invariant mode is enabled. Opting into
the platform resolver may therefore change which identifiers resolve successfully.

A custom resolver receives the explicit identifier and must return a non-null `TimeZoneInfo`.
Throwing `TimeZoneNotFoundException` or `InvalidTimeZoneException` leaves the original date
and offset unchanged. Other exceptions propagate. A null result is unsupported and throws
instead of falling back to another resolver or time zone.

The case-insensitive special identifier `local` uses `TemplateContext.TimeZone` and bypasses
the resolver. A missing or nil identifier returns nil, preserving the existing 2.x behavior.
An empty string is an explicit identifier and is passed to the resolver; the default leaves
the date unchanged. Input that cannot be parsed as a date returns nil without invoking
the resolver.

For date parsing and formatting, see [Time zones in the README](README.md#time-zones).
