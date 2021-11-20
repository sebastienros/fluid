## MinimalApis.LiquidViews

This library provides some extensions to ASP.NET Minimal APIs that allow to return templated view results using the Liquid language.
Liquid is fast and safe. Views are interpreted so changes are reflected very quickly without a compilation phase.

## Sample usage

These files demonstrates how to return a view result, which will be able to use 
- a `_layout` file to act as a template for multiple pages
- a `_viewstart` file to be executed for each view
- a `component` file to act like partial views

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
From /Views/_ViewStart.liquid
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
    <!-- Required meta tags -->
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">

    <!-- Bootstrap CSS -->
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.0.0-beta3/dist/css/bootstrap.min.css" rel="stylesheet" integrity="sha384-eOJMYsd53ii+scO/bJGFsiCZc+5NDVN2yr8+0RDqr0Ql0h+rP48ckxlpbzKgwra6" crossorigin="anonymous">

    <title>Hello, world!</title>
  </head>
  <body>

  <div class="px-4 py-5 my-5 text-center">
    <h1 class="display-5 fw-bold">Title from the layout</h1>
  <div class="col-lg-6 mx-auto">
    <p class="lead mb-4">
		{% renderbody %}

		{% partial 'Component', x: 1, y:2 %}

	</p>
  </div>
</div>

	<pre>
	{% rendersection footer %}
	</pre>

    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.0.0-beta3/dist/js/bootstrap.bundle.min.js" integrity="sha384-JEW9xMcG8R+pH31jmWH6WWP0WintQrMb4s7ZOdauHnUtxwoG2vI5DkLtS3qm9Ekf" crossorigin="anonymous"></script>
  </body>
</html>
```