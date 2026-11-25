import math
import re
import statistics
import sys
from pathlib import Path

root = Path(sys.argv[1])
base, head = sys.argv[2:4]
units = {"ns": 1, "us": 1000, "μs": 1000, "µs": 1000, "ms": 1e6, "s": 1e9}

def read(name):
    result = {}
    for file in sorted((root / name).glob("*-report-github.md")):
        content = file.read_text()
        if "Benchmarks with issues:" in content:
            raise SystemExit(f"Invalid benchmark report: {file}")
        label = file.name.removesuffix("-report-github.md").split(".")[-1]
        headers = None
        for line in content.splitlines():
            if not line.strip().startswith("|"):
                continue
            cells = [x.strip().replace("**", "").replace("\x60", "") for x in line.strip().strip("|").split("|")]
            if {"Method", "Count", "Mean"}.issubset(cells):
                headers = cells
                continue
            if headers is None or len(cells) != len(headers):
                continue
            row = dict(zip(headers, cells))
            method = row["Method"]
            if method not in ("DefaultListSort", "SortzSort"):
                continue
            mean = re.fullmatch(r"([\d,.]+)\s*(ns|us|μs|µs|ms|s)", row["Mean"])
            if not mean:
                raise SystemExit(f"Invalid mean: {file}: {row}")
            key = (label, row.get("Ordinal", ""), int(row["Count"].replace(",", "")), method)
            if key in result:
                raise SystemExit(f"Duplicate benchmark case: {key}")
            result[key] = float(mean[1].replace(",", "")) * units[mean[2]]
    if len(result) != 126:
        raise SystemExit(f"{name}: expected 126 measurements, found {len(result)}")
    return result

a, b, c, d = (read(name) for name in ("main-1", "pr-1", "pr-2", "main-2"))
if not a.keys() == b.keys() == c.keys() == d.keys():
    raise SystemExit("Benchmark cases differ between revisions")

print(f"## Paired comparison: {base[:7]} vs {head[:7]}\n")
print("Same harness, same logical CPU, sequential ABBA order; means in ns.")
print("Negative change is faster. Unstable = repeat spread exceeds 5% or "
      "List.Sort control drift exceeds 5%. Unstable cases are excluded from aggregate.\n")
print("| Benchmark | Count | Main mean | PR mean | Change | Main spread | PR spread | Control drift | PR/List speedup | Status |")
print("|---|---:|---:|---:|---:|---:|---:|---:|---:|---|")

def spread(x, y):
    return abs(x-y) / statistics.mean((x,y))

ratios = []
for key in sorted(a):
    label, ordinal, count, method = key
    if method != "SortzSort":
        continue
    control = (label, ordinal, count, "DefaultListSort")
    m, p = statistics.mean((a[key], d[key])), statistics.mean((b[key], c[key]))
    mc, pc = statistics.mean((a[control], d[control])), statistics.mean((b[control], c[control]))
    sm, sp = spread(a[key], d[key]), spread(b[key], c[key])
    drift = pc / mc - 1
    unstable = max(sm, sp, abs(drift), spread(a[control], d[control]), spread(b[control], c[control])) > .05
    if not unstable:
        ratios.append(p/m)
    name = label + (f" (Ordinal={ordinal})" if ordinal else "")
    print(f"| {name} | {count} | {m:.2f} | {p:.2f} | {p/m-1:+.2%} | "
          f"{sm:.2%} | {sp:.2%} | {drift:+.2%} | {pc/p:.2f}x | "
          f"{'Unstable' if unstable else 'OK'} |")

print(f"\nStable cases: {len(ratios)}/63.")
if ratios:
    print(f"Geometric mean change (stable cases): {math.exp(statistics.mean(map(math.log, ratios)))-1:+.2%}.")
else:
    print("No stable cases; no performance conclusion.")
