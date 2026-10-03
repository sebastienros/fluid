# AGENTS.md

Fluid is a .NET Liquid template engine. Reference implementation for ambiguous spec questions: https://github.com/Shopify/liquid (Ruby).

## Branches

- `main` targets an unreleased major version: API and behavior breaking changes are allowed. Prefer the right design over backward compatibility, and call out breaking changes in the PR description.
- Maintenance branches of released versions must stay compatible (keep members with `[Obsolete]` instead of removing them).

## Commands

```shell
dotnet build
dotnet test                               # xUnit v3 on Microsoft.Testing.Platform
dotnet test --property:Compiled=true      # second CI pass: compiled Parlot grammar
dotnet run -c Release --project Fluid.Benchmarks
```

- Use `--property:`, never `/p:` (Git Bash on Windows mangles `/p:` and the run executes zero tests).
- CI runs both test passes. If a change passes only one, a grammar rule behaves differently once compiled (`COMPILED` constant swaps in `new FluidParser().Compile()`).

## Golden Liquid tests

`Fluid.Tests/GoldenLiquidTests.cs` runs the [Golden Liquid](https://github.com/jg-rp/golden-liquid) suite (definitions downloaded from its repo). Golden tests always prevail: if one contradicts a unit test, update the unit test.

```shell
# all
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -method "Fluid.Tests.GoldenLiquidTests.GoldenTestShouldPass"
# one: find its id, then run it (-preEnumerateTheories is required)
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -list full 2>&1 | grep -B2 -A5 "<test_name>"
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -id "<id>"
```

## Build rules

- Package versions go in `Directory.Packages.props`, never in a `.csproj`.
- `TreatWarningsAsErrors=true`: warnings break the build.
- `Fluid` multi-targets `netstandard2.0;net8.0;net9.0;net10.0`. New core code must compile on all; use `Fluid/Shims.cs` and `#if` guards with a working fallback for newer APIs (e.g. `SearchValues<T>` is net8.0+). Never drop the fallback.

## Architecture

Pipeline: source → Parlot grammar (`Fluid/FluidParser.cs`) → `Statement` AST → async render to `IFluidOutput`.

- **Rendering**: nodes derive from `Statement` (`WriteToAsync` returns `ValueTask<Completion>`). Statements with children must stop and bubble up any non-`Normal` completion (`break`/`continue`). Follow `FluidParserExtensions.RenderStatementsAsync`: stay synchronous while the `ValueTask` is completed, fall into an `Awaited` local function only on suspension. Don't make everything `async`.
- **Values**: the engine only manipulates `FluidValue` subclasses. Prefer cached singletons (`NilValue.Instance`, `BooleanValue.True`, `Statement.NormalCompletion`).
- **Options vs context**: `TemplateOptions` is shared, effectively immutable; create once. `TemplateContext` is per-render and not thread-safe. `FluidParser` and `IFluidTemplate` are thread-safe and should be cached.
- **FluidParserOptions** is fixed at construction (some options rewire tag parsers).
- **Member access** is allow-list based. Changes to accessor resolution must work for all three paths: emit, `Reflection*Accessor` fallbacks, and source-generated (`Fluid.SourceGenerator`, `[FluidRegister]`).
- **Filters**: add built-ins to the matching `Fluid/Filters/*Filters.cs` plus its `With*Filters()` method. Color and Money filters are opt-in.
- **Grammar extension**: `Register*Tag/Block`, `RegisteredOperators`; `Fluid.ViewEngine/FluidViewParser.cs` is the worked example.
- **Visitors**: a new `Statement` must override `Accept` and have a matching hook in `AstVisitor`/`AstRewriter`.

## Performance

Performance is a primary goal (competes with DotLiquid, Scriban, Handlebars.Net); allocations matter as much as throughput. Hot paths: parsing, member access, filter dispatch, `FluidValue` conversion, output writing.

- Use modern features (`Span<T>`, `stackalloc`, `ArrayPool`, `SearchValues`, ref structs) only when measurably faster or fewer allocations.
- Reuse existing patterns: `ValueStringBuilder`, `BufferFluidOutput`, cached singletons, the synchronous `ValueTask` fast path.
- Benchmark with `Fluid.Benchmarks` before and after hot-path changes and report the numbers.

## Documentation

`README.md` is both user docs and the NuGet readme: update it for any template-visible behavior change.
