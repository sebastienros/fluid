# Compiled template generator

The compiled template generator parses `.liquid` files at build time and compiles each one into a C# class that implements `IFluidTemplate`. It is part of the `Fluid.SourceGenerator` package and is unrelated to the [model accessor generator](model-accessor-generator.md).

The basic usage is described in the [README](../README.md#compiled-templates). This document covers the details.

## When to use it

Use the generator for templates that are part of the application's source code, such as emails, reports, or files produced by a tool:

- A syntax error fails the build, with the file name in the error.
- Nothing is parsed at startup or at render time.
- Templates are accessed through generated properties instead of file paths.

It can't be used when:

- The template is only known at runtime: stored in a database, edited by users, or deployed separately from the application. Use `FluidParser` for those.
- The template uses custom tags, blocks or operators. The generator parses templates with its own `FluidParser`, which only knows the built-in syntax.
- The template uses the `tablerow`, `ifchanged` or `doc` tags, which can't be compiled yet.
- An `include` or `from` tag computes its path, for example `{% include name %}`. The path must be a string literal.

Everything else is resolved when the template is rendered, from the `TemplateContext` and its `TemplateOptions`: values, filters (including custom ones), value converters, member access, `Trimming` and `Greedy`, encoding, culture, time zone and execution limits.

## Project setup

Reference the package and declare the templates as additional files:

```xml
<ItemGroup>
  <PackageReference Include="Fluid.SourceGenerator" Version="..." />
  <AdditionalFiles Include="Templates/**/*.liquid" />
</ItemGroup>
```

Only `AdditionalFiles` are visible to the generator. A template that isn't declared there is neither compiled nor available to `render`.

## Selecting templates

Annotate a `partial` class with `[FluidTemplates]` from the `Fluid.SourceGenerator` namespace. The class can be static or not, and nested in another class.

```csharp
using Fluid.SourceGenerator;

namespace MyApp;

[FluidTemplates("Templates/**/*.liquid", Exclude = new[] { "Templates/partials/**" })]
public static partial class Templates
{
}
```

The constructor takes one or more include patterns, and `Exclude` takes the patterns to skip. Patterns are matched against paths relative to the project directory, with `/` as the separator:

- `*` matches any characters within a path segment.
- `?` matches a single character within a path segment.
- `**` matches any number of directories, for example `**/*.liquid`.

Several classes can each select a different set of templates.

### Parser options

The `FluidParserOptions` features that change the syntax are disabled by default, as with `FluidParser`. Enable them on the attribute:

```csharp
[FluidTemplates("Templates/**/*.liquid", AllowFunctions = true)]
public static partial class Templates
{
}
```

| Property | Enables |
| --- | --- |
| `AllowFunctions` | The `macro` and `from` tags, and function calls such as `{{ field('name') }}`. |
| `AllowLiquidTag` | The `liquid` tag. |
| `AllowParentheses` | Parentheses in expressions. |
| `AllowTrailingQuestionMark` | Identifiers ending with `?`. |

The options apply to every template of the class, and to the templates they reference.

### Templates folder

To match paths relative to a folder instead of the project directory, set `FluidTemplatesFolder`:

```xml
<PropertyGroup>
  <FluidTemplatesFolder>Templates</FluidTemplatesFolder>
</PropertyGroup>
```

The generator then only considers additional files under that folder, and both the patterns and the `render` paths are relative to it: `[FluidTemplates("**/*.liquid")]` and `{% render "partials/footer" %}`.

## Generated properties

Each selected file becomes a static property of type `IFluidTemplate`. The property name is the file name without its extension, in PascalCase:

| File | Property |
| --- | --- |
| `Templates/hello.liquid` | `Templates.Hello` |
| `Templates/welcome-email.liquid` | `Templates.WelcomeEmail` |
| `Templates/order_summary.txt.liquid` | `Templates.OrderSummaryTxt` |
| `Templates/2fa.liquid` | `Templates.T2fa` |

Folders are not part of the name. When two files produce the same name, a number is appended to the second one, for example `Hello2`. Exclude the files you don't need, or use separate classes, to keep names predictable.

The template instance is created on first access and reused. It is thread-safe like a parsed template, and is rendered the same way:

```csharp
var context = new TemplateContext(options);
context.SetValue("name", "Ada");

var output = await Templates.Hello.RenderAsync(context);
```

The compiled classes are generated in the `<namespace>.FluidTemplates` namespace, `MyApp.FluidTemplates` in the example above, or `FluidTemplates` for a class in the global namespace. Set `Namespace` on the attribute to change it. These classes are an implementation detail: use the properties.

## Referenced templates

The templates referenced by `render`, `include` and `from` are compiled with the template that uses them, and behave like they do at runtime: `render` evaluates the partial in its own scope, `include` shares the caller's scope (and lets `break` and `continue` reach an enclosing loop), and `from` imports macros.

- The path must be a string literal, relative to the project directory or to `FluidTemplatesFolder`.
- The `.liquid` extension is optional: `{% render "partials/footer" %}` finds `partials/footer.liquid`.
- The target must be an additional file, but doesn't need to match the `[FluidTemplates]` patterns. Partials can be excluded so they don't get their own property.
- The `TemplateOptions.FileProvider` isn't used: changing a file requires a new build.

## The forloop variable

A loop only creates and updates its `forloop` variable when something can read it, which makes the other loops cheaper. The generator looks at everything that runs inside the loop:

- the loop body, including `forloop.parentloop` and `parentloop` in inner loops;
- the templates it includes, which share its scope (a template rendered with `render` is isolated and only sees the loop of a `render ... for`);
- the macros it calls, which run in the scope of the call.

When the loop runs something the generator can't look into, it keeps the variable: a filter that isn't built in, a function that isn't a macro of the template, or a variable whose name is computed, like `{{ [name] }}`.

The only code that can't be followed is .NET code reading the variable from the `TemplateContext` outside of a filter or function call, for instance a custom member accessor, a value converter, a context callback such as `TemplateContext.Assigned`, or a custom `FluidValue`. In a compiled template, such code sees no `forloop` unless the template itself uses it. A built-in filter replaced with `AddFilter` is also treated as built in.

## Errors

| Error | Cause |
| --- | --- |
| `FLUIDSG101` | The class annotated with `[FluidTemplates]` isn't `partial`. |
| `FLUIDSG105` | A template has a syntax error. The message gives the file and the position. |
| `FLUIDSG106` | A template uses a tag that can't be compiled, an `include` or `from` with a dynamic path, or references a file that isn't an additional file. The message gives the file and the cause. |

When a template has an error, no property is generated for it, but the other templates are still compiled.
