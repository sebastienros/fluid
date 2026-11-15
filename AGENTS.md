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

## Checking behavior against the Liquid gem

`scripts/liquid-gem/liquid-gem.sh` (bash) and `liquid-gem.ps1` (PowerShell, same options) render a template with the [Liquid gem](https://github.com/Shopify/liquid), the reference implementation, in Docker. Use them to confirm the expected output before changing behavior, or to verify a fix. The image (`scripts/liquid-gem/Dockerfile`, Liquid 5.14.0 on Ruby 3.4) is built on the first run.

```shell
scripts/liquid-gem/liquid-gem.sh -e '{{ "a,b" | split: "," | join: "-" }}'   # gem output, raw on stdout
scripts/liquid-gem/liquid-gem.sh -c -e '{%- for i in (1..3) -%} {{ i }} {% endfor -%}'   # diff with Fluid
scripts/liquid-gem/liquid-gem.sh -c -z America/New_York -j '{"t": {"$time": "2026-03-04T13:00:00"}}' -e '{{ t | date: "%H:%M %z" }}'
scripts/liquid-gem/liquid-gem.sh template.liquid -d data.json --expected-file expected.txt
```

- `-c` renders with Fluid too (`scripts/liquid-gem/compare.cs`), parsing with `AllowLiquidTag = true`, the only opt-in `FluidParserOptions` syntax the gem supports. Two syntax errors match, since the messages differ. Render errors match when their messages are the same.
- `-d`/`-j` give the variables as JSON. `{"$time": "..."}` is a Ruby `Time` and a .NET `DateTime` (in the `-z` time zone without an offset, `Time.utc` and a Utc `DateTime` with `Z`, a `DateTimeOffset` with an offset), `{"$date": "..."}` a `Date` and a `DateOnly`. Integers are `Integer`/`long`, other numbers `Float`/`decimal`.
- `-z` sets `TZ` for Ruby and `TemplateContext.TimeZone` for Fluid (default UTC). `-m` sets the gem's error mode (default `strict`).
- Exit codes: 0 rendered or matched, 1 differ, 2 syntax error, 3 usage or Docker error, 4 render error. `--help` lists all options.
- Needs Docker, and the .NET SDK for `-c` and `--expected`. No partials, and no network access is needed after the image is built.
- `scripts/shopify-liquid.cs`, which renders with Shopify's storefront playground, is deprecated: the storefront differs from the gem (it reads `"1772643600.75"` and `0.5` as timestamps, for instance). Use it only to check storefront objects like `product`.

## Build rules

- Package versions go in `Directory.Packages.props`, never in a `.csproj`.
- `TreatWarningsAsErrors=true`: warnings break the build.
- `Fluid` multi-targets `netstandard2.0;net8.0;net10.0;net11.0`. New core code must compile on all; use `Fluid/Shims.cs` and `#if` guards with a working fallback for newer APIs (e.g. `SearchValues<T>` is net8.0+). Never drop the fallback.
- `net11.0` builds use runtime async (`Features` `runtime-async=on` in `Directory.Build.targets`): async methods compile without state machines there, so check async hot-path changes on net11.0 too. Runtime async is still an opt-in preview feature in .NET 11.0 GA, so the flag stays. `global.json` requires the .NET 11 SDK (11.0.100 or a later 11.0 feature band).

## Architecture

Pipeline: source → Parlot grammar (`Fluid/FluidParser.cs`) → `Statement` AST → async render to `IFluidOutput`.

- **Rendering**: nodes derive from `Statement` (`WriteToAsync` returns `ValueTask<Completion>`). Statements with children must stop and bubble up any non-`Normal` completion (`break`/`continue`). Follow `FluidParserExtensions.RenderStatementsAsync`: stay synchronous while the `ValueTask` is completed, fall into an `Awaited` local function only on suspension. Don't make everything `async`.
- **Values**: the engine only manipulates `FluidValue` subclasses. Prefer cached singletons (`NilValue.Instance`, `BooleanValue.True`, `Statement.NormalCompletion`). Convert doubles and floats to decimals with `NumberConversions.ToDecimal`, never a cast or `Convert.ToDecimal`: since .NET 11 those keep the exact binary value (0.1 becomes 0.1000000000000000055511151231) instead of 15 or 7 significant digits.
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
- `Fluid.Benchmarks` targets net11.0, so it measures the runtime async build of Fluid. It needs a BenchmarkDotNet 0.16 preview: 0.15.8 can't generate a net11.0 project.

### Benchmark results

On a quiet machine, run `dotnet run -c Release --project Fluid.Benchmarks -- --filter '*ComparisonBenchmarks*'`, then `python3 Fluid.Benchmarks/Charts/generate_charts.py` to regenerate `docs/_content/fluid/assets/benchmarks/benchmark-*.svg` and `.png` from the report (PNGs need Chrome or Edge). Then update `docs/_content/fluid/benchmarks.md` by hand: the summary sentences, test date, package versions and the Benchmark.NET table, and the README "Performance" summary if the conclusion changes.

The "Compiled templates and static engines" section of that page comes from `--filter '*StaticRenderingBenchmarks*'` (Fluid's compiled templates, RazorSlices and Lissic against a parsed template): `generate_charts.py` regenerates its `benchmark-static-*` charts from that report too, then update its summary, versions and table by hand. The script skips the charts of a report that wasn't generated.

## Documentation

`README.md` is both the quick start guide and the NuGet readme: it introduces every feature with a short example and links to the documentation site for the details. Update it for any template-visible behavior change, and put details (options, edge cases, compatibility notes, breaking changes) in the matching page of the site. The README is rendered on nuget.org, so its links and images use absolute URLs: `https://sebastienros.github.io/fluid/...` for pages, `https://github.com/sebastienros/fluid/raw/main/...` for images.

The documentation site (https://sebastienros.github.io/fluid/) is built by [Douze](https://sebastienros.github.io/douze/) from `docs/`, and deployed with the playground by `.github/workflows/docs.yml`; `docs/README.md` explains how to build and preview it.

- Pages are Markdown files in `docs/_content/fluid/` with a `title` and a `description` in their front matter, and the sidebar lists them in `docs/_data/sidebar.yml`. Links between pages are relative links to the `.md` files; Douze checks them, and the anchors, on every build.
- Page content is not Liquid (`liquid: false` in `docs/_content/fluid/fluid.yml`), so Liquid samples are written as they are.
- `Fluid.Playground` (Blazor WebAssembly) is the playground, and `Fluid.Playground.Core` its logic, tested by `Fluid.Tests/Playground`. Options worth trying in the browser go in `PlaygroundOptions`, and samples in `PlaygroundSamples`. The editors are colored by [Tint](https://github.com/sebastienros/tint) (`PlaygroundHighlighter`), with Shopify's Liquid grammar vendored in `Fluid.Playground.Core/Grammars` (MIT, its license next to it). .NET's WebAssembly runtime has no MD5, so the `md5` filter fails there.
