# View engines

Using Liquid views with ASP.NET Core MVC, or with the MVC-agnostic `Fluid.ViewEngine` package.

## ASP.NET MVC View Engine

The package `Fluid.MvcViewEngine` provides a convenient way to use Liquid as a replacement for, or in combination with, Razor in ASP.NET MVC.

### Configuration

#### Registering the view engine

1. Reference the `Fluid.MvcViewEngine` NuGet package
2. Add a `using` statement on `Fluid.MvcViewEngine`
3. Call `AddFluid()` in your `Startup.cs`.

#### Sample
```csharp
using Fluid.MvcViewEngine;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc().AddFluid();
    }
}
```

#### Encoding views

Views are HTML-encoded with `FluidViewEngineOptions.TextEncoder`. When it isn't set explicitly:

- `Fluid.MvcViewEngine` and `MinimalApis.LiquidViews` use the application's `HtmlEncoder` service when one is registered (MVC always registers it). It reflects `WebEncoderOptions`, so Liquid views encode like Razor views in the same application.
- Otherwise views use `FluidViewEngineOptions.DefaultTextEncoder`, `HtmlEncoder.Create(UnicodeRanges.All)`, which leaves non-ASCII text such as `é` or `漢` as is and still encodes `<`, `>`, `&`, quotes and emoji.

The `HtmlEncoder` service registered by MVC only allows Basic Latin unless `WebEncoderOptions` is configured. To keep non-ASCII text as is in both Razor and Liquid views:

```csharp
services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
```

Or only for Liquid views:

```csharp
services.Configure<FluidMvcViewOptions>(o => o.TextEncoder = HtmlEncoder.Create(UnicodeRanges.All));
```

> [!NOTE]
> In Fluid 3.0 the view engines stopped defaulting to `HtmlEncoder.Default`. Pages with non-ASCII text, outside of MVC, now contain the characters instead of numeric entities, so their bytes change: update snapshot tests, and expect cached pages and ETags to be invalidated once.

#### Registering view models

Because the Liquid language only allows known members to be accessed, the View Model classes need to be registered in Fluid, usually from a static constructor so that the code is run only once for the application.

#### View Model registration

View models are automatically registered and available as the root object in liquid templates.
Custom model registrations can be added when calling `AddFluid()`, whose action configures the `FluidMvcViewOptions` used by the view engine (the same as `services.Configure<FluidMvcViewOptions>()`).

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc().AddFluid(o => o.TemplateOptionsBuilder.ConfigureMemberAccess(
            strategy => strategy.Register<Person, object>((person, name) => person.Firstname)));
    }
}
```

More ways to register types and members can be found in the [Models and values](models-and-values.md) document.

#### Registering custom tags

When using the MVC View engine, custom tags can still be added to the parser. Refer to [Registering a custom tag](extensibility.md#registering-a-custom-tag) on how to create custom tags.

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
        services.AddMvc().AddFluid(options =>
        {
            options.Parser = new CustomFluidViewParser();
        });
    }
}
```

### Layouts

#### Index.liquid

```Liquid
{% layout '_layout.liquid' %}

This is the home page
```

The `{% layout [template] %}` tag accepts one argument which can be any expression that returns the relative location of a Liquid template that will be used as the master template.

The layout tag is optional in a view. It can also be defined multiple times or conditionally.

From a layout template, the `{% renderbody %}` tag is used to depict the location of the view's content inside the layout itself.

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

#### Nested layouts

A layout can itself use a `{% layout %}` tag, for instance a `_columns.liquid` layout that renders its own content inside `_layout.liquid`. Layouts are applied from the innermost to the outermost, and the path of a nested layout is resolved from the location of the layout that declares it. Sections defined by a view or by any of its layouts can be rendered by any outer layout. A layout that ends up applying itself again throws an `InvalidOperationException`.

```Liquid
{% layout '_layout.liquid' %}

<div class="columns">
  {% renderbody %}
</div>
```

A layout without a `{% layout %}` tag is rendered directly to the output. A layout that has one is buffered so it can become the body of the next layout. Only a tag in the layout file itself is detected, so a layout that only gets one from a partial or an include it renders ignores it.

### Sections

Sections are defined in a layout for views to render content in specific locations. For instance, a view can render some content in a **menu** or a **footer** section.

#### Rendering content in a section

```Liquid
{% layout '_layout.liquid' %}

This is the home page

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

Defining the layout template in each view might be cumbersome and make it difficult to change it globally. To prevent that, it can be defined in a `_ViewStart.liquid` file.

When a view is rendered, all `_ViewStart.liquid` files from its current and parent directories are executed beforehand. This means multiple files can be defined to set settings for a group of views.

#### _ViewStart.liquid

```Liquid
{% layout '_layout.liquid' %}
{% assign background = 'ffffff' }
```

You can also define other variables or render some content.

### Custom views locations

It is possible to add custom file locations containing views by adding them to `FluidMvcViewOptions.ViewsLocationFormats`.

The default ones are:
- `Views/{1}/{0}.liquid`
- `Views/Shared/{0}.liquid`

Where `{0}` is the view name, and `{1}` is the controller name.

For partials, the list is defined in `FluidMvcViewOptions.PartialsLocationFormats`:
- `Views/{0}.liquid`
- `Views/Partials/{0}.liquid`
- `Views/Partials/{1}/{0}.liquid`
- `Views/Shared/Partials/{0}.liquid`

Layouts will be searched in the same locations as Views.

### Execution

The content of a view is parsed once and kept in memory until the file or one of its dependencies changes. Once parsed, the tags are executed every time the view is called. In comparison, Razor views are first compiled and then instantiated every time they are rendered. This means that on startup or when the view is changed, views with Fluid will run faster than those in Razor, unless you are using precompiled Razor views. In all cases, Razor views will be faster on subsequent calls as they are compiled directly to C#.

This difference makes Fluid very suitable for rapid development cycles where the views can be deployed and updated frequently. And because the Liquid language is secure, developers can give access to them with more confidence.  

## Fluid.ViewEngine without MVC

The Fluid ASP.NET MVC View Engine is based on an MVC-agnostic view engine provided in the `Fluid.ViewEngine` package. The same options and features are available, but without 
requiring ASP.NET MVC. This is useful to provide the same experience when building templates using layouts and sections.

### Usage

Use the class `FluidViewRenderer : IFluidViewRenderer` and `FluidViewEngineOptions`.

### Missing views, layouts and partials

A view, a layout or a partial that can't be found throws a `FileNotFoundException` whose `FileName` is the requested path. To render something else instead, log the path, or throw a different exception, set `FluidViewEngineOptions.TemplateNotFound`. It is called in place of the missing template, with the requested path, whether it is a view, a layout or a partial, and the output the template would have been rendered to:

```csharp
options.TemplateNotFound = (path, kind, output, context) =>
{
    output.Write($"<!-- Missing {kind}: {path} -->");

    // A layout renders the body of the view, which is otherwise lost.
    if (kind == ViewTemplateKind.Layout)
    {
        output.Write(context.AmbientValues[Constants.BodyIndex] as string);
    }

    return default;
};
```

The text written by the delegate is not encoded. A missing template is looked up again on each rendering, so a file added later is used as soon as it exists.

> In Fluid 3.0 a missing view, layout or partial throws instead of rendering nothing.
