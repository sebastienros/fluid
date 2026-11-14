---
title: Benchmarks
description: "How Fluid compares with other .NET template engines, and with compiled templates."
---

A benchmark application is provided in the source code to compare Fluid, [Scriban](https://github.com/scriban/scriban), [DotLiquid](https://github.com/dotliquid/dotliquid), [Liquid.NET](https://github.com/mikebridge/Liquid.NET), and [Handlebars.NET](https://github.com/Handlebars-Net/Handlebars.Net).
Run it locally to analyze the time it takes to execute specific templates.

TL;DR — Fluid is faster and allocates less memory than all other well-known .NET Liquid parsers.

## Comparison with other .NET engines

**Parse: Parses a simple HTML template containing filters and properties**

Scriban takes 71% longer to parse and allocates 4 times as much memory as Fluid.

![Parse benchmark comparison](/fluid/assets/benchmarks/benchmark-parse.svg)

**ParseBig: Parses a Blog Post template**

Scriban takes 2.4 times as long to parse and allocates over 5 times as much memory as Fluid.

![Large template parse benchmark comparison](/fluid/assets/benchmarks/benchmark-parse-big.svg)

**Render: Renders a simple HTML template containing filters and properties, with 100 elements**

DotLiquid takes almost 13 times as long to render and allocates 18 times as much memory as Fluid.
The second best, Handlebars (Mustache), takes 66% longer and allocates over 3 times as much memory.

![Render benchmark comparison](/fluid/assets/benchmarks/benchmark-render.svg)

Tested on 11/8/2026 with
- Parlot 2.2.1
- Scriban 7.5.3
- DotLiquid 2.3.260
- Handlebars.Net 2.4.3

- Liquid.NET 0.10.0 (Ignored since much slower and not in active development for a long time)

<details>

<summary>Benchmark.NET data</summary>

``` text
BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.2 (24H232) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.3.26522.148
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

| Method              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|-------------------- |-----------:|------------:|-----------:|-------:|--------:|--------:|--------:|----------:|------------:|
| Fluid_Parse         |   1.411 us |   0.0809 us |  0.0044 us |   1.00 |    0.00 |  0.2975 |       - |   2.45 KB |        1.00 |
| Scriban_Parse       |   2.419 us |   0.2555 us |  0.0140 us |   1.71 |    0.01 |  1.2245 |  0.0648 |  10.01 KB |        4.09 |
| DotLiquid_Parse     |   3.879 us |   0.0956 us |  0.0052 us |   2.75 |    0.01 |  1.9608 |  0.0229 |  16.05 KB |        6.56 |
| Handlebars_Parse    | 440.647 us | 390.3733 us | 21.3977 us | 312.25 |   13.16 | 15.6250 |  7.8125 | 132.63 KB |       54.24 |
|                     |            |             |            |        |         |         |         |           |             |
| Fluid_ParseBig      |   5.933 us |   0.1952 us |  0.0107 us |   1.00 |    0.00 |  1.1368 |  0.0153 |    9.3 KB |        1.00 |
| Scriban_ParseBig    |  13.990 us |   3.9269 us |  0.2152 us |   2.36 |    0.03 |  5.8746 |  1.1749 |  48.07 KB |        5.17 |
| DotLiquid_ParseBig  |  20.270 us |   8.1472 us |  0.4466 us |   3.42 |    0.07 | 11.4136 |  0.5188 |  93.46 KB |       10.05 |
| Handlebars_ParseBig | 344.341 us | 535.0808 us | 29.3296 us |  58.04 |    4.28 | 16.6016 |  7.8125 | 141.46 KB |       15.22 |
|                     |            |             |            |        |         |         |         |           |             |
| Fluid_Render        |  13.454 us |   0.1538 us |  0.0084 us |   1.00 |    0.00 |  4.4250 |  0.0610 |  36.28 KB |        1.00 |
| Scriban_Render      |  67.609 us |  18.1324 us |  0.9939 us |   5.03 |    0.06 | 17.0898 |  2.4414 | 140.73 KB |        3.88 |
| DotLiquid_Render    | 170.340 us |  17.5619 us |  0.9626 us |  12.66 |    0.06 | 80.5664 | 15.3809 | 659.18 KB |       18.17 |
| Handlebars_Render   |  22.341 us |   1.3578 us |  0.0744 us |   1.66 |    0.00 | 14.5874 |       - | 119.31 KB |        3.29 |
```

</details>

## Compiled templates and static engines

Some engines compile templates into the application instead of reading them at runtime. This benchmark compares Fluid's [compiled templates](guide/template-generator.md), [RazorSlices](https://github.com/DamianEdwards/RazorSlices) and [Lissic](https://github.com/gendeg/Lissic) with Fluid rendering a parsed template, and with the same page written by hand in C#. Each one renders the product template with 100 products and HTML-encodes the values. Compiled Fluid templates read the products with [source-generated accessors](guide/model-accessor-generator.md), parsed ones with the default accessors.

**Render: renders the product template to a string**

Compiled Fluid templates are the fastest engine: they take 33% less time than parsed ones and allocate the same memory. RazorSlices takes 24% less time than parsed Fluid templates and allocates 51% more, and Lissic takes 7% more time and allocates 2.3 times as much. Hand-written C# takes 68% less time, for reference.

![Compiled templates and static engines render benchmark comparison](/fluid/assets/benchmarks/benchmark-static-render.svg)

**Render to UTF-8: renders the product template as UTF-8 to a buffer, like an HTTP response body**

Compiled Fluid templates take 31% less time than parsed ones. RazorSlices is the fastest engine, taking 48% less time than parsed Fluid templates and allocating 70% less. Lissic takes 14% more time and allocates 3.5 times as much when each render copies the products into the template classes it generates. When the same model is built once and reused, it takes 21% less time and allocates nothing.

![Compiled templates and static engines UTF-8 render benchmark comparison](/fluid/assets/benchmarks/benchmark-static-render-utf8.svg)

These engines are faster because the compiler reads the template, not the application. In exchange, the template has to be part of the application's source code: changing it means rebuilding and redeploying, and it can't be provided by users, stored in a database or edited after deployment. Fluid can do both: compile the templates that ship with the application, and parse the others, with the same syntax and the same output. Each engine also has its own constraints:

- Fluid's compiled templates are Liquid templates and render like parsed ones, except for a few unsupported features listed in [compiled templates](guide/template-generator.md).
- RazorSlices templates are Razor, so they contain C# code and can never come from users. RazorSlices requires ASP.NET Core.
- Lissic templates have no expressions or conditions, only a choice between branches by a value. The data has to be copied into the classes it generates, and values are only HTML-encoded with a filter.

Tested on 11/8/2026 on the same machine as the results above, with
- RazorSlices 0.12.2
- Lissic 0.1.4

<details>

<summary>Benchmark.NET data</summary>

```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.2 (24H232) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.3.26522.148
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

```
| Method                           | Categories | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |----------- |----------:|----------:|----------:|------:|--------:|--------:|-------:|----------:|------------:|
| HandWritten_Render               | String     |  4.805 μs | 0.0949 μs | 0.1015 μs |  0.32 |    0.01 |  5.3329 | 0.3281 |   44720 B |        1.20 |
| Fluid_Render                     | String     | 14.895 μs | 0.2933 μs | 0.5213 μs |  1.00 |    0.05 |  4.4250 | 0.0610 |   37153 B |        1.00 |
| Fluid_SourceGenerated_Render     | String     |  9.994 μs | 0.1510 μs | 0.2213 μs |  0.67 |    0.03 |  4.4098 | 0.0458 |   37089 B |        1.00 |
| RazorSlices_Render               | String     | 11.272 μs | 0.0546 μs | 0.0511 μs |  0.76 |    0.03 |  6.6833 | 0.7324 |   56112 B |        1.51 |
| Lissic_Render                    | String     | 15.890 μs | 0.1753 μs | 0.1554 μs |  1.07 |    0.04 | 10.1013 | 1.7090 |   84744 B |        2.28 |
|                                  |            |           |           |           |       |         |         |        |           |             |
| Fluid_RenderUtf8                 | Utf8       | 13.580 μs | 0.1099 μs | 0.1028 μs |  1.00 |    0.01 |  2.2888 | 0.0305 |   19201 B |        1.00 |
| Fluid_SourceGenerated_RenderUtf8 | Utf8       |  9.417 μs | 0.0597 μs | 0.0559 μs |  0.69 |    0.01 |  2.2736 | 0.0305 |   19137 B |        1.00 |
| RazorSlices_RenderUtf8           | Utf8       |  7.000 μs | 0.0262 μs | 0.0219 μs |  0.52 |    0.00 |  0.6866 |      - |    5792 B |        0.30 |
| Lissic_RenderUtf8                | Utf8       | 15.493 μs | 0.1390 μs | 0.1300 μs |  1.14 |    0.01 |  8.0261 | 1.3428 |   67280 B |        3.50 |
| Lissic_PrebuiltModel_RenderUtf8  | Utf8       | 10.749 μs | 0.0414 μs | 0.0367 μs |  0.79 |    0.01 |       - |      - |         - |        0.00 |

</details>
