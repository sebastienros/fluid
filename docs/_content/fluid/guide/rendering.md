---
title: Parsing and rendering
description: "Details about parsing errors, scopes, undefined values, file providers, encoding and rendering performance."
---

## Parsing errors

`TryParse` returns parsing diagnostics as a string. Use `Parse` when you need structured
error information:

```csharp
try
{
    var template = parser.Parse(source);
}
catch (ParseException exception)
{
    if (exception.Position is { } position)
    {
        Console.WriteLine($"Error at line {position.Line}, column {position.Column} (offset {position.Offset})");
    }

    Console.WriteLine(exception.Message);
}
```

`ParseException.TemplateSource` contains the full original template, including when an
invalid template is loaded by an `include` or `render` tag or the view engine.
`Position` is a nullable `Parlot.TextPosition`: its offset is zero-based and its line
and column are one-based. Line numbers follow Parlot's LF-based counting (including CRLF,
but not standalone CR). Exceptions created without parsing metadata may have a
null source or position. The inherited `Exception.Source` property retains its standard
meaning and does not contain the template text.

The top-level grammar must consume the entire template. If a registered tag parser fails
without providing its own diagnostic, Fluid reports `Unexpected trailing input` at the
first unparsed character instead of accepting a partial template. Nested block grammars
still stop before their closing tags.

## Thread-safety and scopes

A `FluidParser` instance is thread-safe and should be shared by the whole application. A common pattern is to declare the parser in a local static variable:

```c#
    private static readonly FluidParser _parser = new FluidParser();
```

An `IFluidTemplate` instance is thread-safe and can be cached and reused by multiple threads concurrently.

A `TemplateContext` instance is __not__ thread-safe, and a new instance should be created every time an `IFluidTemplate` instance is used.

A `TemplateOptions` instance is immutable and thread-safe, and is created once with a `TemplateOptionsBuilder` (see [Reuse the `TemplateOptions` instance](#reuse-the-templateoptions-instance)). Values registered with `TemplateOptionsBuilder.WithGlobalValue` are exposed by `TemplateOptions.GlobalValues` and are shared by every context created from those options. Values set directly on a `TemplateContext` belong to that rendering. Custom tags that need temporary values should use a scope lease:

```csharp
using var scope = context.EnterScope(ScopeBehavior.Local);
context.SetValue("temporary", value);
```

`Local` scopes inherit values and keep assignments local. `WriteThrough` scopes keep values assigned with `LocalScope.SetOwnValue` temporary while normal assignments update the caller, which matches `include` and loop behavior. `Isolated` scopes can only read the context's initial values and `GlobalValues`, which matches the `render` tag.

## Handling undefined values

Fluid evaluates members lazily, so undefined identifiers can be detected precisely when they are consumed. By default, undefined values render as empty strings without raising errors.

### Tracking undefined values

To track missing values during template rendering, assign a delegate to `TemplateOptions.Undefined` or `TemplateContext.Undefined`. This delegate is called each time an undefined variable is accessed and receives the variable path and parent object type. The `type` argument is `null` when there is no target object (for example, when a global value is missing).

```csharp
var missingVariables = new List<string>();

var context = new TemplateContext();
context.Undefined = (name, type) =>
{
    missingVariables.Add(name);
    return ValueTask.FromResult<FluidValue>(NilValue.Instance);
};

var template = parser.Parse("Hello {{ user.name }} in {{ city }}!");

await template.RenderAsync(context);

// missingVariables now contains ["user.name", "city"]
```

### Strict variables

If you prefer templates to fail fast when they reference a variable that does not exist, enable strict variable mode by calling `TemplateOptionsBuilder.WithStrictVariables()`. When `StrictVariables` is `true`, any attempt to access an undefined variable throws a `FluidException` containing the variable name. This makes missing data issues visible immediately instead of silently rendering as an empty string.

```csharp
var options = new TemplateOptionsBuilder()
    .WithStrictVariables()
    .Build();
var context = new TemplateContext(options);

// Parsing a template that references an undefined variable
var template = parser.Parse("Hello {{ user.name }}!");

// Throws FluidException: Undefined variable 'user'
await template.RenderAsync(context);
```

When `StrictVariables` is disabled (the default), you can still track missing variables using the `Undefined` delegate described above, or provide fallback values by returning a custom `FluidValue`.

### Strict filters

By default, applying an unknown filter simply returns the input value unchanged:

```liquid
{{ 'hello' | unknown }}  => hello
```

If you would rather fail fast when a template references a filter that has not been registered, enable strict filter mode by calling `TemplateOptionsBuilder.WithStrictFilters()`:

```csharp
var options = new TemplateOptionsBuilder()
    .WithStrictFilters()
    .Build();
var context = new TemplateContext(options);

var template = parser.Parse("{{ 'hello' | unknown }}");
// Throws FluidException: Undefined filter 'unknown'
await template.RenderAsync(context);
```

Known filters continue to work normally when `StrictFilters` is enabled:

```liquid
{{ 'hello' | upcase }}  => HELLO
```

Use `StrictFilters` together with `StrictVariables` to enforce both variable and filter correctness during authoring.

### Returning custom values for undefined values

The `Undefined` delegate can return a custom `FluidValue` to provide fallback values or error messages for missing values:

```csharp
var options = new TemplateOptionsBuilder()
    .WithUndefined((name, type) =>
    {
        // Return a custom default value for undefined variables
        return ValueTask.FromResult<FluidValue>(StringValue.Create($"[{name} not found]"));
    })
    .Build();

var template = parser.Parse("Hello {{ user.name }} in {{ city }}!");
var context = new TemplateContext(options);

var result = await template.RenderAsync(context);
// Outputs: "Hello [user.name not found] in [city not found]!"
```

### Logging undefined accesses

You can use the `Undefined` delegate to log missing values for debugging or monitoring:

```csharp
var options = new TemplateOptionsBuilder()
    .WithUndefined((path, type) =>
    {
        Console.WriteLine($"Missing variable: {path}, parent type: {type?.Name ?? "<none>"}");
        return ValueTask.FromResult<FluidValue>(NilValue.Instance);
    })
    .Build();

var template = parser.Parse("{{ first }} {{ second }}");
var context = new TemplateContext(options);
await template.RenderAsync(context);
// Logs: "Missing variable: first"
// Logs: "Missing variable: second"
```

## Loading templates asynchronously

`TemplateOptions.FileProvider` uses the asynchronous `ITemplateFileProvider` contract. Templates stored in a remote service can be loaded without blocking:

```csharp
var options = new TemplateOptionsBuilder()
    .WithFileProvider(new DelegateTemplateFileProvider(async (path, context, cancellationToken) =>
    {
        var metadata = await templateStore.GetMetadataAsync(path, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        return new TemplateSourceInfo(
            metadata.LastModified,
            async cancellationToken => await templateStore.OpenReadAsync(path, cancellationToken),
            cacheKey: $"{tenantId}:{path}");
    }))
    .Build();

var context = new TemplateContext(options)
{
    CancellationToken = requestAborted
};
var result = await template.RenderAsync(context);
```

The provider returns `null` when a path does not exist. Fluid tries the requested path first, then appends `DefaultFileExtension` when necessary.

Fluid calls the provider to obtain the source version before checking its parsed-template cache. The stream is opened only on a cache miss. `LastModified` must advance whenever the content changes; remote implementations can cache metadata themselves if checking it requires a network request. When a provider can return different content for the same path in different contexts, set `TemplateSourceInfo.CacheKey` to a stable value that includes the tenant or other source identity.

Existing `Microsoft.Extensions.FileProviders.IFileProvider` implementations can be adapted:

```csharp
var options = new TemplateOptionsBuilder()
    .WithFileProvider(new FileProviderTemplateFileProvider(existingFileProvider))
    .Build();
```

`FluidViewEngineOptions.ViewsFileProvider` and `PartialsFileProvider` use the same asynchronous contract for views, layouts, `_ViewStart` files, and partials. ASP.NET Core MVC view-name discovery remains synchronous because `IViewEngine.FindView` has no asynchronous contract; configure `FluidMvcViewOptions.ViewLocationFileProvider` with an `IFileProvider` for that lookup.

Always use `RenderAsync` with an asynchronous provider. The synchronous `Render` APIs must block if the provider suspends.

`TemplateContext.CancellationToken` is also checked at render entry, between statements, on loop iterations and built-in array enumeration, and before buffered output is flushed. Custom filters and `FluidValue` implementations receive the context and should check the token during long-running work.

## Encoding

By default, Fluid doesn't encode the output. Encoders can be specified when calling `Render()` or `RenderAsync()` on the template.

### HTML encoding

To render a template with HTML encoding, use the `System.Text.Encodings.Web.HtmlEncoder.Default` instance.

`HtmlEncoder.Default` only allows Basic Latin, so it writes any other character as a numeric entity (`é` becomes `&#xE9;`). To keep non-ASCII text readable, use `HtmlEncoder.Create(UnicodeRanges.All)`, which still encodes `<`, `>`, `&`, quotes and characters outside the Basic Multilingual Plane such as emoji. The view engines fall back to that encoder, see [Encoding views](view-engine.md#encoding-views).

### Arrays

An array written directly, as in `{{ tags }}`, renders each item the same way as `{% for tag in tags %}{{ tag }}{% endfor %}`, including nested arrays and custom `FluidValue` renderers. Items that are marked as not encoded, for instance with `StringValue.Create(text, encode: false)`, are written as they are. Numbers use the render culture and the same formatting as individually rendered numbers (for example, `2.50` renders as `2.5` with the invariant culture).

### Disabling encoding contextually

When an encoder is defined, you can use a special `raw` filter or `{% raw %} ... {% endraw %}` tag to prevent a value from being encoded, for instance if you know that the content is HTML and is safe.

#### Source
```Liquid
{% assign html = '<em>This is some html</em>' %}

Encoded: {{ html }}
Not encoded: {{ html | raw }
```

#### Result
```html
&lt;em%gt;This is some html&lt;/em%gt;
<em>This is some html</em>
```

### Captured blocks are not double-encoded

When using `capture` blocks, the inner content is flagged as 
pre-encoded and won't be double-encoded if used in a `{{ }}` tag.

The body of a `capture` always runs, so its tags take effect even when the result is empty or only whitespace: `{% capture x %}{% assign y = 2 %}{% endcapture %}` assigns `y`, whitespace is kept (`{% capture x %}  {% endcapture %}` stores two spaces), and `{% break %}` or `{% continue %}` in a capture stop the enclosing loop. The `TemplateOptionsBuilder.WithCaptured()` callback is invoked for every capture, including empty ones.

### Customizing JSON output

The `json` filter uses `System.Text.Json.JsonSerializerOptions` to control the JSON output format. You can customize these options through `TemplateOptionsBuilder.WithJsonSerializerOptions` or `TemplateContext.JsonSerializerOptions`.

#### Example: Indented JSON output

```csharp
var options = new TemplateOptionsBuilder()
    .WithJsonSerializerOptions(new JsonSerializerOptions
    {
        WriteIndented = true
    })
    .Build();

var context = new TemplateContext(options);
context.SetValue("data", new { name = "John", age = 30 });
```

```Liquid
{{ data | json }}
```

#### Result
```json
{
  "name": "John",
  "age": 30
}
```

You can also set `JsonSerializerOptions` per `TemplateContext`, but it is recommended to reuse `JsonSerializerOptions` instances and define them in a `TemplateOptions` instance that can be reused across `TemplateContext` instances.

### JSON encoding

By default, all JSON strings are encoded using the default `JavaScriptEncoder` instance. This can be changed by setting the `JsonSerializerOptions.JavaScriptEncoder` property to `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`.

```Liquid
{{ "你好，这是一条短信" | json" }}
```

With the default JSON encoder:

```html
"\u4F60\u597D\uFF0C\u8FD9\u662F\u4E00\u6761\u77ED\u4FE1"
```

Using the relaxed JSON encoding:

```csharp
// This variable should be static and reused for all template contexts
var options = new TemplateOptionsBuilder()
    .WithJsonSerializerOptions(new JsonSerializerOptions
    {
        JavaScriptEncoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    })
    .Build();

var context = new TemplateContext(options);
```

Result:

```html
"你好，这是一条短信"
```

## Performance

Fluid is fast, but only if you follow these best practices:

### Cache `IFluidTemplate` instances

It is common for the same templates to be rendered over time. In this case, it is beneficial to cache the resulting `IFluidTemplate` instance from the `FluidParser.Parse()` method. You can use the template name with a timestamp or its content as the cache key. If your templates can evolve, ensure that the cache is not unbounded and entries eventually get evicted. The recommended approach is to use a singleton `IMemoryCache` that can be configured with size limits and eviction time.

`IFluidTemplate` instances are thread-safe for read access and can be shared by multiple concurrent threads.

### Reuse the `TemplateOptions` instance

These instances are meant to be reused. This is why there is a separation between `TemplateContext`, which is per rendering, and `TemplateOptions`, which contains state that is shared across all renderings, such as property resolutions and lambdas. A convenient approach is to declare them as `static`, though you should adapt this to your needs.

`TemplateOptions` instances are immutable: they are created with a `TemplateOptionsBuilder` and can't be modified afterwards, so they can be shared by multiple concurrent threads without any precaution. The filters, global values, value converters and member access registrations are copied when `Build()` is called, so changing the builder later doesn't affect options that are already in use. Call `ToBuilder()` on an existing instance to create a variation, for instance one per tenant.

```csharp
private static readonly TemplateOptions _options = new TemplateOptionsBuilder()
    .WithCultureInfo(new CultureInfo("en-US"))
    .AddFilter("downcase", Downcase)
    .ConfigureMemberAccess(strategy => strategy.Register<Person, object>((person, name) => person.Firstname))
    .Build();

private static readonly TemplateOptions _tenantOptions = _options.ToBuilder()
    .WithMaxSteps(10_000)
    .Build();
```

`TemplateOptions.Default` is also immutable, so a `TemplateContext` created without options can't alter configuration shared with other renderings. Use `TemplateContext` properties such as `CultureInfo`, `TimeZone` or `MoneyOptions` for overrides that apply to a single rendering.

Objects that are provided to the builder, like the `ITemplateFileProvider`, the `ITemplateCache`, the delegates, or the `JsonSerializerOptions`, are used as-is, and remain responsible for their own thread-safety. A custom `MemberAccessStrategy` is created by a factory passed to `WithMemberAccessStrategy()`, which is invoked by every `Build()` call so that options never share a strategy that can be modified.

### Reuse the `FluidParser` instance

Instantiating a `FluidParser` instance is expensive, do it once and reuse the instance. This can be registered as a singleton if you use dependency injection, but in most cases a `static` instance makes sense since it's rare to customize these.

### Render directly to UTF-8

On .NET 8 and later, `Utf8FluidOutput` writes directly to an `IBufferWriter<byte>` without accumulating the rendered response as UTF-16 or adding an ASP.NET Core dependency to Fluid:

```csharp
await template.RenderAsync(response.BodyWriter, HtmlEncoder.Default, context);

// The destination remains caller-owned.
await response.BodyWriter.FlushAsync(requestAborted);
```

The `RenderAsync` overload creates and disposes `Utf8FluidOutput`, ensuring its UTF-8 encoder state is finalized. The destination remains caller-owned and is not flushed or completed. Create `Utf8FluidOutput` directly when multiple templates need to share one character stream. Its `FlushAsync` method transcodes buffered characters while preserving encoder state so surrogate pairs remain valid across intermediate template flushes. Because `IFluidOutput` writes synchronously, transport flushing is not attempted in the middle of a render. `MaxOutputSize` continues to count UTF-16 characters, not encoded bytes.
