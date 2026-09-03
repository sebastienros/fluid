# Fluid fuzz-testing bug inventory

This inventory contains only deterministic, minimized failures confirmed by the fuzz campaign in
`FuzzCampaignTests`. Equivalent symptoms are grouped by root cause.

## Campaign configuration

| Area | CI/default seed | Default cases | Extended campaign |
| --- | ---: | ---: | --- |
| Parser | `1592594996` (`0x5EED1234`) | 256 generated plus the curated corpus | Set `FLUID_FUZZ_CASES` and `FLUID_FUZZ_SEED` |
| Built-in filters | `15826469` (`0x0F17E25`) | 32 generated values plus the curated value matrix | Set `FLUID_FILTER_FUZZ_CASES` and `FLUID_FILTER_FUZZ_SEED` |

The parser campaign runs all 16 combinations of `FluidParserOptions` through interpreted and
compiled parsers, compares acceptance/rejection and render outcomes, and includes malformed
constructs, delimiter and whitespace mutations, nested blocks, long literals, Unicode, control
characters, unterminated constructs, and unexpected token combinations.

The filter campaign enumerates all registered filters from the array, string, number, misc, color,
and money families. Every filter receives nil-like, boolean, numeric boundary, string, Unicode,
date, array, and dictionary inputs with missing, excess, named, invalid-format, and boundary
arguments. Each invocation is repeated with a fresh bounded context to detect instability.

## Verified findings

### FF-001: Array slices truncate too aggressively near the end

- **Category/severity:** Filter correctness / medium
- **Minimized input:** Input `["a", "b", "c", "d", "e"]`, filter `slice`, arguments `3, 4`
- **Setup:** `StringFilters.Slice(input, new FilterArguments(3, 4), new TemplateContext())`
- **Expected invariant:** A slice is clamped to the available suffix and returns `["d", "e"]`.
- **Actual behavior:** Returned `["d"]`. The remaining length was calculated as
  `requestedLength - startIndex` rather than `sourceLength - startIndex`.
- **Reproduction:** `dotnet test --filter "FullyQualifiedName~SliceArrayClampsLengthAtTheEnd"`
- **Status:** Fixed
- **Regression/fix:** `StringFiltersTests.SliceArrayClampsLengthAtTheEnd`;
  `Fluid/Filters/StringFilters.cs`

### FF-002: Minimum integer slice offset throws

- **Category/severity:** Unexpected exception / medium
- **Minimized input:** Input `"abc"`, filter `slice`, argument `-2147483648`
- **Setup:** `StringFilters.Slice(new StringValue("abc"), new FilterArguments(NumberValue.Create(int.MinValue)), context)`
- **Expected invariant:** An offset before the beginning returns the normal empty slice value.
- **Actual behavior:** `OverflowException: Negating the minimum value of a twos complement number is invalid.`
  The exception originated in `Math.Abs(requestedStartIndex)` in `StringFilters.Slice`.
- **Reproduction:** `dotnet test --filter "FullyQualifiedName~SliceWithMinimumOffsetReturnsEmpty"`
- **Status:** Fixed
- **Regression/fix:** `StringFiltersTests.SliceWithMinimumOffsetReturnsEmpty`;
  `Fluid/Filters/StringFilters.cs`

### FF-003: Large handleize input exhausts the process stack

- **Category/severity:** Availability / high
- **Minimized input:** A `StringValue` containing 5,000,000 ASCII `a` characters, filter `handleize`,
  no arguments
- **Setup:** `MiscFilters.Handleize(input, FilterArguments.Empty, new TemplateContext())`
- **Expected invariant:** Large input is handled with bounded stack use and returns a same-length
  lowercase handle.
- **Actual behavior:** The test process terminated with exit code 134 and `Stack overflow`. The top
  stack frame was `Fluid.Filters.MiscFilters.Handleize`; the method used
  `stackalloc char[Math.Max(512, value.Length * 2)]`.
- **Reproduction:** `dotnet test --filter "FullyQualifiedName~HandleizeSupportsLargeInputWithoutUsingInputSizedStackSpace"`
- **Status:** Fixed
- **Regression/fix:** `MiscFiltersTests.HandleizeSupportsLargeInputWithoutUsingInputSizedStackSpace`;
  `Fluid/Filters/MiscFilters.cs`

### FF-004: format_string with no arguments crashes on empty FilterArguments

- **Category/severity:** Filter correctness / medium
- **Minimized input:** Input `"literal text"`, filter `format_string`, no arguments
- **Setup:** `MiscFilters.FormatString(new StringValue("literal text"), FilterArguments.Empty, new TemplateContext())`
- **Expected invariant:** Empty positional arguments behave like `Array.Empty<object>()`, allowing
  literal format strings to round-trip without a NullReferenceException.
- **Actual behavior:** A `NullReferenceException` was thrown from `FilterArguments.ValuesToObjectArray()`
  because `_positional` was null when the filter was invoked without arguments.
- **Reproduction:** `./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -method "Fluid.Tests.MiscFiltersTests.FormatStringWithoutArgumentsDoesNotThrow"`
- **Status:** Fixed
- **Regression/fix:** `MiscFiltersTests.FormatStringWithoutArgumentsDoesNotThrow`;
  `Fluid/FilterArguments.cs`, `Fluid/FunctionArguments.cs`

No additional parser differential, instability, or built-in filter runtime failures were found in
the extended campaign of 108,768 parses and 744,930 filter invocations with the seeds above.

## Reproduction

```shell
dotnet test --filter "FullyQualifiedName~Fluid.Tests.Fuzzing"
dotnet test --property:Compiled=true --filter "FullyQualifiedName~Fluid.Tests.Fuzzing"

FLUID_FUZZ_CASES=5000 \
FLUID_FILTER_FUZZ_CASES=256 \
dotnet test --filter "FullyQualifiedName~Fluid.Tests.Fuzzing"
```
