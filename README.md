<p align="center"><img width=25% src="https://github.com/sebastienros/fluid/raw/main/Assets/logo-vertical.png"></p>

[![NuGet](https://img.shields.io/nuget/v/Fluid.Core.svg)](https://nuget.org/packages/Fluid.Core)
[![NuGet](https://img.shields.io/nuget/vpre/Fluid.Core.svg)](https://nuget.org/packages/Fluid.Core)
[![MIT](https://img.shields.io/github/license/sebastienros/fluid)](https://github.com/sebastienros/fluid/blob/main/LICENSE)

## Basic Overview

Fluid is an open-source .NET template engine based on the [Liquid template language](https://shopify.github.io/liquid/). It's a **secure** template language that is also **very accessible** for non-programmer audiences.

> The following content is based on the 2.0.0-beta version, which is the recommended version even though some of its API might vary significantly.
To see the corresponding content for v1.0 use [this version](https://github.com/sebastienros/fluid/blob/release/1.x/README.md)

<br>

## Features

- Very fast Liquid parser and renderer (no-regexp), with few allocations. See [benchmarks](#performance).
- Secure templates by allow-listing all the available properties in the template. User templates can't break your application.
- Supports **async** filters. Templates can execute database queries more efficiently under load.
- Customize filters and tag with your own. Even with complex grammar constructs. See [Customizing tags and blocks](#customizing-tags-and-blocks)
- Parses templates in a concrete syntax tree that lets you cache, analyze and alter the templates before they are rendered.
- Register any .NET types and properties, or define **custom handlers** to intercept when a named variable is accessed.

<br>

## Contents
- [Features](#features)
- [Using Fluid in your project](#using-fluid-in-your-project)
- [Allow-listing object members](#allow-listing-object-members)
- [Execution limits](#execution-limits)
- [Converting CLR types](#converting-clr-types)
- [Encoding](#encoding)
- [Localization](#localization)
- [Time zones](#time-zones)
- [Customizing tags and blocks](#customizing-tags-and-blocks)
- [ASP.NET MVC View Engine](#aspnet-mvc-view-engine)
- [Whitespace control](#whitespace-control)
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
var source = "Hello {{ Firstname }} {{ Lastname }}";

if (parser.TryParse(source, out var template, out var error))
{   
    var context = new TemplateContext(model);

    Console.WriteLine(template.Render(context));
}
else
{
    Console.WriteLine($"Error: {error}");
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
public static ValueTask<FluidValue> Downcase(FluidValue input, FilterArguments arguments, TemplateContext context)
{
    return new StringValue(input.ToStringValue().ToLower());
}
```

#### Registration
Filters are registered in an instance of `TemplateOptions`. This options object can be reused every time a template is rendered.

```csharp
var options = new TemplateOptions();
options.Filters.AddFilter('downcase', Downcase);

var context = new TemplateContext(options);
```

<br>

## Allow-listing object members

Liquid is a secure template language which will only allow a predefined set of members to be accessed, and where model members can't be changed. 
Property are added to the `TemplateOptions.MemberAccessStrategy` property. This options object can be reused every time a template is rendered.

Alternatively, the `MemberAccessStrategy` can be assigned an instance of `UnsafeMemberAccessStrategy` which will allow any property to be accessed.

### Allow-listing a specific type

This will allow any public field or property to be read from a template.

```csharp
var options = new TemplateOptions();
options.MemberAccessStrategy.Register<Person>();
``` 

> Note: When passing a model with `new TemplateContext(model)` the type of the `model` object is automatically registered. This behavior can be disable
by calling `new TemplateContext(model, false)`

### Allow-listing specific members

This will only allow the specific fields or properties to be read from a template.

```csharp
var options = new TemplateOptions();
options.MemberAccessStrategy.Register<Person>("Firstname", "Lastname");
``` 

### Intercepting a type access

This will provide a method to intercept when a member is accessed and either return a custom value or prevent it.

NB: If the model implements `IDictionary` or any similar generic dictionary types the dictionary access has priority over the custom accessors.

This example demonstrates how to intercept calls to a `Person` and always return the same property.

```csharp
var model = new Person { Name = "Bill" };

var options = new TemplateOptions();
options.MemberAccessStrategy.Register<Person, object>((obj, name) => obj.Name);
``` 

### Customizing object accessors

To provide advanced customization for specific types, it is recommended to use value converters and a custom `FluidValue` implementation by inheriting from `ObjectValueBase`.

The following example show how to provide a custom transformation for any `Person` object:

```csharp
private class PersonValue : ObjectValueBase
{
    public PersonValue(Person value) : base(value)
    {
    }

    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
    {
        return Create(((Person)Value).Firstname + "!!!" + index.ToStringValue(), context.Options);
    }
}
```

This custom type can be used with a converter such that any time a `Person` is used, it is wrapped as a `PersonValue`.

```csharp
var options = new TemplateOptions();
options.ValueConverters.Add(o => o is Person p ? new PersonValue(p) : null);
```

It can also be used to replace custom member access by customizing `GetValueAsync`, or do custom conversions to standard Fluid types. 

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
var options = new TemplateOptions();
options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
```

## Execution limits

### Limiting templates recursion

When invoking `{% include 'sub-template' %}` statements it is possible that some templates create an infinite recursion that could block the server.
To prevent this the `TemplateOptions` class defines a default `MaxRecursion = 100` that prevents templates from being have a depth greater than `100`.

### Limiting templates execution

Template can inadvertently create infinite loop that could block the server by running indefinitely. 
To prevent this the `TemplateOptions` class defines a default `MaxSteps`. By default this value is not set.

<br>

## Converting CLR types

Whenever an object is manipulated in a template it is converted to a specific `FluidValue` instance that provides a dynamic type system somehow similar to the one in JavaScript.

In Liquid they can be Number, String, Boolean, Array, Dictionary, or Object. Fluid will automatically convert the CLR types to the corresponding Liquid ones, and also provides specialized ones.

To be able to customize this conversion you can add **value converters**.

### Adding a value converter

When the conversion logic is not directly inferred from the type of an object, a value converter can be used.

Value converters can return:
- `null` to indicate that the value couldn't be converted
- a `FluidValue` instance to stop any further conversion and use this value
- another object instance to continue the conversion using custom and internal **type mappings**

The following example shows how to convert any instance implementing an interface to a custom string value:

```csharp
var options = new TemplateOptions();

options.ValueConverters.Add((value) => value is IUser user ? user.Name : null);
```

> Note: Type mapping are defined globally for the application.

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
var options = new TemplateOptions();
options.CultureInfo = new CultureInfo("en-US");
var context = new TemplateContext(options);
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

## Time zones

### System time zone

`TemplateOptions` and `TemplateContext` provides a property to define a default time zone to use when parsing date and times. The default value is the current system's time zone.
When dates and times are parsed and don't specify a time zone, the default one is assumed. Setting a custom one can also prevent different environments (data centers) from
generating different results.

> Note: The `date` filter conforms to the Ruby date and time formats https://ruby-doc.org/core-3.0.0/Time.html#method-i-strftime. To use the .NET standard date formats, use the `format_date` filter.

#### Source

```csharp
var context = new TemplateContext { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time") } ;
var result = template.Render(context);
```

```Liquid
{{ '1970-01-01 00:00:00' | date: '%c' }}
```

#### Result
```html
Wed Dec 31 19:00:00 -08:00 1969
```

### Converting time zones

Dates and times can be converted to specific time zones using the `time_zone: <iana>` filter.

#### Example

```csharp
var context = new TemplateContext();
context.SetValue("published", DateTime.UtcNow);
```

```Liquid
{{ published | time_zone: 'America/New_York' | date: '%+' }}
```

#### Result
```html
Tue Aug  1 17:04:36 -05:00 2017
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
- __Expression__: Tag taking an expression as parameter, like `{% layout 'home' | append: '.liquid' %}`

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

### Registering a custom block

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

### Custom parsers

If __identifier__, __empty__ and __expression__ parsers are not sufficient, the methods `RegisterParserBlock` and `RegisterParserTag` accept
any custom parser construct. These can be the standard ones defined in the `FluidParser` class, like `Primary`, or any other composition of them.

For instance, `RegisterParseTag(Primary.AndSkip(Comma).And(Primary), ...)` will expect two `Primary` elements separated by a comma. The delegate will then 
be invoked with a `ValueTuple<Expression, Expression>` representing the two `Primary` expressions.

### Registering a custom operator

Operator are used to compare values, like `>` or `contains`. Custom operators can be defined if special comparisons need to be provided.

#### Source

The following example creates a custom `xor` operator that will evaluate to `true` if only one of the left and right expressions is true when converted to booleans.

__XorBinaryExpression.cs__

```csharp
using Fluid.Ast;
using Fluid.Values;
using System.Threading.Tasks;

namespace Fluid.Tests.Extensibility
{
    public class XorBinaryExpression : BinaryExpression
    {
        public XorBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            return BooleanValue.Create(leftValue.ToBooleanValue() ^ rightValue.ToBooleanValue());
        }
    }
}
```

__Parser configuration__

```csharp
parser.RegisteredOperators["xor"] = (a, b) => new XorBinaryExpression(a, b);
```

__Usage__

```Liquid
{% if true xor false %}Hello{% endif %}
```

#### Result
```html
Hello
```

<br>

## ASP.NET MVC View Engine

The package `Fluid.MvcViewEngine` provides a convenient way to use Liquid as a replacement or in combination of Razor in ASP.NET MVC.

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

View models are automatically registered and available as the root object in liquid templates.
Custom model regsitrations can be added when calling `AddFluid()`.

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc().AddFluid(o => o.TemplateOptions.Register<Person>());
    }
}
```

More way to register types and members can be found in the [Allow-listing object members](#allow-listing-object-members) section.

#### Registering custom tags

When using the MVC View engine, custom tags can still be added to the parser. Refer to [this section](https://github.com/sebastienros/fluid#registering-a-custom-tag) on how to create custom tags.

It is recommended to create a custom class inheriting from `FluidViewParser`, and to customize the tags in the constructor of this new class.
This class can then be registered as the default parser for the MVC view engine.

```csharp
using Fluid.Ast;
using Fluid.MvcViewEngine;

namespace Fluid.MvcSample
{
    public class CustomFluidViewParser : FluidViewParser
    {
        public CustomFluidViewParser()
        {
            RegisterEmptyTag("mytag", static async (s, w, e, c) =>
            {
                await w.WriteAsync("Hello from MyTag");

                return Completion.Normal;
            });
        }
    }
}
```

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<MvcViewOptions>(options =>
        {
            options.Parser = new CustomFluidViewParser();
        });

        services.AddMvc().AddFluid();
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

It is possible to add custom file locations containing views by adding them to `MvcViewOptions.ViewsLocationForm`.

The default ones are:
- `Views/{1}/{0}`
- `Views/Shared/{0}`

Where `{0}` is the view name, and `{1}` is the controller name.

### Execution

The content of a view is parsed once and kept in memory until the file or one of its dependencies changes. Once parsed, the tag are executed every time the view is called. To compare this with Razor, where views are first compiled then instantiated every time they are rendered. This means that on startup or when the view is changed, views with Fluid will run faster than those in Razor, unless you are using precompiled Razor views. In all cases Razor views will be faster on subsequent calls as they are compiled directly to C#.

This difference makes Fluid very adapted for rapid development cycles where the views can be deployed and updated frequently. And because the Liquid language is secure, developers give access to them with more confidence.  

<br>

## View Engine

The Fluid ASP.NET MVC View Engine is based on an MVC agnostic view engine provided in the `Fluid.ViewEngine` package. The same options and features are available, but without 
requiring ASP.NET MVC. This is useful to provide the same experience to build template using layouts and sections.

### Usage

Use the class `FluidViewRenderer : IFluidViewRender` and `FluidViewEngineOptions`. 



## Whitespace control

Liquid follows strict rules with regards to whitespace support. By default all spaces and new lines are preserved from the template.
The Liquid syntax and some Fluid options allow to customize this behavior.

### Hyphens

For example:

```liquid
{%  assign name = "Bill" %}
{{ name }}
```

There is a new line after the `assign` tag which will be preserved.

Outputs:

```

Bill
```

Tags and values can use hyphens to strip whitespace. 

Example:

```liquid
{%  assign name = "Bill" -%}
{{ name }}
```

Outputs:

```
Bill
```

The `-%}` strips the whitespace from the right side of the `assign` tag.

## Template Options

Fluid provides the `TemplateOptions.Trimming` property that can be set with predefined preferences for when whitespace should be stripped automatically, even if hyphens are not
present in tags and output values.

## Greedy Mode

When greedy model is disabled in `TemplateOptions.Greedy`, only the spaces before the first new line are stripped.
Greedy mode is enabled by default since this is the standard behavior of the Liquid language.

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

A benchmark application is provided in the source code to compare Fluid, [Scriban](https://github.com/scriban/scriban), [DotLiquid](https://github.com/dotliquid/dotliquid), [Liquid.NET](https://github.com/mikebridge/Liquid.NET) and Handlebars.NET (https://github.com/Handlebars-Net).
Run it locally to analyze the time it takes to execute specific templates.

#### Results

Fluid is faster and allocates less memory than all other well-known .NET Liquid parsers.
For parsing, Fluid is 30% faster than Scriban, allocating 2 times less memory.
For rendering, Fluid is slightly faster than Handlebars, 3 times faster than Scriban, but allocates at least half the memory.
Compared to DotLiquid, Fluid renders 9 times faster, and allocates 30 times less memory.

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.22000
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=6.0.100-rc.2.21505.57
  [Host]   : .NET Core 6.0.0 (CoreCLR 6.0.21.48005, CoreFX 6.0.21.48005), X64 RyuJIT
  ShortRun : .NET Core 6.0.0 (CoreCLR 6.0.21.48005, CoreFX 6.0.21.48005), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|             Method |          Mean |         Error |        StdDev |  Ratio | RatioSD |     Gen 0 |    Gen 1 |   Gen 2 |   Allocated |
|------------------- |--------------:|--------------:|--------------:|-------:|--------:|----------:|---------:|--------:|------------:|
|        Fluid_Parse |      6.387 us |      1.815 us |     0.0995 us |   1.00 |    0.00 |    0.7019 |        - |       - |     2.88 KB |
|      Scriban_Parse |      8.731 us |      2.153 us |     0.1180 us |   1.37 |    0.03 |    1.7853 |        - |       - |     7.32 KB |
|    DotLiquid_Parse |     26.626 us |     74.726 us |     4.0960 us |   4.18 |    0.70 |    3.9673 |        - |       - |    16.21 KB |
|    LiquidNet_Parse |     90.636 us |     54.499 us |     2.9873 us |  14.19 |    0.30 |   15.1367 |   0.1221 |       - |    62.08 KB |
|   Handlebars_Parse |  3,942.611 us |  1,134.420 us |    62.1814 us | 617.40 |   15.44 |   39.0625 |        - |       - |   159.38 KB |
|                    |               |               |               |        |         |           |          |         |             |
|     Fluid_ParseBig |     37.779 us |     14.975 us |     0.8208 us |   1.00 |    0.00 |    3.1738 |        - |       - |    13.02 KB |
|   Scriban_ParseBig |     44.500 us |     13.322 us |     0.7303 us |   1.18 |    0.02 |    8.1787 |        - |       - |    33.54 KB |
| DotLiquid_ParseBig |     79.929 us |    123.318 us |     6.7595 us |   2.12 |    0.16 |   23.0713 |        - |       - |    94.37 KB |
| LiquidNet_ParseBig | 34,916.067 us | 63,809.961 us | 3,497.6393 us | 924.79 |   97.54 | 6812.5000 | 500.0000 |       - | 28543.69 KB |
|                    |               |               |               |        |         |           |          |         |             |
|       Fluid_Render |    386.133 us |    178.984 us |     9.8107 us |   1.00 |    0.00 |   22.9492 |        - |       - |    95.45 KB |
|     Scriban_Render |  1,260.079 us |    171.254 us |     9.3870 us |   3.26 |    0.09 |   95.7031 |  66.4063 | 66.4063 |    486.6 KB |
|   DotLiquid_Render |  3,586.364 us |    742.398 us |    40.6933 us |   9.29 |    0.28 |  730.4688 | 214.8438 | 27.3438 |  3364.13 KB |
|   LiquidNet_Render |  2,490.968 us |  1,792.951 us |    98.2777 us |   6.45 |    0.10 |  515.6250 | 257.8125 |       - |  3144.11 KB |
|  Handlebars_Render |    409.511 us |    139.444 us |     7.6434 us |   1.06 |    0.05 |   47.3633 |  11.7188 |       - |   195.11 KB |
```

Tested on 10/26/2021 with 
- Scriban 5.0.0
- DotLiquid 2.2.548
- Liquid.NET 0.10.0
- Handlebars.Net 2.0.9

##### Legend

- Parse: Parses a simple HTML template containing filters and properties
- ParseBig: Parses a Blog Post template.
- Render: Renders a simple HTML template containing filters and properties, with 500 products.

## Used by

Fluid is known to be used in the following projects:
- [Orchard Core CMS](https://github.com/OrchardCMS/Orchard2)
- [MaltReport](https://github.com/oldrev/maltreport) OpenDocument/OfficeOpenXML powered reporting engine for .NET and Mono
- [Elsa Workflows](https://github.com/elsa-workflows/elsa-core) .NET Workflows Library
- [FluentEmail](https://github.com/lukencode/FluentEmail/) All in one email sender for .NET

_Please file an issue to be listed here._
