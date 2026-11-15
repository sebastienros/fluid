#!/usr/bin/env bash
# Renders a Liquid template with the Liquid gem (https://github.com/Shopify/liquid) in Docker, and optionally
# compares the result with Fluid or with an expected output. liquid-gem.ps1 is the same for PowerShell.
# Run `scripts/liquid-gem/liquid-gem.sh --help` for usage.

set -euo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

usage() {
    cat <<'EOF'
Renders a Liquid template with the Liquid gem, the reference implementation, in Docker.

Usage:
  scripts/liquid-gem/liquid-gem.sh [<template-file>] [options]

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
EOF
}

fail() {
    echo "$1" >&2
    exit 3
}

# Reads a file, or stdin without one, into a variable, exactly: a command substitution would drop trailing newlines.
read_into() {
    local content
    content="$(cat "${@:2}"; printf x)"
    printf -v "$1" '%s' "${content%x}"
}

liquid_version="5.14.0"
template_file=""
has_template_file=0
inline=""
has_inline=0
data=""
tz="UTC"
error_mode="strict"
compare=0
expected=""
has_expected=0
json=0

# Fails when the option has no value: called with all the remaining arguments.
next() {
    [ $# -ge 2 ] || fail "Missing value for $1"
}

while [ $# -gt 0 ]; do
    arg="$1"
    case "$arg" in
        -h|--help) usage; exit 0 ;;
        -e|--inline) next "$@"; inline="$2"; has_inline=1; shift ;;
        -d|--data) next "$@"; [ -f "$2" ] || fail "Data file not found: $2"; read_into data "$2"; shift ;;
        -j|--data-json) next "$@"; data="$2"; shift ;;
        -z|--tz) next "$@"; tz="$2"; shift ;;
        -m|--error-mode) next "$@"; error_mode="$2"; shift ;;
        -c|--compare) compare=1 ;;
        -x|--expected) next "$@"; expected="$2"; has_expected=1; shift ;;
        -f|--expected-file) next "$@"; [ -f "$2" ] || fail "Expected file not found: $2"; read_into expected "$2"; has_expected=1; shift ;;
        --json) json=1 ;;
        --liquid-version) next "$@"; liquid_version="$2"; shift ;;
        -) [ $has_template_file -eq 0 ] || fail "Only one template file can be provided"; template_file="-"; has_template_file=1 ;;
        -*) fail "Unknown option $arg" ;;
        *) [ $has_template_file -eq 0 ] || fail "Only one template file can be provided"; template_file="$arg"; has_template_file=1 ;;
    esac
    shift
done

[ $has_template_file -eq 0 ] || [ $has_inline -eq 0 ] || fail "Provide either a template file or --inline, not both"
[ $compare -eq 0 ] || [ $has_expected -eq 0 ] || fail "--compare cannot be combined with --expected"

if [ $has_inline -eq 1 ]; then
    template="$inline"
elif [ "$template_file" = "-" ] || { [ $has_template_file -eq 0 ] && [ ! -t 0 ]; }; then
    read_into template
elif [ $has_template_file -eq 1 ]; then
    [ -f "$template_file" ] || fail "Template file not found: $template_file"
    read_into template "$template_file"
else
    fail "No template provided. Use --help for usage."
fi

command -v docker >/dev/null 2>&1 || fail "Docker is required: https://docs.docker.com/get-docker/"

image="fluid-liquid-gem:$liquid_version"
docker build --quiet --tag "$image" --build-arg "LIQUID_VERSION=$liquid_version" "$dir" >/dev/null \
    || fail "Building the $image image failed"

export LIQUID_TEMPLATE="$template" LIQUID_DATA="$data" LIQUID_ERROR_MODE="$error_mode" LIQUID_TZ="$tz"

run_gem() {
    docker run --rm -e LIQUID_TEMPLATE -e LIQUID_DATA -e LIQUID_ERROR_MODE -e LIQUID_FORMAT -e "TZ=$tz" "$image"
}

if [ $compare -eq 0 ] && [ $has_expected -eq 0 ]; then
    if [ $json -eq 1 ]; then export LIQUID_FORMAT=json; else export LIQUID_FORMAT=raw; fi
    set +e
    run_gem
    code=$?
    set -e
    # Docker's own failures (125 to 127) are usage errors.
    [ $code -lt 125 ] || exit 3
    exit $code
fi

result="$(mktemp)"
trap 'rm -f "$result"' EXIT

export LIQUID_FORMAT=json
set +e
run_gem > "$result"
code=$?
set -e
[ $code -lt 125 ] || exit 3
[ $json -eq 0 ] || cat "$result" >&2

if [ $has_expected -eq 1 ]; then export LIQUID_EXPECTED="$expected"; else unset LIQUID_EXPECTED; fi

command -v dotnet >/dev/null 2>&1 || fail "The .NET SDK is required for --compare and --expected"
dotnet build "$dir/compare.cs" --verbosity quiet --nologo >&2 || fail "Building compare.cs failed"
set +e
dotnet run --no-build "$dir/compare.cs" -- "$result"
code=$?
set -e
exit $code
