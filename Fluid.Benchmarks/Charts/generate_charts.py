"""Regenerates the README benchmark charts (Assets/benchmark-*.svg and .png).

Run the comparison benchmarks first, from the repository root:

    dotnet run -c Release --project Fluid.Benchmarks -- --filter '*ComparisonBenchmarks*'
    python3 Fluid.Benchmarks/Charts/generate_charts.py

The static engine charts (Assets/benchmark-static-*) come from another report:

    dotnet run -c Release --project Fluid.Benchmarks -- --filter '*StaticRenderingBenchmarks*'

The charts of a report that wasn't generated are left as they are.

The SVGs are filled from benchmark-chart.svg.template. The PNGs are rendered with a
headless Chrome or Edge (pass --browser to point at one, or --no-png to skip them).
Existing axis ranges and legends are kept while the new values still fit, so that
successive updates only change the bars.
"""
import argparse
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RESULTS = os.path.join(ROOT, "BenchmarkDotNet.Artifacts", "results")
REPORT = os.path.join(RESULTS, "Fluid.Benchmarks.ComparisonBenchmarks-report-github.md")
STATIC_REPORT = os.path.join(RESULTS, "Fluid.Benchmarks.StaticRendering.StaticRenderingBenchmarks-report-github.md")
ASSETS = os.path.join(ROOT, "Assets")

WIDTH, HEIGHT = 1051, 400
TOP, BOTTOM = 84, 342
PLOT_HEIGHT = BOTTOM - TOP

COLORS = {"Fluid": "#1f77b4", "Scriban": "#ff7f0e", "DotLiquid": "#2ca02c", "Handlebars": "#d62728"}

CHARTS = [
    {
        "file": "benchmark-parse",
        "category": "Parse",
        "title": "Parse benchmark comparison",
        "desc": "Duration and memory allocation for {engines}. Smaller values are better. Fluid has the smallest values.",
        "log": True,
    },
    {
        "file": "benchmark-parse-big",
        "category": "ParseBig",
        "title": "Large template parse benchmark comparison",
        "desc": "Duration and memory allocation for parsing a blog post template with {engines}. Smaller values are better. Fluid has the smallest values.",
        "log": True,
    },
    {
        "file": "benchmark-render",
        "category": "Render",
        "title": "Render benchmark comparison",
        "desc": "Duration and memory allocation for {engines} rendering. Smaller values are better. Fluid has the smallest values.",
        "log": False,
    },
]

# Compiled templates and other static engines, against a parsed Fluid template. Each series is a benchmark method,
# its label, its color and whether it is highlighted like Fluid.
STATIC_CHARTS = [
    {
        "file": "benchmark-static-render",
        "title": "Compiled templates and static engines render benchmark comparison",
        "desc": "Duration and memory allocation for {engines} rendering the product template to a string. Smaller values are better.",
        "series": [
            ("Fluid_Render", "Fluid", "#1f77b4", True),
            ("Fluid_SourceGenerated_Render", "Fluid compiled", "#17becf", True),
            ("RazorSlices_Render", "RazorSlices", "#9467bd", False),
            ("Lissic_Render", "Lissic", "#8c564b", False),
            ("HandWritten_Render", "Hand-written C#", "#7f7f7f", False),
        ],
    },
    {
        "file": "benchmark-static-render-utf8",
        "title": "Compiled templates and static engines UTF-8 render benchmark comparison",
        "desc": "Duration and memory allocation for {engines} rendering the product template to a UTF-8 buffer. Smaller values are better.",
        "series": [
            ("Fluid_RenderUtf8", "Fluid", "#1f77b4", True),
            ("Fluid_SourceGenerated_RenderUtf8", "Fluid compiled", "#17becf", True),
            ("RazorSlices_RenderUtf8", "RazorSlices", "#9467bd", False),
            ("Lissic_RenderUtf8", "Lissic", "#8c564b", False),
            ("Lissic_PrebuiltModel_RenderUtf8", "Lissic, prebuilt model", "#c49c94", False),
        ],
    },
]


def fmt(v):
    return f"{round(v, 1):g}"


def parse_report(path):
    """Returns {method: (microseconds, kilobytes)} from a BenchmarkDotNet GitHub markdown report."""
    time_units = {"ns": 0.001, "us": 1, "μs": 1, "ms": 1000, "s": 1_000_000}
    size_units = {"B": 1 / 1024, "KB": 1, "MB": 1024}
    rows = {}
    mean_column = alloc_column = None
    with open(path, encoding="utf-8") as f:
        for line in f:
            cells = [c.strip().replace("**", "") for c in line.strip().strip("|").split("|")]
            if cells[0] == "Method":
                mean_column, alloc_column = cells.index("Mean"), cells.index("Allocated")
                continue
            if mean_column is None or not re.match(r"^\w+_\w+$", cells[0]):
                continue
            mean, mean_unit = cells[mean_column].split()
            # Nothing allocated is reported as "-".
            alloc, alloc_unit = cells[alloc_column].split() if cells[alloc_column] != "-" else ("0", "B")
            rows[cells[0]] = (
                float(mean.replace(",", "")) * time_units[mean_unit],
                float(alloc.replace(",", "")) * size_units[alloc_unit],
            )
    return rows


def axis(x0, values, log, old_top):
    """Returns the grid lines and a function mapping a value to a bar height."""
    peak = max(values)
    if log:
        top_exp = math.ceil(math.log2(peak * 1.5))
        if old_top and peak * 1.15 <= old_top:
            top_exp = round(math.log2(old_top))
        ticks = [2 ** i for i in range(top_exp + 1)]
        height = lambda v: PLOT_HEIGHT * max(math.log2(v), 0) / top_exp
    else:
        top = math.ceil(peak * 1.03 / 100) * 100
        if old_top and peak * 1.03 <= old_top:
            top = old_top
        ticks = [0] + [top * i / 5 for i in range(1, 6)]
        height = lambda v: PLOT_HEIGHT * v / top

    lines = []
    for t in ticks:
        y = BOTTOM - height(t) if t > 0 else BOTTOM
        lines.append(f'    <line class="grid" x1="{x0}" y1="{fmt(y)}" x2="{x0 + 438}" y2="{fmt(y)}"/>'
                     f'<text class="tick" x="{x0 - 10}" y="{fmt(y + 4)}" text-anchor="end">{t:g}</text>')
    lines.append(f'    <line class="axis" x1="{x0}" y1="{TOP}" x2="{x0}" y2="{BOTTOM}"/>'
                 f'<line class="axis" x1="{x0}" y1="{BOTTOM}" x2="{x0 + 438}" y2="{BOTTOM}"/>')
    return lines, height


def panel(x0, entries, log, title, decimals, old_top):
    """Draws the bars of entries, a list of (value, color, highlighted)."""
    lines, height = axis(x0, [v for v, _, _ in entries], log, old_top)
    lines.append("")
    # Four bars are 72 wide every 105 units. More bars keep the margins and the proportions.
    spacing = 105 if len(entries) <= 4 else round(387 / (len(entries) - 1 + 72 / 105), 1)
    width = 72 if len(entries) <= 4 else round(spacing * 72 / 105, 1)
    for i, (value, color, highlighted) in enumerate(entries):
        x = round(x0 + 26 + i * spacing, 1)
        h = height(value)
        y = BOTTOM - h
        if highlighted:
            lines.append(f'    <rect x="{fmt(x)}" y="{fmt(y)}" width="{fmt(width)}" height="{fmt(h)}" rx="1" fill="{color}" stroke="#0b4f79" stroke-width="2.5"/>')
            css = "fluid-value"
        else:
            lines.append(f'    <rect class="competitor" x="{fmt(x)}" y="{fmt(y)}" width="{fmt(width)}" height="{fmt(h)}" rx="1" fill="{color}"/>')
            css = "value"
        lines.append(f'    <text class="{css}" x="{fmt(x + width / 2)}" y="{round(y - 7)}">{value:,.{decimals}f}</text>')
    scale = " · log₂ scale" if log else ""
    lines.append(f'    <text class="axis-title" x="{x0 + 219}" y="381" text-anchor="middle">{title}{scale}</text>')
    return "\n".join(lines)


def legend(items, old_svg):
    """Draws the legend of items, a list of (name, color, highlighted)."""
    names = [name for name, _, _ in items]

    # Keep the previous legend when the series and their order did not change.
    old = re.search(r'<g font-size="12">\n(.*?)\n  </g>', old_svg, re.S)
    if old and re.findall(r">([^<]+)</text>", old.group(1)) == names:
        return old.group(1)

    # Approximate Arial 12px text widths to center the legend.
    widths = [len(n) * 6.8 + (6 if highlighted else 0) for n, _, highlighted in items]
    gap = 95
    x = (WIDTH - sum(widths) - 11 * len(names) - gap * (len(names) - 1)) / 2
    lines = []
    for (name, color, highlighted), w in zip(items, widths):
        cx = round(x + 5)
        if highlighted:
            lines.append(f'    <circle cx="{cx}" cy="23" r="5" fill="{color}" stroke="#0b4f79" stroke-width="2"/>')
            lines.append(f'    <text x="{cx + 11}" y="27" font-weight="700">{name}</text>')
        else:
            lines.append(f'    <circle cx="{cx}" cy="23" r="5" fill="{color}" opacity=".52"/>')
            lines.append(f'    <text x="{cx + 11}" y="27">{name}</text>')
        x += 11 + w + gap
    return "\n".join(lines)


def old_axis_tops(old_svg):
    tops = []
    for x in ("52", "578"):
        ticks = re.findall(r'class="tick" x="' + x + r'" y="[\d.]+" text-anchor="end">([\d.]+)<', old_svg)
        tops.append(max(map(float, ticks)) if ticks else None)
    return tops


def comparison_series(rows, chart):
    """The series of a comparison chart: one per engine with a benchmark in the chart's category."""
    category = chart["category"]
    return [(f"{e}_{category}", e, COLORS[e], e == "Fluid") for e in COLORS if f"{e}_{category}" in rows]


def render_chart(template, rows, chart, series, old_svg):
    """Renders the series, a list of (method, label, color, highlighted), in the chart's template."""
    # Fluid first, then the other engines from fastest to slowest.
    series = [s for s in series if s[3]] + sorted((s for s in series if not s[3]), key=lambda s: rows[s[0]][0])
    labels = [label for _, label, _, _ in series]
    duration_top, memory_top = old_axis_tops(old_svg)

    # The legend of the comparison charts names the benchmark methods.
    legend_items = [(method if "category" in chart else label, color, h) for method, label, color, h in series]
    durations = [(rows[method][0], color, h) for method, _, color, h in series]
    allocations = [(rows[method][1], color, h) for method, _, color, h in series]

    values = {
        "title": chart["title"],
        "desc": chart["desc"].format(engines=", ".join(labels[:-1]) + ", and " + labels[-1]),
        "legend": legend(legend_items, old_svg),
        "duration": panel(62, durations, chart.get("log", False), "Duration (Microseconds)", 3, duration_top),
        "memory": panel(588, allocations, chart.get("log", False), "Memory Allocation (Kilobytes)", 2, memory_top),
    }
    return re.sub(r"\{\{(\w+)\}\}", lambda m: values[m.group(1)], template)


def find_browser():
    candidates = [
        shutil.which("google-chrome"), shutil.which("chromium"), shutil.which("microsoft-edge"), shutil.which("msedge"),
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    ]
    return next((c for c in candidates if c and os.path.exists(c)), None)


def render_png(browser, svg_path, png_path):
    with tempfile.TemporaryDirectory() as tmp:
        html = os.path.join(tmp, "chart.html")
        with open(svg_path, encoding="utf-8") as f, open(html, "w", encoding="utf-8") as out:
            out.write(f'<!doctype html><html><body style="margin:0;overflow:hidden">{f.read()}</body></html>')
        subprocess.run([
            browser, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
            f"--user-data-dir={os.path.join(tmp, 'profile')}", f"--window-size={WIDTH},{HEIGHT}",
            f"--screenshot={png_path}", "file://" + html.replace(os.sep, "/"),
        ], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--report", default=REPORT, help="BenchmarkDotNet GitHub markdown report of the comparison benchmarks")
    parser.add_argument("--static-report", default=STATIC_REPORT, help="BenchmarkDotNet GitHub markdown report of the static rendering benchmarks")
    parser.add_argument("--output", default=ASSETS, help="directory receiving the charts")
    parser.add_argument("--browser", help="Chrome or Edge executable used to render the PNGs")
    parser.add_argument("--no-png", action="store_true", help="only write the SVGs")
    args = parser.parse_args()

    with open(os.path.join(HERE, "benchmark-chart.svg.template"), encoding="utf-8") as f:
        template = f.read()

    charts = []
    for report, report_charts, get_series in [
        (args.report, CHARTS, comparison_series),
        (args.static_report, STATIC_CHARTS, lambda rows, chart: chart["series"]),
    ]:
        if not os.path.exists(report):
            print(f"Skipped the charts of {report}, which doesn't exist")
            continue
        rows = parse_report(report)
        charts += [(chart, get_series(rows, chart), rows) for chart in report_charts]
        for name, (us, kb) in rows.items():
            print(f"  {name:35} {us:12.3f} us {kb:10.2f} KB")

    if not charts:
        sys.exit("No benchmark report found: run the benchmarks first.")

    browser = None
    if not args.no_png:
        browser = args.browser or find_browser()
        if not browser:
            sys.exit("No Chrome or Edge found to render the PNGs: pass --browser or --no-png.")

    for chart, series, rows in charts:
        svg_path = os.path.join(args.output, chart["file"] + ".svg")
        old_svg = open(svg_path, encoding="utf-8").read() if os.path.exists(svg_path) else ""
        with open(svg_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(render_chart(template, rows, chart, series, old_svg))
        if browser:
            render_png(browser, svg_path, os.path.join(args.output, chart["file"] + ".png"))
        print(f"Wrote {svg_path}" + (" and .png" if browser else ""))


if __name__ == "__main__":
    main()
