# Liquid syntax

How Fluid reads templates like Shopify Liquid, and the syntax extensions that can be enabled.

## Shopify Liquid syntax

Shopify Liquid ignores some content and reads some words in ways that can be surprising. Fluid does the same:

- Operators made of letters, like `contains`, `or` and `and`, and the other words of the syntax, like `with` or `else`, end where no letter, digit or `_` follows them: `{% if a contains"b" %}` is valid, `{% if a orb %}` is an error instead of `a or b`, and `{% render 'card' format: 'short' %}` passes a `format` argument.
- The `in` of a `for` or `tablerow` tag is followed by whitespace: `{% for i in(1..3) %}` is invalid.
- A tag name is made of letters, digits and `_`, so `{% comment-x %}` is a `comment` tag, and `{% my-tag %}` is the `my` tag.
- Tags without arguments ignore anything after their name: closing tags, `else`, `comment`, `raw`, `break`, `continue` and `ifchanged`, as in `{% endfor items %}`.
- A `for` tag only accepts `reversed` right after the collection, then `limit` and `offset` attributes, optionally separated by commas. Anything else is a syntax error, like a misspelled `{% for i in items limt: 2 %}`, `{% for i in items reversedx %}` or `{% for i in items limit: 2 reversed %}`.
- The `limit`, `offset` and `cols` attributes of a `tablerow` tag are read anywhere after the collection, and anything else is ignored, `reversed` included: `{% tablerow i in items foo, limit: 2 %}` renders two items.
- A `for` or `tablerow` loop over a string renders it once, whatever its `limit` and `offset`: `{% for i in "abc" limit: 0 %}{{ i }}{% endfor %}` renders `abc`.
- `forloop.name` is the loop variable and the collection as written, without the whitespace between its parts: `i-items`, `i-a[0]`, `i-(1..n)` or `i-"abc"`. The loops of the same name share the cursor of `offset: continue`.
- Like Ruby's unbounded integers, a number literal too large for a decimal, like `99999999999999999999999999999`, is valid. The math filters are exact with integers of any size, and compute with doubles as soon as a float is involved: `{{ 99999999999999999999999999999 | plus: 1 }}` renders `100000000000000000000000000000` and `{{ 79228162514264337593543950335 | times: 1.5 }}` renders `1.188422437713965e+29`. Such a literal is out of range as a range bound, `json` writes an integer out of the range of a 64-bit integer as a string, and `size` returns the number of bytes of an integer like Ruby's `Integer#size`: `8` for most, `13` for `99999999999999999999999999999`, `0` for a float.
- `empty` and `blank` are only special as an operand of a condition (`if`, `unless`, `elsif` and `when`), where `x == empty` tests whether `x` is empty. Everywhere else they are an empty string: after `{% assign a = blank %}`, `a` is `''`, so `{% if a == nil %}` is false.
- A hash with string keys, like a JSON object or a `Dictionary<string, T>`, is only indexed by strings, like a Ruby hash: `{{ h[1] }}` doesn't find the `"1"` key, `{{ h['1'] }}` does. A dictionary with keys of another type, like `Dictionary<int, T>`, is indexed by its keys: `{{ d[5] }}`. The `size` and `first` filters return the number of entries and the first `[key, value]` pair of a hash, even when it has a `size` or `first` key, which `h.size` and `h.first` read.

### The `self` object

Like Liquid 5.13 and later, `self` is an object whose members are the variables of the template: `{{ self.title }}` and `{{ self['title'] }}` are `{{ title }}`, and `{{ self[name] }}` reads the variable whose name is in `name`. Variables are found as usual, from the current scope (loop variables, `assign` and `capture`) to the model and the global values.

```liquid
{% assign key = 'title' %}
{{ self[key] }}
{% if self.discount %}On sale{% endif %}
```

- A variable named `self`, assigned or in the model, takes precedence over the object.
- In a `render` partial, `self` reads the variables of the partial, its arguments and the model, but not the variables of the template that renders it. A `self` passed to a partial as an argument (`{% render 'card', parent: self %}`) keeps reading the variables of the template it comes from.
- `self.self` and a bare bracket `{{ ['self'] }}` are the object too, unless a variable is named `self`.
- `self` has no other member: `self.size` is a variable named `size`, `{{ self | size }}` is `0`, and `{{ self }}` renders `Liquid::SelfDrop`, like in Shopify.

## Partials: `include` and `render`

Like Shopify Liquid, `render` renders a partial in an isolated context, and `include` (deprecated in Liquid) in the context of the template:

- A partial rendered with `render` sees the values given to the template and its named arguments, and starts with its own `cycle` and `ifchanged` state: `{% cycle 'a', 'b' %}{% render 'cycle' %}` renders `aa` when the partial is `{% cycle 'a', 'b' %}`. Each item of `render ... for` starts over too. An included partial shares the state of the template.
- The named arguments of `render` are set before the `with` or `for` value, which replaces an argument of the same name unless it is nil: `{% render 'card' with product as item, item: default %}` gets `default` when `product` is nil.
- `include ... with` and `include ... for` both render the partial once for each item of an array, and once with the whole value for anything else, including a range or a hash. They don't define a `forloop` variable: the partial sees the `forloop` of the loop around the `include`.
- Without `with` or `for`, `include` uses the variable named like the partial: `{% include 'product' %}` renders the partial once for each item of a `product` array, with `product` set to the item. Like with `with`, the variable replaces a named argument of the same name, even when it doesn't exist: `{% include 'product', product: p %}` renders the partial with a nil `product` when there is no `product` variable. A partial in a folder is named like the whole path, which is never a variable: in `{% include 'snippets/product' %}`, `product` is nil.
- A `break` or `continue` in an included partial isn't consumed by the `include`, even with `for`: it applies to the loop around the `include`, after every item of an array was rendered. Like in Liquid, the items after one that reached a `break` or `continue` only render up to their first tag or output (`{% include 'p' for items %}` renders `1-123` for items `1`, `2`, `3` and a partial `{{ p }}-{{ p }}{% break %}`), and each `break` or `continue` they reach is stacked: the loop around the `include` consumes one, and each of the others applies to the next loop around it, or ends the template. The loop that consumes one only renders its next iteration up to its first tag or output. Unlike Liquid, a block such as an `if` in such an item, or in such an iteration, still renders all its content.
- A partial rendered with `render` can't use `include`: rendering it throws a `LiquidException` with the message `include usage is not allowed in this context` (Liquid renders this message as an inline error).

> **Breaking change:** `include ... with` used to render an array once, `include ... for` used to iterate ranges and hashes, define `forloop` and stop at a `break`, `render` used to share the cycle and ifchanged state of the template, and `include` used to work in a partial rendered with `render`. `include` without `with` or `for` used to render an array named like the partial once and keep a named argument or a variable named like the partial, and a `break` or `continue` in the items of an array used to stop only the loop around the `include`, after rendering every item in full.

## Whitespace control

Liquid follows strict rules with regard to whitespace support. By default, all spaces and new lines are preserved from the template.
The Liquid syntax and some Fluid options allow you to customize this behavior.

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

### Blank blocks

Like in Shopify Liquid, the whitespace of an `if`, `unless`, `case` or `else` block is not rendered when the block contains only whitespace, comments and tags that write nothing, such as `assign` or `capture`. Tags that write output, such as `increment`, `decrement` and `cycle`, keep the whitespace around them.

```liquid
[{% if true %} {% assign a = 1 %} {% endif %}]
[{% if true %} {% increment a %} {% endif %}]
```

Outputs:

```
[]
[ 0 ]
```

### Whitespace characters

Like Shopify, Fluid only skips spaces, tabs, new lines (`\n`, `\r`), `\v` and `\f` in tags and outputs, with or without `AllowLiquidTag`. The other Unicode spaces, such as the no-break space (U+00A0), are like any other char such as `@`: `{{<U+00A0>name }}` and `{% if a<U+00A0>%}` are syntax errors, where Shopify's lax mode ignores the char in an output and reads it as part of the value in a tag.

> **Breaking change:** Fluid used to skip the no-break space and most Unicode spaces in tags and outputs. Templates edited in a rich text editor can contain no-break spaces: replace them with spaces.

In the text around tags and outputs, the hyphens and the `WithTrimming()` options only strip spaces, tabs, new lines, `\v`, `\f` and NUL (like Ruby's `strip`), and only spaces, tabs, new lines, `\v` and `\f` make a block blank. A no-break space or another Unicode space is content: it is rendered, like in Shopify.

### Trimming options

Fluid provides the `TemplateOptionsBuilder.WithTrimming()` method that can be used with predefined preferences for when whitespace should be stripped automatically, even if hyphens are not
present in tags and output values.

### Greedy mode

When greedy mode is disabled with `TemplateOptionsBuilder.WithGreedy(false)`, only the spaces before the first new line are stripped.
Greedy mode is enabled by default since this is the standard behavior of the Liquid language.

## String literals

Like Liquid, string literals have no escape sequences: a backslash is a plain character, and a string ends at the next matching quote. `{{ "C:\temp" }}` renders `C:\temp`. Use the other kind of quote to include one in a string: `{{ "it's" }}`.

To decode backslash escape sequences such as `\n`, `\t`, `\'` or `\u0041` in string literals, set `FluidParserOptions.AllowStringEscapeSequences` to `true`:

```csharp
var parser = new FluidParser(new FluidParserOptions { AllowStringEscapeSequences = true });
```

## Functions

Fluid provides optional support for functions, which is not part of the standard Liquid templating language. As such, it is not enabled by default.

### Enabling functions

When instantiating a `FluidParser`, set the `FluidParserOptions.AllowFunctions` property to `true`.

```
var parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });
```

When functions are used while the feature is not enabled, a parse error will be returned.

### Declaring local functions with the `macro` tag

`macro` allows you to define reusable chunks of content to invoke with a local function.

```
{% macro field(name, value='', type='text') %}
<div class="field">
  <input type="{{ type }}" name="{{ name }}"
         value="{{ value }}" />
</div>
{% endmacro %}
```

Now `field` is available as a local property of the template and can be invoked as a function.

```
{{ field('user') }}
{{ field('pass', type='password') }}
```

> Macros need to be defined before they are used, as they are discovered as the template is executed.

### Importing functions from external templates

Macros defined in an external template **must** be imported before they can be invoked.

Omit the import list to import every macro defined by the template:

```
{% from 'forms' %}

{{ field('user') }}
{{ field('pass', type='password') }}
```

Use an explicit import list to import only selected macros:

```
{% from 'forms' import field %}

{{ field('user') }}
{{ field('pass', type='password') }}
```

The import-all form is the equivalent of a wildcard import; no wildcard token is required. Both forms evaluate the external template without rendering its output and copy only macros, not its assigned variables. An imported macro replaces a same-named value in the current scope, so prefer selective imports when name collisions are possible. Use `include` to render external template content and `from` to load its macros.

## Order of execution

With tags containing more than one `and` or `or` operator, operators are evaluated in order from right to left. You cannot change the order of operations using parentheses. This is the same for filters, which are executed from left to right.
However, Fluid provides an option to support grouping expressions with parentheses.

### Enabling parentheses

When instantiating a `FluidParser`, set the `FluidParserOptions.AllowParentheses` property to `true`.

```
var parser = new FluidParser(new FluidParserOptions { AllowParentheses = true });
```

When parentheses are used while the feature is not enabled, a parse error will be returned (except for ranges like `(1..4)`).

At that point a template like the following will work:

```liquid
{{ 1 | plus : (2 | times: 3) }}
```
