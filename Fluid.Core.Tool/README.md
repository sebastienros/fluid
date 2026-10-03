# Fluid.Core.Tool

A native AOT command line tool (`liquid`) that renders [Liquid](https://shopify.github.io/liquid/) templates with [Fluid](https://github.com/sebastienros/fluid). It works from any shell (bash, zsh, PowerShell, cmd) and is suited to scripts and CI.

Native AOT builds are provided for Windows, Linux and macOS on x64 and arm64. Other platforms use a framework-dependent build that requires the .NET 10 runtime.

## Install

```shell
dotnet tool install -g Fluid.Core.Tool
# or run without installing
dnx Fluid.Core.Tool -- --help
```

## Usage

```text
liquid [<template>] [options]
```

Exactly one template source is required: a template file (positional argument or `--template`) or `--inline`.

### Arguments

| Argument | Description |
|----------|-------------|
| `<template>` | Path of the Liquid template file to render. |

### Options

| Option | Description |
|--------|-------------|
| `-t`, `--template <path>` | Path of the template file (same as the positional argument). |
| `-e`, `--inline <source>` | Liquid template source provided inline. |
| `-d`, `--data <path>` | JSON data file. Use `-` for stdin. When omitted, JSON is read from stdin if it is redirected. |
| `-o`, `--output <path>` | Write the result to a file instead of stdout. The file is only written if rendering succeeds. |
| `-s`, `--set <key=value>` | Set a string variable. Repeatable. Overrides values coming from the JSON data. |
| `-I`, `--include-path <dir>` | Directory used to resolve `include`, `render` and `from` templates. Repeatable, searched in order. Defaults to the template's directory (or the current directory with `--inline`). `.liquid` is appended when the file is not found as-is. Paths cannot escape the directory. |
| `--culture <name>` | Culture used to format numbers and dates, e.g. `fr-FR`. Default is invariant. |
| `--timezone <id>` | Time zone id used for dates, e.g. `UTC` or `Europe/Paris`. Default is the local time zone. |
| `--strict` | Fail on undefined variables and filters. |
| `--env` | Expose environment variables as the `env` object, e.g. `{{ env.HOME }}`. |
| `--validate` | Only parse the template and report errors; nothing is rendered. |
| `--max-steps <n>` | Maximum number of statements to execute. `0` (default) means unlimited. |
| `--max-recursion <n>` | Maximum `include`/`render` recursion depth. Default `100`. |
| `-?`, `-h`, `--help` | Show help. |
| `--version` | Show version information. |

### Data model

JSON read from stdin or `--data` is the model:

- The properties of a root object become top-level variables.
- Any other root value (array, string, number...) is exposed as the `data` variable.
- Empty input means an empty model.
- `--set` values and `--env` are added afterwards, so `--set` wins over JSON.

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success. |
| `1` | Template parse/render error, invalid JSON data, or missing file. The message is written to stderr. |
| `2` | Usage error (missing or conflicting template source, invalid `--set`, unknown culture or time zone). |

## Examples

```shell
# Inline template with JSON on stdin
echo '{"name":"World"}' | liquid -e "Hello {{ name | upcase }}!"

# Template file, data file and output file
liquid page.liquid --data model.json --output page.html

# Partials resolved from a directory
cat model.json | liquid page.liquid -I ./partials

# Variables from the command line and the environment
liquid -e "{{ user }} on {{ env.HOME }}" --set user=Bob --env

# Non-object JSON is available as `data`
echo '[1,2,3]' | liquid -e "{{ data | join: ', ' }}"

# Localized output
liquid -e "{{ 1234.5 }} {{ '2024-03-01' | date: '%B' }}" --culture fr-FR

# Check a template without rendering it
liquid page.liquid --validate

# Fail on typos in variable names
liquid page.liquid --data model.json --strict
```

PowerShell:

```powershell
Get-Content model.json | liquid page.liquid -I ./partials
'{"name":"World"}' | liquid -e 'Hello {{ name }}!'
Invoke-RestMethod https://example.com/api | ConvertTo-Json -Depth 10 | liquid report.liquid -o report.md
```

## Notes

- All the standard Liquid tags and filters of Fluid are available, including `json`.
- Only JSON-derived values are exposed to templates; reflection-based access to .NET objects is not available (this is what keeps the tool AOT-compatible).
- Templates are executed with Fluid's default options: no file access outside the include directories.

## Links

- Fluid documentation and source: https://github.com/sebastienros/fluid
- Liquid language reference: https://shopify.github.io/liquid/
