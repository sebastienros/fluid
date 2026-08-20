# AGENTS.md

Guidance for coding agents working in this repository.

## Reference implementation

The reference implementation of the Liquid template language in Ruby can be found at https://github.com/Shopify/liquid. Refer to it when the specification is unclear.

## Commands

```shell
dotnet build                              # SDK pinned by global.json (10.0.100, rollForward latestMajor)
dotnet test                               # xUnit v3 on Microsoft.Testing.Platform (set in global.json)
dotnet test /p:Compiled=true              # second CI pass: exercises the compiled Parlot grammar
dotnet test --list-tests
dotnet run -c Release --project Fluid.Benchmarks
```

CI (`.github/workflows/pr.yml`) runs both test passes in Release on Linux/macOS/Windows. `Fluid.Tests` targets `net10.0` only, so `dotnet test` is already the single-TFM run that keeps the dev loop fast.

`/p:Compiled=true` defines the `COMPILED` constant, and ~18 test classes use it to swap `new FluidParser()` for `new FluidParser().Compile()` (see `Fluid.Tests/ParserTests.cs:14`). A change that passes one pass but not the other usually means a grammar rule behaves differently once Parlot compiles it.

## Golden Liquid tests

`Fluid.Tests/GoldenLiquidTests.cs` runs the [Golden Liquid](https://github.com/jg-rp/golden-liquid) suite. The test definitions are not in that class, they come from `https://raw.githubusercontent.com/jg-rp/golden-liquid/main/golden_liquid.json`.

Golden Liquid tests always prevail: if one contradicts an existing unit test, update the unit test.

Run them all:

```shell
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -method "Fluid.Tests.GoldenLiquidTests.GoldenTestShouldPass"
```

Run a single one — with xUnit v3 and MTP v2, use the test executable directly with `-id`. The `-preEnumerateTheories` flag is required to enumerate the parameterized tests.

```shell
# Find the test ID
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -list full 2>&1 | grep -B2 -A5 "identifiers_ascii_lowercase"

# Run it, using the ID from the output above
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -id "71958641a76ed3a8219c73a9e5f956b4ecf2cb1b07ca728d3d8c8365646e7895"
```

## Build configuration

- Central package management: add/update versions in `Directory.Packages.props`, never in a `.csproj`.
- `Common.props` sets `TreatWarningsAsErrors=true` and strong-name signing (`Fluid.snk`) for all projects — warnings break the build.
- `Fluid` multi-targets `netstandard2.0;net8.0;net9.0;net10.0`. New core code must compile on netstandard2.0: PolySharp supplies language polyfills and `Fluid/Shims.cs` (`#if !NET6_0_OR_GREATER`) supplies the missing BCL overloads.

## Architecture

Pipeline: **source text → Parlot grammar → `Statement` AST → async render into an `IFluidOutput`.**

**Parsing** — `Fluid/FluidParser.cs` builds `Grammar`, a `Parser<IReadOnlyList<Statement>>` from [Parlot](https://github.com/sebastienros/parlot) combinators. `FluidParserExtensions.Parse/TryParse` runs it and wraps the result in `FluidTemplate` (`Fluid/Parser/FluidTemplate.cs`), which implements `IFluidTemplate`. `FluidParser.Compile()` (`Fluid/FluidParser.cs:828`) compiles the grammar and every entry in `RegisteredTags` for faster parsing.

`FluidParserOptions` gates non-default syntax (`AllowFunctions`, `AllowParentheses`, `AllowLiquidTag`, `AllowTrailingQuestionMark`) and is fixed at construction — some options rewire which tag-start/tag-end parsers are used, so it cannot be changed later.

**Rendering** — every node derives from `Fluid/Ast/Statement.cs`:
`ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)`. `Completion` (`Normal`/`Break`/`Continue`) is how `{% break %}`/`{% continue %}` propagate out of nested blocks — a statement that contains children must stop and bubble up any non-`Normal` completion. `FluidParserExtensions.RenderStatementsAsync` is the reference implementation of that loop, and of the repo-wide async idiom: run synchronously while `ValueTask.IsCompletedSuccessfully`, and only fall into an `Awaited` local function when a task actually suspends. Match that shape in new statements rather than making everything `async`.

**Values** — `Fluid/Values/FluidValue.cs` and its subclasses are the only things the engine manipulates at runtime. CLR objects enter through `FluidValue.Create` and `TemplateOptions.ValueConverters`. Prefer the cached singletons (`NilValue.Instance`, `BooleanValue.True`, `Statement.NormalCompletion`) over new allocations.

**Options vs context** — `TemplateOptions` is shared, effectively-immutable application configuration (filters, member access strategy, culture, time zone, execution limits) and should be created once. `TemplateContext` is per-render and **not** thread-safe; it owns the scope chain, the model, and the step/recursion counters. `FluidParser` and `IFluidTemplate` instances are thread-safe and meant to be cached.

**Security / member access** — Fluid is allow-list based: a .NET member is invisible to templates until registered. `MemberAccessStrategy` + `MemberAccessor` resolve members, with emit-based accessors when dynamic code is available and `Reflection*Accessor` fallbacks otherwise (NativeAOT/trimming). `Fluid.SourceGenerator/MemberAccessorGenerator.cs` generates accessors for types marked with `[FluidRegister]`; the `TemplateOptions` constructor invokes them via `RegisterGeneratedMemberAccessors()`. Changes to accessor resolution need to hold for all three paths — emit, reflection, generated.

**Filters** — `FilterCollection` maps a name to a `FilterDelegate`. Built-ins live in `Fluid/Filters/{Array,String,Number,Misc,Color,Money}Filters.cs` and are wired up by `With*Filters()` extension methods. The `TemplateOptions` constructor calls `WithArrayFilters().WithStringFilters().WithNumberFilters().WithMiscFilters()`; `WithColorFilters()` and `WithMoneyFilters()` exist but are opt-in. A new built-in filter goes in the matching file plus its `With*Filters` method. `WithMoneyFilters()` is configured by `TemplateOptions.MoneyOptions` (`Fluid/MoneyOptions.cs`), which is mirrored on `TemplateContext` so a currency can be picked per render.

**Extending the grammar** — `FluidParser.Register{Empty,Identifier,Expression,Parser}{Tag,Block}` add custom tags/blocks; `RegisteredOperators` adds binary operators. `Fluid.ViewEngine/FluidViewParser.cs` is the worked example, adding `layout`, `section`, `rendersection`, and `renderbody` on top of the base parser.

**Visitors** — `Fluid/Ast/AstVisitor.cs` and `AstRewriter.cs` walk or rewrite a parsed template via `Statement.Accept`. A new statement type must override `Accept` and get a matching visitor hook, or it becomes opaque to analysis and rewriting.

## Project layout

| Project | Role |
| --- | --- |
| `Fluid` | Core engine, packaged as `Fluid.Core` |
| `Fluid.SourceGenerator` | Roslyn generator for `[FluidRegister]` member accessors |
| `Fluid.ViewEngine` | Layout/section tags, `FluidViewParser`, `FluidViewRenderer` |
| `Fluid.MvcViewEngine` | ASP.NET Core MVC `IViewEngine` on top of `Fluid.ViewEngine` |
| `MinimalApis.LiquidViews` | Minimal APIs integration |
| `Fluid.Tests` | All tests (net10.0) |
| `Fluid.Benchmarks` | BenchmarkDotNet, incl. comparisons against DotLiquid/Scriban/Handlebars |
| `Fluid.MvcSample`, `Fluid.MinimalApisSample` | Sample apps |

## Performance

Performance is a primary design goal of this library, not an afterthought — Fluid competes on benchmarks against DotLiquid, Scriban, and Handlebars.Net, and allocation counts matter as much as throughput. Hot paths are parsing, member access resolution, filter dispatch, `FluidValue` conversions, and output writing.

Use the newest language and runtime features available when they genuinely help — `stackalloc` and `Span<T>`/`ReadOnlySpan<T>`, `SearchValues<char>` for multi-character scanning, `ArrayPool<char>`, ref structs, `MemoryMarshal`, vectorized/SIMD APIs, collection expressions, and newer BCL overloads that avoid intermediate allocations. `LangVersion` is `latest` everywhere, so new C# syntax is available. The bar is "measurably faster or fewer allocations for a realistic template", not novelty — a change that only makes the code look modern is not worth the churn.

Existing patterns to follow rather than reinvent:

- `Fluid/Utils/ValueStringBuilder.cs` — ref struct builder seeded from `stackalloc`, spilling to `ArrayPool<char>`. Used throughout `Fluid/Filters/MiscFilters.cs` (e.g. `new ValueStringBuilder(stackalloc char[32])`).
- `Fluid/Utils/BufferFluidOutput.cs` and `TextWriterFluidOutput.cs` — pooled output buffering behind `IFluidOutput`.
- `Fluid/Values/NumberValue.cs:131` — `Span<char>` scratch with an `ArrayPool` fallback for larger formats.
- `FluidParserExtensions.RenderStatementsAsync` — synchronous `ValueTask` fast path, `Awaited` local function only when a task actually suspends.
- Cached singletons (`NilValue.Instance`, `BooleanValue.True`, `Statement.NormalCompletion`) instead of fresh allocations.

Because `Fluid` still targets netstandard2.0, APIs that only exist on modern runtimes go behind a TFM guard with a working fallback — `Fluid/FluidOutputExtensions.cs:78` (`#if NET8_0_OR_GREATER`) and `Fluid/Filters/StringFilters.cs:205` (`#if NET6_0_OR_GREATER`) are the model. `SearchValues<T>` in particular is net8.0+, so it needs this treatment. Do not drop the fallback path; do not regress netstandard2.0 behavior.

Measure with `Fluid.Benchmarks` (`dotnet run -c Release --project Fluid.Benchmarks`) before and after any hot-path change, and keep the numbers — an unmeasured performance claim is not one.

## Conventions

- `README.md` is the user-facing documentation *and* the NuGet package readme — behavior changes visible in templates belong there too.
- Public API changes should keep the netstandard2.0 surface working, and obsolete members are kept with `[Obsolete]` rather than removed (see `FluidValue.ToStringValue()` and friends).
