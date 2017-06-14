<p align="center"><img width=25% src="https://github.com/sebastienros/fluid/raw/dev/Assets/logo-vertical.png"></p>

## Basic Overview

Fluild is an open-source .NET template engine that is as close as possible to the [Liquid template language](https://shopify.github.io/liquid/). If a **secure** template language that is also **very accessible** for non-programmer audiences.

Fluid is different from other .NET implementations by not relying on code compilation but instead interpreting the templates.

<br>

## Features

- Parses and renders Liquid templates.
- Supports **async** filters, templates can execute database queries more efficiently under load.
- Parses templates in an intermediate **AST** that lets you analyze and alter the templates before they are rendered. You can also cache them to get even better performance.
- Register any .NET types and properties, or define **custom handlers** to intercept when a named variable is accessed.
- Secure by white-listing all the available properties in the template.

<br>

## Contents
- [Features](#features)
- [Differences with Liquid](#differences-with-liquid)
- [Using Fluid in your project](#using-fluid-in-your-project)
- [White-listing object members](#white-listing-object-members)
- [Converting CLR types](#converting-clr-types)
- [Encoding](#encoding)
- [Performance](#performance)

<br>

## Differences with Liquid

### Optional default parameters for custom filters
In Fluid a **Filter** doesn't need to have a default parameter, you can name all of them.

```Liquid
{% assign customers = 'allcustomers' | query: limit:10 %}
```

### Maintained indentation

Fluid will automatically maintain the indentation from the original template and won't inject extra lines where tags are used.

In the standard Liquid implementation this behavior requires the use of the special `-%}` end tag, not with Fluid.

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

      Peal it.
    </li>
</ul>
```

<br>

## Using Fluid in your project

You can directly reference the [Nuget package](https://www.nuget.org/packages/Fluid.Core).

### Hello World

#### Source

```csharp
var model = new { Firstname = "Bill", Lastname = "Gates" };
var source = "Hello {{ p.Firstname }} {{ p.Lastname }}";

if (FluidTemplate.TryParse(source, out var template))
{   
    var context = new TemplateContext();
    context.Register(model.GetType()); // Allows any public property of the model to be used

    Console.WriteLine(template.Render())
}
```

#### Result
`Hello Bill Gates`

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

This will only allow the specied fields or properties to be read from a template.

```csharp
TemplateContext.GlobalMemberAccessStrategy.Register<Person>("Firstname", "Lastname");
``` 

### Intercepting a type access

This will provide a method to intercept when a member is accessed and either return a custom value or prevent it.

This example demonstrates how to intercept calls to a `JObject` and return the corresponding property.

```csharp
TemplateContext.GlobalMemberAccessStrategy.Register<JObject>((obj, name) => return obj[name]);
``` 

<br>

## Converting CLR types

Whenever an object is manipulated in a template it is converted to a specific `FluidValue` instance that provides a dynamic type system somehow similar to the one in JavaScript.

In Liquid they can be Number, String, Boolean, Array, or Dictionary. Fluid will automatically convert the CLR types to the corresponding Liquid ones, and also provides specilized ones.

To be able to customize this conversion you can add type mappings

### Adding a type mapping

The following example shows how to support `JObject` and `JValue` types to map their values to `FluidValue` instances.

First is solves the issue that a `JObject` implements `IEnumerable` and would be converted to an `ArrayValue` instead of an `ObjectValue`. Then we use `FluidValue.Create` to automatically convert the CLR value of the `JValue` object.

```csharp
FluidValue.TypeMappings.Add(typeof(JObject), o => new ObjectValue(o));
FluidValue.TypeMappings.Add(typeof(JValue), o => FluidValue.Create(((JValue)o).Value));
```

> Note: Type mapping are defined globally for the application.

<br>

## Encoding

By default Fluid will encode any output variable into HTML. The default encoder can be specified when calling `Render()` on the template.

Alternatively you can use a special `raw` filter to prevent a value from being encoded, for instance if you know that the content is HTML and is safe.

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

## Performance

### Caching

Some performance boost can be gained in your application if you decide to cache the parsed templates before they are rendered. Even though parsing is memory-safe as it won't induce any compilation (meaning all the memory can be collected if you decide to parse a lot of templates), you can skip the parsing step by storing and reusing the `FluidTemplate` instance.

These object are thread-safe as long as each call to `Render()` uses a dedicated `TemplateContext` instance.

### Benchmark

A performance benchmark application is provided in the source code. Run it locally to analyze the time it takes to execute specific templates.