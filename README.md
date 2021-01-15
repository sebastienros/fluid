<p align="center"><img width=25% src="https://github.com/sebastienros/fluid/raw/dev/Assets/logo-vertical.png"></p>

[![Build status](https://ci.appveyor.com/api/projects/status/mhe85ihfdrlrci01/branch/master?svg=true)](https://ci.appveyor.com/project/SebastienRos/fluid/branch/master)
[![NuGet](https://img.shields.io/nuget/v/Fluid.Core.svg)](https://nuget.org/packages/Fluid.Core)
[![MIT](https://img.shields.io/github/license/sebastienros/fluid)](https://github.com/sebastienros/fluid/blob/dev/LICENSE)

## Basic Overview

Fluid is an open-source .NET template engine that is as close as possible to the [Liquid template language](https://shopify.github.io/liquid/). It's a **secure** template language that is also **very accessible** for non-programmer audiences. It also contains an ASP.NET Core MVC View Engine.

<br>

## Features

- Parses and renders Liquid templates.
- Supports **async** filters, templates can execute database queries more efficiently under load.
- Parses templates in a concrete syntax tree that lets you cache, analyze and alter the templates before they are rendered.
- Register any .NET types and properties, or define **custom handlers** to intercept when a named variable is accessed.
- Secure templates by white-listing all the available properties in the template.

<br>

## Contents
- [Features](#features)
- [Differences with Liquid](#differences-with-liquid)
- [Using Fluid in your project](#using-fluid-in-your-project)
- [White-listing object members](#white-listing-object-members)
- [Execution limits](#execution-limits)
- [Converting CLR types](#converting-clr-types)
- [Encoding](#encoding)
- [Localization](#localization)
- [Customizing tags and blocks](#customizing-tags-and-blocks)
- [ASP.NET MVC View Engine](#aspnet-mvc-view-engine)
- [Custom filters](#custom-filters)
- [Performance](#performance)
- [Used by](#used-by)

<br>

#### Source

```Liquid
<ul id="products">
  {% for product in products %}
    <li>
      <h2>{{product.name}}</h2>
      Only {{product.price | price }}

      {{product.description | prettyprint | paragraph }}
    </li>
  {% endfor %}
</ul>
```

#### Result

```html
<ul id="products">
    <li>
      <h2>Apple</h2>
      $329

      Flat-out fun.
    </li>
    <li>
      <h2>Orange</h2>
      $25

      Colorful. 
    </li>
    <li>
      <h2>Banana</h2>
      $99

      Peel it.
    </li>
</ul>
```

Notice
- The `<li>` tags are at the same index as in the template, even though the `{% for }` tag had some leading spaces
- The `<ul>` and `<li>` tags are on contiguous lines even though the `{% for }` is taking a full line.

<br>

## Using Fluid in your project

You can directly reference the [Nuget package](https://www.nuget.org/packages/Fluid.Core).

### Hello World

#### Source

```csharp
var parser = new FluidParser();

var model = new { Firstname = "Bill", Lastname = "Gates" };
var source = "Hello {{ p.Firstname }} {{ p.Lastname }}";

if (parser.TryParse(source, out var template))
{   
    var context = new TemplateContext(model);

    context.SetValue("p", model);

    Console.WriteLine(template.Render(context));
}
```

#### Result
`Hello Bill Gates`

### Thread-safety

A `FluidParser` instance is thread-safe, and should be shared by the whole application. A common pattern is declare the parser in a local static variable:

```c#
    private static readonly FluidParser _parser = new FluidParser();
```

A `IFluidTemplate` instance is thread-safe and can be cached and reused by multiple threads concurrently.

A `TemplateContext` instance is __not__ thread-safe and an instance should be created every time an `IFluidTemplate` instance is used.

<br>

## Adding custom filters

Filters can be **async** or not. They are defined as a `delegate` that accepts an **input**, a **set of arguments** and the current **context** of the rendering process.

Here is the `downcase` filter as defined in Fluid.

#### Source
```csharp
public static FluidValue Downcase(FluidValue input, FilterArguments arguments, TemplateContext context)
{
    return new StringValue(input.ToStringValue().ToLower());
}
```

#### Registration
Filters can be registered globally for the lifetime of the application, or for each usage of a template.

```csharp
TemplateContext.GlobalFilters.AddFilter('downcase', Downcase);

// Or for a specific context

var context = new TemplateContext();
context.Filters.AddFilter('downcase', Downcase);
```

To create an **async** filter use the `AddAsyncFilter` method instead.

<br>

## White-listing object members

Liquid is a secure template language which will only allow a predefined set of members to be accessed. Like filters, this can be done globally to the application  with `GlobalMemberAccessStrategy`, or for each context with `MemberAccessStrategy`. Even if a member is white-listed its value won't be able to be changed.

> Warning: To prevent concurrency issues you should always register global filters and members in a static constructor. Local ones can be defined at the time of usage.

### White-listing a specific type

This will allow any public field or property to be read from a template.

```csharp
TemplateContext.GlobalMemberAccessStrategy.Register<Person>();
``` 

### White-listing specific members

This will only allow the specific fields or properties to be read from a template.

```csharp
TemplateContext.GlobalMemberAccessStrategy.Register<Person>("Firstname", "Lastname");
``` 

### Intercepting a type access

This will provide a method to intercept when a member is accessed and either return a custom value or prevent it.

This example demonstrates how to intercept calls to a `JObject` and return the corresponding property.

```csharp
TemplateContext.GlobalMemberAccessStrategy.Register<JObject, object>((obj, name) => obj[name]);
``` 

### Inheritance

All the members of the class hierarchy are registered. Besides, all inherited classes will be correctly evaluated when a base class is registered and
a member of the base class is accessed.

<br>

### Object members casing

By default, the properties of a registered object are case sensitive and registered as they are in their source code. For instance, 
the property `FirstName` would be access using the `{{ p.FirstName }}` tag.

However it can be necessary to register these properties with different cases, like __Camel case__ (`firstName`), or __Snake case__ (`first_name`).

The following example configures the templates to use Camel casing.

```csharp
TemplateContext.GlobalMemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
```

## Execution limits

### Limiting templates recursion

When invoking `{% include 'sub-template' %}` statements it is possible that some templates create an infinite recursion that could block the server.
To prevent this the `TemplateContext` class defines a default `DefaultMaxRecursion = 100` that prevents templates from being have a depth greater than `100`.
This can be defined globally with this static member, or on an individual `TemplateContext` instance on its `MaxRecursion` property.

### Limiting templates execution

Template can inadvertently create infinite loop that could block the server by running indefinitely. 
To prevent this the `TemplateContext` class defines a default `DefaultMaxSteps`. By default this value is not set.
This can be defined globally with this static member, or on an individual `TemplateContext` instance on its `MaxSteps` property.

<br>

## Converting CLR types

Whenever an object is manipulated in a template it is converted to a specific `FluidValue` instance that provides a dynamic type system somehow similar to the one in JavaScript.

In Liquid they can be Number, String, Boolean, Array, or Dictionary. Fluid will automatically convert the CLR types to the corresponding Liquid ones, and also provides specialized ones.

To be able to customize this conversion you can either add **type mappings** or **value converters**.

### Adding a type mapping

The following example shows how to support `JObject` and `JValue` types to map their values to `FluidValue` instances.

First is solves the issue that a `JObject` implements `IEnumerable` and would be converted to an `ArrayValue` instead of an `ObjectValue`. Then we use `FluidValue.Create` to automatically convert the CLR value of the `JValue` object.

```csharp
FluidValue.SetTypeMapping<JObject>(o => new ObjectValue(o));
FluidValue.SetTypeMapping<JValue>(o => FluidValue.Create(o.Value));
```

> Note: Type mapping are defined globally for the application.

<br>

### Adding a value converter

When the conversion logic is not directly inferred from the type of an object, a value converter can be used.

Value converters can return:
- `null` to indicate that the value couldn't be converted
- a `FluidValue` instance to stop any further conversion and use this value
- another object instance to continue the conversion using custom and internal **type mappings**

The following example shows how to convert any instance implementing an interface to a custom string value:

```csharp
FluidValue.ValueConverters.Add((value) => value is IUser user ? user.Name : null);
```

> Note: Type mapping are defined globally for the application.

<br>

## Using Json.NET object in models

The classes that are used in Json.NET don't have direct named properties like classes, which makes them unusable out of the box
in a Liquid template.

To remedy that we can configure Fluid to map names to `JObject` properties, and convert `JValue` objects to the ones used by Fluid.

```csharp
// When a property of a JObject value is accessed, try to look into its properties
TemplateContext.GlobalMemberAccessStrategy.Register<JObject, object>((source, name) => source[name]);

// Convert JToken to FluidValue
FluidValue.TypeMappings.Add(typeof(JObject), o => new ObjectValue(o));
FluidValue.TypeMappings.Add(typeof(JValue), o => FluidValue.Create(((JValue)o).Value));

var expression = "{{ Model.Name }}";
var model = JObject.Parse("{\"Name\": \"Bill\"}");

if (FluidTemplate.TryParse(expression, out var template))
{
    var context = new TemplateContext();
    context.SetValue("Model", model);

    Console.WriteLine(template.Render(context));
}
```

<br>

## Encoding

By default Fluid doesn't encode the output. Encoders can be specified when calling `Render()` or `RenderAsync()` on the template.

### HTML encoding

To render a template with HTML encoding use the `System.Text.Encodings.Web.HtmlEncoder.Default` instance.

This encoder is used by default for the MVC View engine.

### Disabling encoding contextually

When an encoder is defined you can use a special `raw` filter or `{% raw %} ... {% endraw %}` tag to prevent a value from being encoded, for instance if you know that the content is HTML and is safe.

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

#### Source
```Liquid
{% capture breaktag %}<br />{% endcapture %}

{{ breaktag }}
```

#### Result
```html
<br />
```

<br>

## Localization

By default templates are rendered using an _invariant_ culture so that the results are consistent across systems. This is important for instance when rendering dates, times and numbers.

However it is possible to define a specific culture to use when rendering a template using the `TemplateContext.CultureInfo` property. 

#### Source

```csharp
var context = new TemplateContext();
context.CultureInfo = new CultureInfo("en-US");
var result = template.Render(context);
```

```Liquid
{{ 1234.56 }}
{{ "now" | date: "%v" }}
```

#### Result
```html
1234.56
Tuesday, August 1, 2017
```

<br>

## Customizing tags and blocks

Fluid's grammar can be modified to accept any new tags and blocks with 
any custom parameters. The parser is based on [Parlot](https://github.com/sebastienros/parlot) 
which makes it completely extensible.

Unlike blocks, tags don't have a closing element (e.g., `cycle`, `increment`).
A closing element will match the name of the opening tag with and `end` suffix, like `endfor`.
Blocks are useful when manipulating a section of a a template as a set of statements.

Fluid provides helper method to register common tags and blocks. All tags and block always start with an __identifier__ that is
the tag name.

Each custom tag needs to provide a delegate that is evaluated when the tag is matched. Each degate will be able to use these properties:

- `writer`, a `TextWriter` instance that is used to render some text.
- `encode`, a `TextEncoder` instance, like `HtmlEncoder`, or `NullEncoder`. It's defined by the caller of the template.
- `context`, a `TemplateContext` instance.

### Registering a custom tag

- __Empty__: Tag with no parameter, like `{% renderbody %}`
- __Identifier__: Tag taking an identifier as parameter, like `{% increment my_variable %}`

Here are some examples:

#### Source

```csharp
parser.RegisterIdentifierTag("hello", (identifier, writer, encoder, context) =>
{
    writer.Write("Hello ");
    writer.Write(identifier);
});
```

```Liquid
{% hello you %}
```

#### Result
```html
Hello you
```

### Creating a custom block

Blocks are created the same way as tags, and the lambda expression can then access the list of statements inside the block.

#### Source


```csharp

parser.RegisterExpressionBlock("repeat", (value, statements, writer, encoder, context) =>
{
    for (var i = 0; i < value.ToNumber(); i++)
    {
      await return statements.RenderStatementsAsync(writer, encoder, context);
    }

    return Completion.Normal;
});
```

```Liquid
{% repeat 1 | plus: 2 %}Hi! {% endrepeat %}
```

#### Result
```html
Hi! Hi! Hi!
```

<br>

## ASP.NET MVC View Engine

To provide a convenient view engine implementation for ASP.NET Core MVC the grammar is extended as described in [Customizing tags](#customizing-tags) by adding these new tags:

### Configuration

#### Registering the view engine

1- Reference the `Fluid.MvcViewEngine` NuGet package
2- Add a `using` statement on `FluidMvcViewEngine`
3- Call `AddFluid()` in your `Startup.cs`.

#### Sample
```csharp
using FluidMvcViewEngine;

public class Startup
{
  public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc().AddFluid();
    }
}
```
#### Registering view models

Because the Liquid language only accepts known members to be accessed, the View Model classes need to be registered in Fluid. Usually from a static constructor such that the code is run only once for the application.

#### View Model registration

```csharp
public class Startup
{
    static Startup()
    {
        TemplateContext.GlobalMemberAccessStrategy.Register<Person>();
    }
}
```

More way to register types and members can be found in the [White-listing object members](#white-listing-object-members) section.

#### Registering custom tags

When using the MVC View engine, custom tags can be added to the `FluidViewTemplate` class. Refer to [this section](https://github.com/sebastienros/fluid#creating-a-custom-tag) on how to create custom tags.

```csharp
public class Startup
{
    static Startup()
    {
        FluidViewTemplate.Factory.RegisterTag<MyTag>("mytag");
    }
}
```

### Layouts

#### Index.liquid

```Liquid
{% layout '_layout.liquid' %}

This is is the home page
```

The `{% layout [template] %}` tag accepts one argument which can be any expression that return the relative location of a liquid template that will be used as the master template.

The layout tag is optional in a view. It can also be defined multiple times or conditionally.

From a layout template the `{% renderbody %}` tag is used to depict the location of the view's content inside the layout itself.

#### Layout.liquid

```Liquid
<html>
  <body>
    <div class="menu"></div>
    
    <div class="content">
      {% renderbody %}
    </div>
    
    <div class="footer"></div>
  </body>
</html>
```

### Sections

Sections are defined in a layout as for views to render content in specific locations. For instance a view can render some content in a **menu** or a **footer** section.

#### Rendering content in a section

```Liquid
{% layout '_layout.liquid' %}

This is is the home page

{% section menu %}
  <a href="h#">This link goes in the menu</a>
{% endsection %}

{% section footer %}
  This text will go in the footer
{% endsection %}
```

#### Rendering the content of a section

```Liquid
<html>
  <body>
    <div class="menu">
      {% rendersection menu %}
    </div>
    
    <div class="content">
      {% renderbody %}
    </div>
    
    <div class="footer">
      {% rendersection footer %}
    </div>
  </body>
</html>
```

### ViewStart files

Defining the layout template in each view might me cumbersome and make it difficult to change it globally. To prevent that it can be defined in a `_ViewStart.liquid` file.

When a view is rendered all `_ViewStart.liquid` files from its current and parent directories are executed before. This means multiple files can be defined to defined settings for a group of views.

#### _ViewStart.liquid

```Liquid
{% layout '_layout.liquid' %}
{% assign background = 'ffffff' }
```

You can also define other variables or render some content.

### Custom views locations

It is possible to add custom file locations containing views by adding them to `FluidViewEngineOptions.ViewLocationFormats`.

The default ones are:
- `Views/{1}/{0}`
- `Views/Shared/{0}`

Where `{0}` is the view name, and `{1}` is the controller name.

### Execution

The content of a view is parsed once and kept in memory until the file or one of its dependencies changes. Once parsed, the tag are executed every time the view is called. To compare this with Razor, where views are first compiled then instantiated every time they are rendered. This means that on startup or when the view is changed, views with Fluid will run faster than those in Razor, unless you are using precompiled Razor views. In all cases Razor views will be faster on subsequent calls as they are compiled directly to C#.

This difference makes Fluid very adapted for rapid development cycles where the views can be deployed and updated frequently. And because the Liquid language is secure, developers give access to them with more confidence.  

<br>

## Custom filters

Some non-standard filters are provided by default

### format_date

Formats date and times using standard .NET date and time formats. It uses the current culture 
of the system.

Input

```
"now" | format_date: "G"
```

Output

```
6/15/2009 1:45:30 PM
```

Documentation: https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings

### format_number

Formats numbers using standard .NET number formats.

Input

```
123 | format_number: "N"
```

Output

```
123.00
```

Documentation: https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings

### format_string

Formats custom string using standard .NET format strings.

Input

```
"hello {0} {1:C}" | format_string: "world" 123
```

Output

```
hello world $123.00
```

Documentation: https://docs.microsoft.com/en-us/dotnet/api/system.string.format

<br>

## Performance

### Caching

Some performance boost can be gained in your application if you decide to cache the parsed templates before they are rendered. Even though parsing is memory-safe as it won't induce any compilation (meaning all the memory can be collected if you decide to parse a lot of templates), you can skip the parsing step by storing and reusing the `FluidTemplate` instance.

These object are thread-safe as long as each call to `Render()` uses a dedicated `TemplateContext` instance.

### Benchmarks

A benchmark application is provided in the source code to compare Fluid, [Scriban](https://github.com/scriban/scriban), [DotLiquid](https://github.com/dotliquid/dotliquid) and [Liquid.NET](https://github.com/mikebridge/Liquid.NET).
Run it locally to analyze the time it takes to execute specific templates.

#### Results

Fluid is faster and allocates less memory than all other well-known .NET Liquid parsers.
For parsing, Fluid is at least 25% faster than Scriban, allocating at least 50% less memory.
For rendering, Fluid is at least twice as fast as Scriban, allocating 25% less memory.
Compared to DotLiquid, Fluid is 6 times faster, and allocates 5 times less memory.

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.200-preview.20601.7
  [Host]   : .NET Core 5.0.2 (CoreCLR 5.0.220.61120, CoreFX 5.0.220.61120), X64 RyuJIT
  ShortRun : .NET Core 5.0.2 (CoreCLR 5.0.220.61120, CoreFX 5.0.220.61120), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|             Method |          Mean |      StdDev |  Ratio |     Gen 0 |    Gen 1 |   Gen 2 |   Allocated |
|------------------- |--------------:|------------:|-------:|----------:|---------:|--------:|------------:|
|        Fluid_Parse |      6.679 us |   0.0308 us |   1.00 |    0.6790 |        - |       - |     2.77 KB |
|      Scriban_Parse |      9.616 us |   0.0604 us |   1.44 |    1.8005 |        - |       - |     7.41 KB |
|    DotLiquid_Parse |     40.317 us |   2.5158 us |   6.04 |    2.6855 |        - |       - |    11.17 KB |
|    LiquidNet_Parse |     73.474 us |   0.2151 us |  11.00 |   15.1367 |   0.1221 |       - |    62.08 KB |
|                    |               |             |        |           |          |         |             |
|     Fluid_ParseBig |     38.491 us |   1.6535 us |   1.00 |    2.8076 |   0.0610 |       - |    11.69 KB |
|   Scriban_ParseBig |     49.321 us |   0.2074 us |   1.28 |    7.8125 |   1.0986 |       - |    32.05 KB |
| DotLiquid_ParseBig |    200.725 us |   2.8106 us |   5.22 |   13.1836 |   0.2441 |       - |    54.39 KB |
| LiquidNet_ParseBig | 24,370.888 us | 813.3676 us | 633.35 | 6812.5000 | 437.5000 |       - | 28557.53 KB |
|                    |               |             |        |           |          |         |             |
|       Fluid_Render |    626.010 us |  14.1571 us |   1.00 |   90.8203 |  58.5938 | 30.2734 |   390.80 KB |
|     Scriban_Render |  1,228.026 us |  15.3122 us |   1.96 |   99.6094 |  66.4063 | 66.4063 |   487.43 KB |
|   DotLiquid_Render |  5,403.001 us |  20.3199 us |   8.63 |  859.3750 | 171.8750 | 23.4375 |  3879.14 KB |
|   LiquidNet_Render |  3,339.111 us |  21.0156 us |   5.34 | 1000.0000 | 390.6250 |       - |  5324.50 KB |
```

Tested with 
- Scriban 3.3.2
- DotLiquid 2.0.366
- Liquid.NET 0.10.0

##### Legend

- Parse: Parses a simple HTML template containing filters and properties
- ParseBig: Parses a Blog Post template.
- Render: Renders a simple HTML template containing filters and properties, with 500 products.

## Used by

Fluid is known to be used in the following projects:
- [Orchard Core CMS](https://github.com/OrchardCMS/Orchard2)
- [MaltReport](https://github.com/oldrev/maltreport) OpenDocument/OfficeOpenXML powered reporting engine for .NET and Mono
- [Elsa Workflows](https://github.com/elsa-workflows/elsa-core) .NET Workflows Library

_Please file an issue to be listed here._
