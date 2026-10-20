"""Regenerates the README benchmark charts (Assets/benchmark-*.svg and .png).

Run the comparison benchmarks first, from the repository root:

    dotnet run -c Release --project Fluid.Benchmarks -- --filter '*ComparisonBenchmarks*'
    python3 Fluid.Benchmarks/Charts/generate_charts.py

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
REPORT = os.path.join(ROOT, "BenchmarkDotNet.Artifacts", "results", "Fluid.Benchmarks.ComparisonBenchmarks-report-github.md")
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


def fmt(v):
    return f"{round(v, 1):g}"


def parse_report(path):
    """Returns {method: (microseconds, kilobytes)} from a BenchmarkDotNet GitHub markdown report."""
    time_units = {"ns": 0.001, "us": 1, "μs": 1, "ms": 1000, "s": 1_000_000}
    size_units = {"B": 1 / 1024, "KB": 1, "MB": 1024}
    rows = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            cells = [c.strip().replace("**", "") for c in line.strip().strip("|").split("|")]
            if len(cells) < 10 or not re.match(r"^\w+_\w+$", cells[0]):
                continue
            mean, mean_unit = cells[1].split()
            alloc, alloc_unit = cells[-2].split()
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
    lines, height = axis(x0, [v for _, v in entries], log, old_top)
    lines.append("")
    for i, (engine, value) in enumerate(entries):
        x = x0 + 26 + i * 105
        h = height(value)
        y = BOTTOM - h
        if engine == "Fluid":
            lines.append(f'    <rect x="{x}" y="{fmt(y)}" width="72" height="{fmt(h)}" rx="1" fill="{COLORS[engine]}" stroke="#0b4f79" stroke-width="2.5"/>')
            css = "fluid-value"
        else:
            lines.append(f'    <rect class="competitor" x="{x}" y="{fmt(y)}" width="72" height="{fmt(h)}" rx="1" fill="{COLORS[engine]}"/>')
            css = "value"
        lines.append(f'    <text class="{css}" x="{x + 36}" y="{round(y - 7)}">{value:,.{decimals}f}</text>')
    scale = " · log₂ scale" if log else ""
    lines.append(f'    <text class="axis-title" x="{x0 + 219}" y="381" text-anchor="middle">{title}{scale}</text>')
    return "\n".join(lines)


def legend(names, old_svg):
    # Keep the previous legend when the series and their order did not change.
    old = re.search(r'<g font-size="12">\n(.*?)\n  </g>', old_svg, re.S)
    if old and re.findall(r">(\w+)</text>", old.group(1)) == names:
        return old.group(1)

    # Approximate Arial 12px text widths to center the legend.
    widths = [len(n) * 6.8 + (6 if i == 0 else 0) for i, n in enumerate(names)]
    gap = 95
    x = (WIDTH - sum(widths) - 11 * len(names) - gap * (len(names) - 1)) / 2
    lines = []
    for i, (name, w) in enumerate(zip(names, widths)):
        color = COLORS[name.split("_")[0]]
        cx = round(x + 5)
        if i == 0:
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


def render_chart(template, rows, chart, old_svg):
    category = chart["category"]
    entries = [(e, rows[f"{e}_{category}"]) for e in COLORS if f"{e}_{category}" in rows]
    # Fluid first, then the other engines from fastest to slowest.
    entries = [entries[0]] + sorted(entries[1:], key=lambda e: e[1][0])
    engines = [e for e, _ in entries]
    duration_top, memory_top = old_axis_tops(old_svg)

    values = {
        "title": chart["title"],
        "desc": chart["desc"].format(engines=", ".join(engines[:-1]) + ", and " + engines[-1]),
        "legend": legend([f"{e}_{category}" for e in engines], old_svg),
        "duration": panel(62, [(e, v[0]) for e, v in entries], chart["log"], "Duration (Microseconds)", 3, duration_top),
        "memory": panel(588, [(e, v[1]) for e, v in entries], chart["log"], "Memory Allocation (Kilobytes)", 2, memory_top),
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
    parser.add_argument("--report", default=REPORT, help="BenchmarkDotNet GitHub markdown report")
    parser.add_argument("--output", default=ASSETS, help="directory receiving the charts")
    parser.add_argument("--browser", help="Chrome or Edge executable used to render the PNGs")
    parser.add_argument("--no-png", action="store_true", help="only write the SVGs")
    args = parser.parse_args()

    rows = parse_report(args.report)
    with open(os.path.join(HERE, "benchmark-chart.svg.template"), encoding="utf-8") as f:
        template = f.read()

    browser = None
    if not args.no_png:
        browser = args.browser or find_browser()
        if not browser:
            sys.exit("No Chrome or Edge found to render the PNGs: pass --browser or --no-png.")

    for chart in CHARTS:
        svg_path = os.path.join(args.output, chart["file"] + ".svg")
        old_svg = open(svg_path, encoding="utf-8").read() if os.path.exists(svg_path) else ""
        with open(svg_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(render_chart(template, rows, chart, old_svg))
        if browser:
            render_png(browser, svg_path, os.path.join(args.output, chart["file"] + ".png"))
        print(f"Wrote {svg_path}" + (" and .png" if browser else ""))

    for name, (us, kb) in rows.items():
        print(f"  {name:30} {us:12.3f} us {kb:10.2f} KB")


if __name__ == "__main__":
    main()
