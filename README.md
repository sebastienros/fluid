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

This example demonstrates how to intercept calls to a `JObject` and return the corresponding property.

```csharp
var options = new TemplateOptions();
options.MemberAccessStrategy.Register<JObject, object>((obj, name) => obj[name]);
``` 

Another common pattern is to pass a dictionary as the model to allow members to represent the keys of the dictionary:

```csharp
var options = new TemplateOptions();
options.MemberAccessStrategy.Register<IDictionary, object>((obj, name) => obj[name]);

var model = new Dictionary<string, object>();
model.Add("Firstname", "Bill");
model.Add("Lastname", "Gates");

var template = _parser.Parse("{{Firstname}} {{Lastname}}");

template.Render(new TemplateContext(model, options));
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

In Liquid they can be Number, String, Boolean, Array, or Dictionary. Fluid will automatically convert the CLR types to the corresponding Liquid ones, and also provides specialized ones.

To be able to customize this conversion you can either add **value converters**.

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

## Using Json.NET object in models

The classes that are used in Json.NET don't have direct named properties like classes, which makes them unusable out of the box
in a Liquid template.

To remedy that we can configure Fluid to map names to `JObject` properties, and convert `JValue` objects to the ones used by Fluid.

```csharp
class JObjectFluidIndexable : IFluidIndexable
{
	private readonly JObject _obj;
	private readonly TemplateOptions _options;
	public JObjectFluidIndexable(JObject jObject, TemplateOptions options)
	{
		_obj = jObject;
		_options = options;
	}
	public int Count => _obj.Count;

	public IEnumerable<string> Keys => _obj.Properties().Select(i => i.Name);

	public bool TryGetValue(string name, out FluidValue value)
	{
		if (_obj.TryGetValue(name, out var token))
		{
			value = FluidValue.Create(token, _options);
			return true;
		}
		else
		{
			value = NilValue.Instance;
		}
		return false;
	}
}

var options = new TemplateOptions();

// When a property of a JObject value is accessed, try to look into its properties
options.MemberAccessStrategy.Register<JObject, object>((source, name) => source[name]);

// Convert JToken to FluidValue
options.ValueConverters.Add(x => x is JObject o ? new DictionaryValue(new JObjectFluidIndexable(o, options)) : null);
options.ValueConverters.Add(x => x is JValue v ? v.Value : null);

var model = JObject.Parse("{\"Name\": \"Bill\"}");

var parser = new FluidParser();

parser.TryParse("His name is {{ Name }}", out var template);
var context = new TemplateContext(model, options);

Console.WriteLine(template.Render(context));
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

When using the MVC View engine, custom tags can be added to the parser. Refer to [this section](https://github.com/sebastienros/fluid#registering-a-custom-tag) on how to create custom tags.

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
        services.Configure<FluidViewEngineOptions>(options =>
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

It is possible to add custom file locations containing views by adding them to `FluidViewEngineOptions.ViewLocationFormats`.

The default ones are:
- `Views/{1}/{0}`
- `Views/Shared/{0}`

Where `{0}` is the view name, and `{1}` is the controller name.

### Execution

The content of a view is parsed once and kept in memory until the file or one of its dependencies changes. Once parsed, the tag are executed every time the view is called. To compare this with Razor, where views are first compiled then instantiated every time they are rendered. This means that on startup or when the view is changed, views with Fluid will run faster than those in Razor, unless you are using precompiled Razor views. In all cases Razor views will be faster on subsequent calls as they are compiled directly to C#.

This difference makes Fluid very adapted for rapid development cycles where the views can be deployed and updated frequently. And because the Liquid language is secure, developers give access to them with more confidence.  

<br>

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

A benchmark application is provided in the source code to compare Fluid, [Scriban](https://github.com/scriban/scriban), [DotLiquid](https://github.com/dotliquid/dotliquid) and [Liquid.NET](https://github.com/mikebridge/Liquid.NET).
Run it locally to analyze the time it takes to execute specific templates.

#### Results

Fluid is faster and allocates less memory than all other well-known .NET Liquid parsers.
For parsing, Fluid is 30% faster than Scriban, allocating 3 times less memory.
For rendering, Fluid is slightly faster than Handlebars, 3 times faster than Scriban, but is allocating a few times less memory.
Compared to DotLiquid, Fluid renders 10 times faster, and allocates 40 times less memory.

```
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.201
  [Host]   : .NET Core 5.0.4 (CoreCLR 5.0.421.11614, CoreFX 5.0.421.11614), X64 RyuJIT
  ShortRun : .NET Core 5.0.4 (CoreCLR 5.0.421.11614, CoreFX 5.0.421.11614), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|             Method |          Mean |         Error |      StdDev |  Ratio | RatioSD |     Gen 0 |    Gen 1 |   Gen 2 |  Allocated |
|------------------- |--------------:|--------------:|------------:|-------:|--------:|----------:|---------:|--------:|-----------:|
|        Fluid_Parse |      7.423 us |      5.678 us |   0.3112 us |   1.00 |    0.00 |    0.7019 |        - |       - |    2.88 KB |
|      Scriban_Parse |     12.226 us |      2.912 us |   0.1596 us |   1.65 |    0.07 |    1.8005 |        - |       - |    7.41 KB |
|    DotLiquid_Parse |     22.582 us |     13.804 us |   0.7567 us |   3.05 |    0.22 |    3.9673 |        - |       - |   16.21 KB |
|    LiquidNet_Parse |     88.729 us |     23.242 us |   1.2740 us |  11.97 |    0.54 |   15.1367 |   0.1221 |       - |   62.08 KB |
|   Handlebars_Parse |  4,118.144 us |  2,852.877 us | 156.3758 us | 556.06 |   45.12 |   31.2500 |        - |       - |  157.91 KB |
|                    |               |               |             |        |         |           |          |         |            |
|     Fluid_ParseBig |     41.033 us |     18.167 us |   0.9958 us |   1.00 |    0.00 |    3.1738 |        - |       - |   13.02 KB |
|   Scriban_ParseBig |     60.069 us |     15.197 us |   0.8330 us |   1.46 |    0.05 |    7.8125 |   1.0986 |       - |   32.05 KB |
| DotLiquid_ParseBig |     86.152 us |     35.531 us |   1.9476 us |   2.10 |    0.10 |   23.0713 |        - |       - |   94.32 KB |
| LiquidNet_ParseBig | 29,854.219 us | 10,570.585 us | 579.4094 us | 727.79 |   18.80 | 6843.7500 | 375.0000 |       - | 28557.5 KB |
|                    |               |               |             |        |         |           |          |         |            |
|       Fluid_Render |    494.335 us |     76.086 us |   4.1705 us |   1.00 |    0.00 |   22.4609 |   4.8828 |       - |   95.52 KB |
|  Handlebars_Render |    525.813 us |     44.308 us |   2.4287 us |   1.06 |    0.01 |   43.9453 |  14.6484 |       - |  183.86 KB |
|     Scriban_Render |  1,465.749 us |    336.759 us |  18.4589 us |   2.97 |    0.04 |   99.6094 |  66.4063 | 66.4063 |  487.62 KB |
|   LiquidNet_Render |  4,137.292 us |    121.268 us |   6.6471 us |   8.37 |    0.07 |  992.1875 | 390.6250 |       - | 5324.52 KB |
|   DotLiquid_Render |  5,418.730 us |  2,265.219 us | 124.1643 us |  10.96 |    0.23 |  875.0000 | 187.5000 | 23.4375 | 3878.86 KB |
```

Tested on 5/8/2021 with 
- Scriban 3.7.0
- DotLiquid 2.1436
- Liquid.NET 0.10.0
- Handlebars.Net 2.0.7

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
