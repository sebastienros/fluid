---
title: Models and values
description: "How .NET objects are exposed to templates and converted to Liquid values."
---

## Converting CLR types

Whenever an object is manipulated in a template, it is converted to a specific `FluidValue` instance that provides a dynamic type system somewhat similar to the one in JavaScript.

In Liquid, they can be Number, String, Boolean, Array, Dictionary, or Object. Fluid will automatically convert the CLR types to the corresponding Liquid ones, and also provides specialized ones.

To customize this conversion, you can add **value converters**.

### Creating Fluid values

Use `FluidValue.Create(value, options)` to convert CLR values with the configured value converters,
culture, and time zone. To create a specific built-in value directly, use its factory, such as
`StringValue.Create("hello")`, `ArrayValue.Create(values)`, or `FunctionValue.Create(action)`.
Factories can reuse cached instances; use `NilValue.Instance`, `BooleanValue.True`, and the other
predefined values where appropriate.

Built-in value constructors are private. When migrating constructor calls, note that
`StringValue.Create(null)` returns `NilValue.Instance`, not an empty string; use `StringValue.Empty`
when an empty string is intended. `StringValue.Encode` is read-only: select encoding with
`StringValue.Create(text, encode: false)` instead of changing an existing value. The default is
`encode: true`, which honors the encoder supplied when rendering.

Custom `FluidValue` and `ObjectValueBase` subclasses can still define their own constructors.

### Adding a value converter

When the conversion logic is not directly inferred from the type of an object, a value converter can be used.

Value converters can return:
- `null` to indicate that the value couldn't be converted
- a `FluidValue` instance to stop any further conversion and use this value
- another object instance to continue the conversion using custom and internal **type mappings**

The following example shows how to convert any instance implementing an interface to a custom string value:

```csharp
var options = new TemplateOptionsBuilder()
    .AddValueConverter((value) => value is IUser user ? user.Name : null)
    .Build();
```

> Note: Type mappings are defined globally for the application.

`System.Text.Json.Nodes` values are supported by default: `JsonObject` and `JsonArray` map to Liquid dictionaries and arrays, and scalar `JsonValue` nodes map to the corresponding Liquid values. Configured value converters run before these built-in mappings, so they can override the default behavior; return `null` to continue with the built-in conversion.

JSON date strings can be used directly with `date`, `format_date`, and `time_zone`, without first applying a string filter such as `strip`:

```csharp
var model = System.Text.Json.Nodes.JsonNode.Parse("""
    { "published": "2020-05-18T02:13:09+00:00" }
    """).AsObject();
var template = new FluidParser().Parse("{{ published | date: '%Y-%m-%d' }}");
var result = await template.RenderAsync(new TemplateContext(model)); // 2020-05-18
```

## Altering exposed .NET properties

### Converting object types

Use the `ValueConverters` property to return different values than those provided by the model classes and properties:

```csharp
var options = new TemplateOptionsBuilder()
    .AddValueConverter(o => o is DateTime d ? StringValue.Create($"This is a date time: {d}") : null)
    .Build();
```

The previous example will return a custom value instead of the actual `DateTime`. When no conversion should be applied, `null` is returned.

### Customizing object properties

A common scenario is to access named properties on an object that do not exist in the source class, or should return a different result.

In this case, the `ValueConverters` can be used to return a specific wrapper/proxy `FluidValue` instance.
In practice, you can inherit from `ObjectValueBase` as it implements how most objects should behave.

The following example shows how to provide a custom transformation for any `Person` object:

```csharp
private class PersonValue : ObjectValueBase
{
    public PersonValue(Person value) : base(value)
    {
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        if (name == "Bingo")
        {
          return StringValue.Create("Hello, World!");
        }
    }
}
```

This custom type can be used with a converter so that any time a `Person` is used, it is wrapped as a `PersonValue`.

```csharp
var options = new TemplateOptionsBuilder()
    .AddValueConverter(o => o is Person p ? new PersonValue(p) : null)
    .Build();
```

Invoking the member `Bingo` on a `Person` instance will then return the string `"Hello, World!"`:

```liquid
{{ myPerson.Bingo }}
```

> Note: This technique can also be used to substitute existing properties with other values or even computed data.

### Custom array values

A custom `FluidValue` whose `Type` is `FluidValues.Array` is enumerated with `EnumerateAsync`. If it also implements `IFluidArray`,
`for`, `tablerow`, `include ... for`, `render ... for` and the `first`, `last` and `slice` filters read its items by index instead,
so `limit`, `offset` and `reversed` only read the items they render. This lets a large collection convert its items on demand:

```csharp
public sealed class LazyArrayValue : FluidValue, IFluidArray
{
    private readonly IReadOnlyList<object> _items;
    private readonly TemplateOptions _options;

    public LazyArrayValue(IReadOnlyList<object> items, TemplateOptions options)
    {
        _items = items;
        _options = options;
    }

    public int Count => _items.Count;

    public FluidValue this[int index] => FluidValue.Create(_items[index], _options);

    public override FluidValues Type => FluidValues.Array;

    // EnumerateAsync, GetValueAsync ("size", "first", "last"), GetIndexAsync and the other members as usual
}
```

Return `NilValue.Instance` for a missing item; `null` is read as `nil`. `ArrayValue` and the arrays created from .NET collections
implement `IFluidArray` too, and are equal to any `IFluidArray` with equal items: override `Equals(FluidValue)` and `GetHashCode()`
the same way, comparing and hashing the items, so that `a == b` and `b == a` agree.

### Object members casing

By default, the properties of a registered object are case-sensitive and registered as they are in their source code. For instance, 
the property `FirstName` would be accessed using the `{{ p.FirstName }}` tag.

However, you can register these properties with different cases, like __camelCase__ (`firstName`), __snake_case__ (`first_name`), or even make them case-insensitive. The `WithModelNamesComparer` option accepts an instance of `System.StringComparer`.

The following example configures the templates to use camel casing.

```csharp
var options = new TemplateOptionsBuilder()
    .WithModelNamesComparer(StringComparers.CamelCase)
    .Build();
```

With this setting, both model properties and context properties are accessible using camel-casing:

```liquid
{{ firstName }} {{ lastName }}
```

## NativeAOT and trimming

Fluid works when targeting NativeAOT and trimmed deployments.

- If dynamic code is not supported at runtime, Fluid automatically switches to reflection-based member accessors.
- `TemplateOptionsBuilder.ConfigureMemberAccess` and the `MemberAccessStrategy.Register<T...>` APIs are available for custom mappings.
- No interceptor setup is required.

### Recommended usage when targeting NativeAOT

1. Build `TemplateOptions` once (for example, at app startup) and reuse the instance.
2. Register custom mappings with `TemplateOptionsBuilder.ConfigureMemberAccess(strategy => strategy.Register<T...>(...))`. Built options reject further registrations.
3. Use the [model accessor generator](model-accessor-generator.md) so that models are read without reflection.
4. Validate your app with AOT/trim publish settings:

```shell
dotnet publish -c Release -r <RID> -p:PublishAot=true
```

### Compatibility boundaries

NativeAOT compatibility and trimming compatibility are related but separate. Fluid's reflection fallback does not emit code and can run when dynamic code is unavailable. A trimmed application must also preserve every member that the fallback discovers at runtime.

| Usage | NativeAOT with trimming |
| --- | --- |
| `new TemplateContext(concreteModel, customOptions)` with the source generator enabled and matching compile-time and runtime model types | Compatible. Eligible public fields and properties are accessed directly by generated code. |
| A model registered with `[FluidRegister]` | Compatible. Use this for boxed models, nested model types, models created in another assembly, or types not visible at a `TemplateContext` construction site. |
| An explicit `MemberAccessStrategy.Register<T...>` mapping or custom `MemberAccessor` that accesses members directly | Compatible. The application supplies the access logic instead of relying on member discovery. |
| The one-argument `new TemplateContext(model)` constructor or `TemplateOptions.Default` | No accessor is inferred. Rendering uses an explicit registration if one exists; otherwise it falls back to reflection. |
| A model passed as `object`, an interface or base type whose runtime type differs, or an unregistered nested model | No accessor is inferred for the runtime type. Use `[FluidRegister]` for the concrete runtime type or register an accessor explicitly. |
| Reflection fallback for an unregistered type | NativeAOT-compatible only when the required public member metadata is preserved from trimming. Prefer source generation or explicit registration rather than relying on linker configuration. |
| A reflection-discovered `Task<T>` member | Avoid in trimmed NativeAOT applications because the reflection fallback uses runtime dynamic binding to read the result. A generated or custom accessor handles `Task<T>` without dynamic binding. |

Source generation covers public readable properties and public fields that can be referenced from generated code. Members that cannot be generated continue through the normal registration and reflection fallback paths. If any required member uses a fallback path, validate the published application rather than assuming that source generation preserved it.

### Custom member accessors

Custom accessors derive from `MemberAccessor` and return a `ValueTask<FluidValue>` directly.

```csharp
private sealed class ProductDisplayNameAccessor : MemberAccessor
{
    public override ValueTask<FluidValue> GetAsync(
        object obj,
        string name,
        TemplateContext context)
    {
        return CreateValueTask(((Product)obj).Name, context);
    }
}

var options = new TemplateOptionsBuilder()
    .ConfigureMemberAccess(strategy => strategy.Register<Product>(
        "display_name",
        new ProductDisplayNameAccessor()))
    .Build();
```

The protected `CreateValueTask` overloads convert synchronous, `Task<T>`, and `ValueTask<T>` results using the `TemplateOptions.ValueConverters` configured for the current context. Return `NilValue.Instance` for a Liquid `nil` value; a null `FluidValue` is reserved as the accessor's not-handled result.

An accessor registered for all the members of a type, with the `"*"` name, receives the member name on every access. To match it only once, override `ResolveMember(name, comparer)` and return an accessor dedicated to that member, which Fluid then caches; return `this` for the names it doesn't handle. The accessors written by the [source generator](model-accessor-generator.md) work this way.
