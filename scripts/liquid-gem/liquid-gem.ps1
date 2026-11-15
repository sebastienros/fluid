#!/usr/bin/env pwsh
# Renders a Liquid template with the Liquid gem (https://github.com/Shopify/liquid) in Docker, and optionally
# compares the result with Fluid or with an expected output. liquid-gem.sh is the same for bash.
# Run `scripts/liquid-gem/liquid-gem.ps1 --help` for usage.
#
# There is no param() block so that the options are the same as liquid-gem.sh: -e, --tz, ...

$ErrorActionPreference = 'Stop'
$pipelineInput = @($input)
$arguments = @($args)

$usage = @'
Renders a Liquid template with the Liquid gem, the reference implementation, in Docker.

Usage:
  scripts/liquid-gem/liquid-gem.ps1 [<template-file>] [options]

Template source (exactly one; stdin is used when none is given and input is redirected):
  <template-file>             Path of a Liquid template file. Use - for stdin.
  -e, --inline <source>       Liquid source provided inline.

Options:
  -d, --data <file>           JSON file with the variables of the template.
  -j, --data-json <json>      The variables of the template, as inline JSON.
                              {"$time": "2026-03-04T13:00:00"} is a Ruby Time (a .NET DateTime) in the time zone,
                              or with its offset ("...-05:00", "...Z" for Time.utc), {"$date": "2026-03-04"} a Date
                              (a DateOnly).
  -z, --tz <zone>             Time zone, like America/New_York: TZ for Ruby, TemplateContext.TimeZone for Fluid.
                              Default: UTC
  -m, --error-mode <mode>     The gem's error mode: lax, warn or strict. Default: strict
  -c, --compare               Also render with Fluid (this repository) and diff the two outputs.
                              Fluid parses with AllowLiquidTag, like the gem.
  -x, --expected <text>       Compare the gem's output with this text.
  -f, --expected-file <file>  Compare the gem's output with the content of this file.
      --json                  Print the gem's result as JSON: output, error, errorType, liquid and ruby versions.
      --liquid-version <v>    Version of the Liquid gem. Default: 5.14.0
  -h, --help                  Show this help.

Output: the gem's rendered output is written to stdout as-is, errors to stderr.

Exit codes:
  0  rendered (and matched, when comparing)
  1  outputs differ
  2  the gem reported a syntax error
  3  usage or Docker error
  4  the gem reported a render error

Notes:
  Needs Docker. The image (fluid-liquid-gem:<version>) is built on the first run, from scripts/liquid-gem.
  --compare and --expected need the .NET SDK. Templates can't include partials.
'@

function Fail([string] $message) {
    [Console]::Error.WriteLine($message)
    exit 3
}

function Read-File([string] $path, [string] $kind) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail "$kind file not found: $path" }
    # Exactly, trailing newlines included.
    return [IO.File]::ReadAllText((Resolve-Path -LiteralPath $path).ProviderPath)
}

function Read-Stdin {
    if ($pipelineInput.Count -gt 0) { return ($pipelineInput -join "`n") }
    return [Console]::In.ReadToEnd()
}

$liquidVersion = '5.14.0'
$templateFile = $null
$inline = $null
$data = ''
$tz = 'UTC'
$errorMode = 'strict'
$compare = $false
$expected = $null
$json = $false

# The value of the current option.
function Next {
    if ($script:i + 1 -ge $script:arguments.Count) { Fail "Missing value for $($script:arguments[$script:i])" }
    $script:i++
    return [string] $script:arguments[$script:i]
}

for ($i = 0; $i -lt $arguments.Count; $i++) {
    $arg = [string] $arguments[$i]

    switch -CaseSensitive ($arg) {
        { $_ -in '-h', '--help' } { [Console]::Out.Write($usage); exit 0 }
        { $_ -in '-e', '--inline' } { $inline = Next; break }
        { $_ -in '-d', '--data' } { $data = Read-File (Next) 'Data'; break }
        { $_ -in '-j', '--data-json' } { $data = Next; break }
        { $_ -in '-z', '--tz' } { $tz = Next; break }
        { $_ -in '-m', '--error-mode' } { $errorMode = Next; break }
        { $_ -in '-c', '--compare' } { $compare = $true; break }
        { $_ -in '-x', '--expected' } { $expected = Next; break }
        { $_ -in '-f', '--expected-file' } { $expected = Read-File (Next) 'Expected'; break }
        '--json' { $json = $true; break }
        '--liquid-version' { $liquidVersion = Next; break }
        default {
            if ($arg.StartsWith('-') -and $arg -ne '-') { Fail "Unknown option $arg" }
            if ($null -ne $templateFile) { Fail 'Only one template file can be provided' }
            $templateFile = $arg
        }
    }
}

if ($null -ne $templateFile -and $null -ne $inline) { Fail 'Provide either a template file or --inline, not both' }
if ($compare -and $null -ne $expected) { Fail '--compare cannot be combined with --expected' }

if ($null -ne $inline) {
    $template = $inline
}
elseif ($templateFile -eq '-' -or ($null -eq $templateFile -and ($pipelineInput.Count -gt 0 -or [Console]::IsInputRedirected))) {
    $template = Read-Stdin
}
elseif ($null -ne $templateFile) {
    $template = Read-File $templateFile 'Template'
}
else {
    Fail 'No template provided. Use --help for usage.'
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Fail 'Docker is required: https://docs.docker.com/get-docker/' }

$image = "fluid-liquid-gem:$liquidVersion"
& docker build --quiet --tag $image --build-arg "LIQUID_VERSION=$liquidVersion" $PSScriptRoot | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "Building the $image image failed" }

$variables = 'LIQUID_TEMPLATE', 'LIQUID_DATA', 'LIQUID_ERROR_MODE', 'LIQUID_FORMAT', 'LIQUID_TZ', 'LIQUID_EXPECTED'
$result = $null

try {
    # The environment of the process is used to pass the template and the data to Docker, without any quoting.
    $env:LIQUID_TEMPLATE = $template
    $env:LIQUID_DATA = $data
    $env:LIQUID_ERROR_MODE = $errorMode
    $env:LIQUID_TZ = $tz
    $dockerArgs = 'run', '--rm', '-e', 'LIQUID_TEMPLATE', '-e', 'LIQUID_DATA', '-e', 'LIQUID_ERROR_MODE', '-e', 'LIQUID_FORMAT', '-e', "TZ=$tz", $image

    if (-not $compare -and $null -eq $expected) {
        $env:LIQUID_FORMAT = if ($json) { 'json' } else { 'raw' }
        & docker @dockerArgs
        # Docker's own failures (125 to 127) are usage errors.
        exit $(if ($LASTEXITCODE -ge 125) { 3 } else { $LASTEXITCODE })
    }

    $env:LIQUID_FORMAT = 'json'
    $gemJson = (& docker @dockerArgs) -join "`n"
    if ($LASTEXITCODE -ge 125) { exit 3 }
    if ($json) { [Console]::Error.WriteLine($gemJson) }

    $result = [IO.Path]::GetTempFileName()
    [IO.File]::WriteAllText($result, $gemJson)

    if ($null -ne $expected) { $env:LIQUID_EXPECTED = $expected } else { Remove-Item Env:LIQUID_EXPECTED -ErrorAction SilentlyContinue }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail 'The .NET SDK is required for --compare and --expected' }
    $compareScript = Join-Path $PSScriptRoot 'compare.cs'
    & dotnet build $compareScript --verbosity quiet --nologo | ForEach-Object { [Console]::Error.WriteLine($_) }
    if ($LASTEXITCODE -ne 0) { Fail 'Building compare.cs failed' }
    & dotnet run --no-build $compareScript -- $result
    exit $LASTEXITCODE
}
finally {
    foreach ($name in $variables) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue }
    if ($null -ne $result) { Remove-Item -LiteralPath $result -ErrorAction SilentlyContinue }
}
