---
title: Getting started
description: Install Fluid, render your first template, and set it up the way it is meant to be used in an application.
---

Fluid is an open-source .NET template engine based on the [Liquid template language](https://shopify.github.io/liquid/).
It is fast, allocates little, and is safe for templates written by users: they can only read the data you give them.

> [!TIP]
> You can try every sample of this documentation in the [playground](/fluid/playground/), which runs Fluid in
> your browser.

## Install the package

Fluid is published on [NuGet](https://www.nuget.org/packages/Fluid.Core) as `Fluid.Core`:

```shell
dotnet add package Fluid.Core
```

The samples of this documentation assume `using Fluid;`.

Previews of the next version are published to a [feedz.io feed](https://f.feedz.io/sebastienros/fluid/nuget/index.json)
after every push to `main`:

```shell
dotnet nuget add source https://f.feedz.io/sebastienros/fluid/nuget/index.json --name fluid-preview
dotnet add package Fluid.Core --prerelease
```

## Render a template

A `FluidParser` turns the source of a template into an `IFluidTemplate`, and a `TemplateContext` holds the values a
rendering can read:

```csharp
var parser = new FluidParser();

var model = new { Firstname = "Bill", Lastname = "Gates" };
var source = "Hello {{ Firstname }} {{ Lastname }}";

if (parser.TryParse(source, out var template, out var error))
{
    var context = new TemplateContext(model);

    Console.WriteLine(template.Render(context)); // Hello Bill Gates
}
else
{
    Console.WriteLine($"Error: {error}");
}
```

`TryParse` returns parsing errors as a string. `Parse` throws a `ParseException` that has the line and column of the
error, see [Parsing errors](rendering.md#parsing-errors).

## Reuse instances

Parsing and configuration are meant to happen once, and rendering many times:

- A `FluidParser` is thread-safe and expensive to create: create it once and share it, for instance in a `static`
  field.
- An `IFluidTemplate` is thread-safe: cache parsed templates and render them concurrently.
- A `TemplateOptions` is immutable and thread-safe: build it once with a `TemplateOptionsBuilder`, and call
  `ToBuilder()` to derive a variation.
- A `TemplateContext` is not thread-safe: create a new one for every rendering.

```csharp
private static readonly FluidParser Parser = new FluidParser();

private static readonly TemplateOptions Options = new TemplateOptionsBuilder()
    .WithCultureInfo(new CultureInfo("en-US"))
    .WithStrictVariables()
    .Build();

public static ValueTask<string> RenderAsync(IFluidTemplate template, object model)
{
    return template.RenderAsync(new TemplateContext(model, Options), HtmlEncoder.Default);
}
```

## Go further

| To | Read |
| --- | --- |
| Learn how Fluid reads Liquid, and enable `macro`, parentheses or escape sequences | [Liquid syntax](syntax.md) |
| Expose .NET objects, convert values, register members | [Models and values](models-and-values.md) |
| Add your own filters, tags, blocks and operators | [Extending the grammar](extensibility.md) |
| Render templates written by users | [Security](security.md) |
| Load partials, handle undefined values, encode the output | [Parsing and rendering](rendering.md) |
| Use culture-aware filters, dates and money | [Filters](filters.md) and [Cultures, dates and time zones](localization.md) |
| Render Liquid views in ASP.NET Core | [View engines](view-engine.md) |
| Avoid reflection, or compile templates at build time | [Model accessor generator](model-accessor-generator.md) and [Compiled template generator](template-generator.md) |
