# Model accessor generator

The model accessor generator writes the code that reads the members of your .NET types, so that Fluid doesn't need reflection to render them. It is part of the `Fluid.SourceGenerator` package and is unrelated to the [compiled template generator](template-generator.md).

The basic usage is described in the [README](../README.md#model-accessors). This document covers the details.

## When to use it

Fluid reads model members with reflection by default and, when dynamic code is supported, compiles that access at runtime. That is enough for most applications.

Use the generator when:

- The application is published with NativeAOT or trimming. The trimmer can remove members that are only discovered through reflection, and the generated code keeps them.
- You want to avoid the reflection and code generation cost the first time each member is accessed.

The generator can't be used for:

- Types that aren't known at compile time, for example types loaded from a plugin assembly.
- Types the generated code can't reference: private or protected nested types, `file` types and anonymous types.

Those types still work through reflection or an [explicit registration](../README.md#custom-member-accessors).

## Registering types

The `[FluidRegister(typeof(T))]` attribute is generated in the `Fluid` namespace. It can be applied to two kinds of declarations.

### On an options builder

```csharp
[FluidRegister(typeof(Person))]
[FluidRegister(typeof(Address))]
public partial class AppTemplateOptionsBuilder : TemplateOptionsBuilder
{
}
```

The constructor of `AppTemplateOptionsBuilder` registers the generated accessors. Anything configured afterwards with `ConfigureMemberAccess` still applies, and a registration for a specific member name takes precedence over the generated accessor:

```csharp
var options = new AppTemplateOptionsBuilder()
    .ConfigureMemberAccess(strategy => strategy.Register<Product, object>((product, name) => product.Name))
    .Build();
```

The class must be `partial`, non-generic, not nested in another type, and derive from `TemplateOptionsBuilder`. Otherwise the build fails with `FLUIDSG002`.

### On a profile method

Use a profile method to apply the same registrations to builders you don't control, or to several different builders:

```csharp
public static partial class FluidProfiles
{
    [FluidRegister(typeof(Person))]
    [FluidRegister(typeof(Address))]
    public static partial void ApplyModels(TemplateOptionsBuilder builder);
}
```

```csharp
var builder = new TemplateOptionsBuilder();
FluidProfiles.ApplyModels(builder);
var options = builder.Build();
```

The method must be declared `static partial` with an explicit accessibility, return `void`, take a single `TemplateOptionsBuilder` parameter and have no body. Its class must be `partial`, non-generic and not nested. Otherwise the build fails with `FLUIDSG001`.

### Members that are generated

For each registered type, the generated accessor exposes:

- Public properties with a public getter, except indexers.
- Public fields, except constants.
- Public methods without parameters that return a value, except generic methods.

Static members are included, and so are inherited members. For an interface, the members of the interface and of the interfaces it extends are included.

Member names are matched with `TemplateOptions.ModelNamesComparer`, like any other accessor. A registered type only covers its own members: register the types of nested objects too, for example `Address` for `{{ person.Address.City }}`.

## Detected models

The generator also looks for this pattern in the code:

```csharp
var context = new TemplateContext(person, options);
```

and generates an accessor for the type of `person`, without any attribute. The accessor is activated when a `TemplateContext` is created with a model of that type and custom options. It applies when:

- The two-argument constructor is used, and `options` isn't `TemplateOptions.Default`. The one-argument `new TemplateContext(model)` constructor uses the default options and is ignored.
- The compile-time type of the model is a concrete type: not `object`, `dynamic`, a type parameter or an anonymous type.
- The project compiles with C# 9 or later, since the registration runs in a module initializer.

Detected accessors only expose properties and fields, not methods, and only for the model type itself. Use `[FluidRegister]` for nested types, for models passed as `object` or as a base type, and for models created in another project.

## NativeAOT compatibility

NativeAOT compatibility and trimming compatibility are related but separate. Fluid's reflection fallback doesn't emit code and can run when dynamic code is unavailable, but a trimmed application must also preserve every member that the fallback discovers at runtime.

| Usage | NativeAOT with trimming |
| --- | --- |
| `new TemplateContext(concreteModel, customOptions)`, with matching compile-time and runtime model types | Compatible. The model's public properties and fields are read by generated code. |
| A model registered with `[FluidRegister]` | Compatible. Use this for models passed as `object`, nested model types, models created in another project, or types that never appear in a `TemplateContext` constructor. |
| An explicit `MemberAccessStrategy.Register<T...>` mapping or custom `MemberAccessor` that accesses members directly | Compatible. The application supplies the access logic instead of relying on member discovery. |
| The one-argument `new TemplateContext(model)` constructor or `TemplateOptions.Default` | No accessor is detected. Rendering uses an explicit registration if one exists, otherwise it falls back to reflection. |
| A model passed as `object`, or as an interface or base type whose runtime type differs, or an unregistered nested model | No accessor is detected for the runtime type. Use `[FluidRegister]` for the concrete runtime type or register an accessor explicitly. |
| Reflection fallback for an unregistered type | Compatible only when the required public members are preserved from trimming. Prefer the generator or an explicit registration to linker configuration. |
| A reflection-discovered `Task<T>` member | Avoid in trimmed NativeAOT applications, because the reflection fallback uses dynamic binding to read the result. A generated or custom accessor handles `Task<T>` without dynamic binding. |

Members that can't be generated continue through the normal registration and reflection paths. If a template depends on such a member, validate the published application:

```shell
dotnet publish -c Release -r <RID> -p:PublishAot=true
```

## Diagnostics

| Id | Meaning |
| --- | --- |
| `FLUIDSG001` | A method annotated with `[FluidRegister]` isn't a valid profile method. |
| `FLUIDSG002` | A class annotated with `[FluidRegister]` isn't a valid options builder. |
