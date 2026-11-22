---
title: Extending the grammar
description: "Custom tags, blocks, operators and functions, and how to analyze or rewrite templates."
---

## Customizing tags and blocks

Fluid's grammar can be modified to accept any new tags and blocks with 
any custom parameters. The parser is based on [Parlot](https://github.com/sebastienros/parlot) 
which makes it completely extensible.

Unlike blocks, tags don't have a closing element (e.g., `cycle`, `increment`).
A closing element will match the name of the opening tag with an `end` suffix, like `endfor`.
Like in Shopify Liquid, anything after the name of a closing element is ignored, as in `{% endfor items %}`.
Blocks are useful when manipulating a section of a template as a set of statements.

Fluid provides helper methods to register common tags and blocks. All tags and blocks always start with their name, made of letters, digits and `_` like in Shopify Liquid: a `-` ends the name.

Each custom tag needs to provide a delegate that is evaluated when the tag is matched. Each delegate will be able to use these properties:

- `writer`, a `TextWriter` instance that is used to render some text.
- `encode`, a `TextEncoder` instance, like `HtmlEncoder` or `NullEncoder`. It's defined by the caller of the template.
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

parser.RegisterExpressionBlock("repeat", async (value, statements, writer, encoder, context) =>
{
    var fluidValue = await value.EvaluateAsync(context);

    for (var i = 0; i < fluidValue.ToNumberValue(context); i++)
    {
        await statements.RenderStatementsAsync(writer, encoder, context);
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

For instance, `RegisterParserTag("mytag", Primary.AndSkip(Comma).And(Primary), ...)` will expect two `Primary` elements separated by a comma. The delegate will then 
be invoked with a `ValueTuple<Expression, Expression>` representing the two `Primary` expressions.

`Primary`, `Comma` and the other building blocks are `protected` members of `FluidParser`, so register such tags in the constructor of a class
deriving from it:

```csharp
using Fluid;
using Fluid.Ast;
using Parlot.Fluent; // Parser combinators like AndSkip and And

public class MyParser : FluidParser
{
    public MyParser()
    {
        RegisterParserTag("mytag", Primary.AndSkip(Comma).And(Primary), static async (values, writer, encoder, context) =>
        {
            var first = await values.Item1.EvaluateAsync(context);
            var second = await values.Item2.EvaluateAsync(context);

            writer.Write($"{first.ToStringValue(context)} and {second.ToStringValue(context)}");

            return Completion.Normal;
        });
    }
}
```

```Liquid
{% mytag 'one', 'two' %}
```

#### Result
```html
one and two
```

With Parlot 2.0, `FluidParser.Compile()` is no longer available. Create and cache a `FluidParser` directly; no runtime grammar compilation step is required.

When composing an EOF check with an error, use
`parser.AndSkip(Parsers.Eof().ElseError("Unexpected trailing input"))` to report the
first trailing character. `parser.Eof().ElseError(...)` instead reports the parser's
starting position because `parser.Eof()` backtracks on failure. Use a non-error EOF
check inside alternatives when failure should allow another branch to be tried.

### Registering a custom operator

Operators are used to compare values, like `>` or `contains`. Custom operators can be defined if special comparisons need to be provided.

Like `contains`, a custom operator made of letters ends where no letter, digit or `_` follows it: `{% if a xorb %}` is not `a xor b`.

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

            return BooleanValue.Create(leftValue.ToBooleanValue(context) ^ rightValue.ToBooleanValue(context));
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

## Accessing the concrete syntax tree

The syntax tree is accessible by casting the template to its concrete `FluidTemplate` type and using the `Statements` property.

#### Source

```csharp
var template = (FluidTemplate)iTemplate;
var statements = template.Statements;
```

## Custom functions

Functions are `FluidValue` instances implementing the `InvokeAsync` method. This allows any template to be provided custom function values as part of the model, the `TemplateContext`, or globally with options.

A `FunctionValue` type is also available to provide out-of-the-box functions. It takes a delegate that returns a `ValueTask<FluidValue>` as the result.

```c#
var lowercase = FunctionValue.Create((args, context) =>
{
  var firstArg = args.At(0).ToStringValue(context);
  var lower = firstArg.ToLowerInvariant();
  return StringValue.Create(lower);
});

var context = new TemplateContext();
context.SetValue("tolower", lowercase);

var parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });
parser.TryParse("{{ tolower('HELLO') }}", out var template, out var error);
template.Render(context);
```

## Visiting and altering a template

Fluid provides a __Visitor__ pattern that allows you to analyze what a template is made of, and also to alter it. This can be used, for instance, to check if a specific identifier is used, replace some filters with others, or remove any expression that might not be authorized.

### Tracking statement source locations (experimental)

```csharp
#pragma warning disable FLUID001

var parser = new FluidParser(new FluidParserOptions
{
    TrackStatementLocations = true
});
var template = parser.Parse(source);
var statements = ((Fluid.Parser.FluidTemplate)template).Statements;

foreach (var statement in statements)
{
    if (statement.SourceOffset >= 0)
    {
        var originalText = source.Substring(statement.SourceOffset, statement.SourceLength);
    }
}

#pragma warning restore FLUID001
```

### Visiting a template

The `Fluid.Ast.AstVisitor` class can be used to create a custom visitor.

Here is an example of a visitor class that records if an identifier is accessed anywhere in a template:

```c#
  public class IdentifierIsAccessedVisitor : AstVisitor
  {
      private readonly string _identifier;

      public IdentifierIsAccessedVisitor(string identifier)
      {
          _identifier = identifier;
      }

      public bool IsAccessed { get; private set; }

      public override IFluidTemplate VisitTemplate(IFluidTemplate template)
      {
          // Initialize the result each time a template is visited with the same visitor instance

          IsAccessed = false;
          return base.VisitTemplate(template);
      }

      protected override Expression VisitMemberExpression(MemberExpression memberExpression)
      {
          var firstSegment = memberExpression.Segments.FirstOrDefault() as IdentifierSegment;

          if (firstSegment != null)
          {
              IsAccessed |= firstSegment.Identifier == _identifier;
          }

          return base.VisitMemberExpression(memberExpression);
      }
  }
```

And its usage:

```c#
var template = new FluidParser().Parse("{{ a.b | plus: 1}}");

var visitor = new IdentifierIsAccessedVisitor("a");
visitor.VisitTemplate(template);

Console.WriteLine(visitor.IsAccessed); // writes True
```

### Rewriting a template

The `Fluid.Ast.AstRewriter` class can be used to create a custom rewriter.

Here is an example of a visitor class that replaces any `plus` filter with a `minus` one:

```c#
  public class ReplacePlusFiltersVisitor : AstRewriter
  {
      protected override Expression VisitFilterExpression(FilterExpression filterExpression)
      {
          if (filterExpression.Name == "plus")
          {
              return new FilterExpression(filterExpression.Input, "minus", filterExpression.Parameters);
          }

          return filterExpression;
      }
  }
```

And its usage:

```c#

var template = new FluidParser().Parse("{{ 1 | plus: 2 }}");

var visitor = new ReplacePlusFiltersVisitor();
var changed = visitor.VisitTemplate(template);

var result = changed.Render();

Console.WriteLine(result); // writes -1
```

### Visiting templates parsed during rendering

You can apply visitors and rewriters to templates that are parsed before they are cached by using the `TemplateParsed` callback of the `TemplateOptionsBuilder`. This works for all template parsing scenarios including the ViewEngine, `include` and `render` statements.

```c#
var options = new TemplateOptionsBuilder()
    .WithFileProvider(new FileProviderTemplateFileProvider(fileProvider))
    .WithTemplateParsed((path, template) =>
    {
        var visitor = new MyCustomVisitor();
        return visitor.VisitTemplate(template);
    })
    .Build();
```

The `TemplateParsed` callback is invoked after a template is parsed but before it is cached. This means:
- The modified template is cached, improving performance
- The callback applies to all templates including partials, includes, and ViewStarts
- Each template is processed only once (when first parsed, not when retrieved from cache)

### Custom parser statements

The [custom statements and expressions](#custom-parsers) can also be visited by using one of these methods:

- `VisitParserTagStatement<T>(ParserTagStatement<T>)`
- `VisitParserBlockStatement<T>(ParserBlockStatement<T>)`
- `VisitEmptyTagStatement(EmptyTagStatement)`
- `VisitEmptyBlockStatement(EmptyBlockStatement)`

They all expose a `TagName` property and, optionally, `Statements` and `Value` properties when applicable.
