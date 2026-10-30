# AGENTS.md

Fluid is a .NET Liquid template engine. Reference implementation for ambiguous spec questions: https://github.com/Shopify/liquid (Ruby).

## Branches

- `main` targets an unreleased major version: API and behavior breaking changes are allowed. Prefer the right design over backward compatibility, and call out breaking changes in the PR description.
- Maintenance branches of released versions must stay compatible (keep members with `[Obsolete]` instead of removing them).

### Backports

After a PR is merged, a collaborator with write access can comment `/backport to release/2.x`
(or another existing target branch) on the source PR. `.github/workflows/backport.yml`
uses Arcade's reusable workflow to create the backport branch and PR in this repository.
Conflicts are reported on the source PR and must be resolved manually. The workflow uses
`GITHUB_TOKEN`, so its pushes and PRs do not automatically trigger other workflows.

## Commands

```shell
dotnet build
dotnet test                               # xUnit v3 on Microsoft.Testing.Platform
dotnet run -c Release --project Fluid.Benchmarks
```

- Use `--property:`, never `/p:` (Git Bash on Windows mangles `/p:` and the run executes zero tests).
- Parlot 2.0 does not require runtime grammar compilation; `FluidParser.Compile()` is not available.

## Golden Liquid tests

`Fluid.Tests/GoldendLiquidTests.cs` (class `GoldenLiquidTests`) runs the [Golden Liquid](https://github.com/jg-rp/golden-liquid) suite (definitions downloaded from its repo). Golden tests always prevail: if one contradicts a unit test, update the unit test.

Run them from the test executable after `dotnet build`. Each test's display name ends with `(test: <id>)`, the snake_case test name.

```shell
# all
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests --filter-class "Fluid.Tests.GoldenLiquidTests"
# list them
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests --list-tests --filter-class "Fluid.Tests.GoldenLiquidTests"
# one, or several with a wildcard ('*' only at the start or end)
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests --filter-display-name "*(test: <id>)"
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests --filter-display-name "*(test: filters_date_*"
```

## Checking behavior against Shopify

`scripts/shopify-liquid.cs` renders a template with Shopify's official engine (the playground behind https://shopify.dev/docs/api/liquid). Use it to confirm the expected output before changing behavior, or to verify a fix.

```shell
dotnet run scripts/shopify-liquid.cs -- -e '{{ "a,b" | split: "," | join: "-" }}'   # Shopify output, raw on stdout
dotnet run scripts/shopify-liquid.cs -- -c -e '{%- for i in (1..3) -%} {{ i }} {% endfor -%}'   # diff with Fluid
dotnet run scripts/shopify-liquid.cs -- template.liquid --expected-file expected.txt
```

- No custom data: make templates self-contained with `assign`/`capture` (arrays via `split`). `--path /products/health-potion` exposes demo store objects such as `product`; `--show-data` prints the ones used.
- Exit codes: 0 match, 1 differ, 2 invalid template (Shopify only says `Syntax error`), 3 usage/network error. `--help` lists all options.
- Needs network access to a public shopify.dev endpoint: don't use it from tests or CI.

## Build rules

- Package versions go in `Directory.Packages.props`, never in a `.csproj`.
- `TreatWarningsAsErrors=true`: warnings break the build.
- `Fluid` multi-targets `netstandard2.0;net8.0;net9.0;net10.0`. New core code must compile on all; use `Fluid/Shims.cs` and `#if` guards with a working fallback for newer APIs (e.g. `SearchValues<T>` is net8.0+). Never drop the fallback.

## Architecture

Pipeline: source → Parlot grammar (`Fluid/FluidParser.cs`) → `Statement` AST → async render to `IFluidOutput`.

- **Rendering**: nodes derive from `Statement` (`WriteToAsync` returns `ValueTask<Completion>`). Statements with children must stop and bubble up any non-`Normal` completion (`break`/`continue`). Follow `FluidParserExtensions.RenderStatementsAsync`: stay synchronous while the `ValueTask` is completed, fall into an `Awaited` local function only on suspension. Don't make everything `async`.
- **Values**: the engine only manipulates `FluidValue` subclasses. Prefer cached singletons (`NilValue.Instance`, `BooleanValue.True`, `Statement.NormalCompletion`).
- **Options vs context**: `TemplateOptions` is shared, immutable configuration with no public constructor or setters: configure a `TemplateOptionsBuilder` (`With*`/`Configure*`/`Add*`), then `Build()`, which copies filters, global values, converters and member access registrations and creates a new `MemberAccessStrategy`. `ToBuilder()` derives variations; a new option goes on the builder, `TemplateOptions` and the `ToBuilder` copy constructor together. `MoneyOptions` is immutable too. `TemplateContext` is per-render and not thread-safe. `FluidParser` and `IFluidTemplate` are thread-safe and should be cached.
- **FluidParserOptions** is immutable (`init`-only; some options rewire tag parsers).
- **Member access** is allow-list based. Changes to accessor resolution must work for all three paths: emit, `Reflection*Accessor` fallbacks, and source-generated (`Fluid.SourceGenerator`, `[FluidRegister]` on a `TemplateOptionsBuilder` subclass or a static partial method taking one). `Build()` makes the strategy read-only, so explicit `Register` calls go through `ConfigureMemberAccess`.
- **Filters**: add built-ins to the matching `Fluid/Filters/*Filters.cs` plus its `With*Filters()` method. Color and Money filters are opt-in (`WithColorFilters()`, `WithMoneyFilters()` on the builder); `TemplateOptions.Filters` is a read-only copy.
- **Grammar extension**: `Register*Tag/Block`, `RegisteredOperators`; `Fluid.ViewEngine/FluidViewParser.cs` is the worked example.
- **Visitors**: a new `Statement` must override `Accept` and have a matching hook in `AstVisitor`/`AstRewriter`.

## Performance

Performance is a primary goal (competes with DotLiquid, Scriban, Handlebars.Net); allocations matter as much as throughput. Hot paths: parsing, member access, filter dispatch, `FluidValue` conversion, output writing.

- Use modern features (`Span<T>`, `stackalloc`, `ArrayPool`, `SearchValues`, ref structs) only when measurably faster or fewer allocations.
- Reuse existing patterns: `ValueStringBuilder`, `BufferFluidOutput`, cached singletons, the synchronous `ValueTask` fast path.
- Benchmark with `Fluid.Benchmarks` before and after hot-path changes and report the numbers.

### README benchmark results

On a quiet machine, run `dotnet run -c Release --project Fluid.Benchmarks -- --filter '*ComparisonBenchmarks*'`, then `python3 Fluid.Benchmarks/Charts/generate_charts.py` to regenerate `Assets/benchmark-*.svg` and `.png` from the report (PNGs need Chrome or Edge). Then update the README "Benchmarks" section by hand: the summary sentences, test date, package versions and the Benchmark.NET table.

The README "Compiled templates and static engines" section comes from `--filter '*StaticRenderingBenchmarks*'` (Fluid's compiled templates, RazorSlices and Lissic against a parsed template): `generate_charts.py` regenerates its `Assets/benchmark-static-*` charts from that report too, then update its summary, versions and table by hand. The script skips the charts of a report that wasn't generated.

## Documentation

`README.md` is both user docs and the NuGet readme: update it for any template-visible behavior change.
