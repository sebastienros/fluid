## MinimalApis.LiquidViews

This library provides some extensions to ASP.NET Minimal APIs that allow to return templated view results using the Liquid language.
Liquid is fast and safe. Views are interpreted so changes are reflected very quickly without a compilation phase.

This View Engine is based on [Fluid](https://github.com/sebastienros/fluid), a Liquid template engine for .NET.

## Sample usage

By default, all views and partials go in the `Views` folder. This can be configured with the `FluidViewEngineOptions` class.

These files demonstrate how to use the different elements of the view engine.
- a `Views\_layout.liquid` file to act as a template for multiple pages.
- a `Views\_viewstart.liquid` file to be executed for each view that is in the same folder.
- a `Views\component.liquid` file to act like partial views.

The full sample can be found [here](https://github.com/sebastienros/fluid/tree/main/Fluid.MinimalApisSample)

#### Program.cs

```c#
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddFluid();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapGet("/", () => Results.Extensions.View("Index", new Todo(1, "Go back to work!", false)));

await app.RunAsync();

record Todo(int Id, string Name, bool IsComplete);
```

#### index.liquid

```liquid
<hr />
Hello World from the body

Name: {{ Name }} <br />
IsComplete: {{ IsComplete}}
<hr />

{% section footer %}
Hello from the footer section
{% endsection %}
```

#### _viewstart.liquid

```liquid
{% layout '_Layout' %}
```

#### component.liquid

```liquid
<div>Using a component {{ x }} + {{ y }} = {{ x | plus: y }}</div>
```

#### _layout.liquid

```liquid
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <title>Hello, world!</title>
  </head>
  <body>

    <h1>Title from the layout</h1>
    {% renderbody %}

    {% partial 'Component', x: 1, y:2 %}

    <footer>
      {% rendersection footer %}
    </footer>

  </body>
</html>
```

## File locations

By default Views and Partials are located in the `Views` folder.
The Partial views can also be placed in the `Views/Partials` folder.
