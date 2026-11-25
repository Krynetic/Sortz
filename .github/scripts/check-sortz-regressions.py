"""Compare all Sortz means from the same-runner baseline and candidate reports."""
import math
import re
import sys
from pathlib import Path

def nanos(text):
    match = re.fullmatch(r"([0-9,.]+)\s*(ns|us|µs|μs|ms|s)", text)
    if not match:
        raise ValueError(f"Invalid mean: {text!r}")
    value = float(match[1].replace(",", "")) * {
        "ns": 1, "us": 1000, "µs": 1000, "μs": 1000, "ms": 1e6, "s": 1e9
    }[match[2]]
    if not math.isfinite(value) or value <= 0:
        raise ValueError(f"Nonpositive mean: {text}")
    return value

def load(directory):
    rows = {}
    for report in sorted(Path(directory).glob("*-report-github.md")):
        content = report.read_text(encoding="utf-8-sig")
        if "Benchmarks with issues:" in content:
            raise ValueError(f"Benchmark failures in {report.name}")
        header = None
        for line in content.splitlines():
            if not line.lstrip().startswith("|"):
                continue
            cells = [cell.strip().replace("**", "").replace("`", "")
                     for cell in line.strip().strip("|").split("|")]
            if "Method" in cells and "Mean" in cells:
                header = cells
                continue
            if not header or len(cells) != len(header):
                continue
            row = dict(zip(header, cells))
            if row.get("Method") != "SortzSort":
                continue
            key = (report.name.removesuffix("-report-github.md"),
                   row["Count"].replace(",", ""), row.get("Pattern", ""),
                   row.get("Ordinal", ""))
            if key in rows:
                raise ValueError(f"Duplicate case: {key}")
            rows[key] = nanos(row["Mean"])
    broad = sum(".Benchmarks." in k[0] for k in rows)
    comparison = sum(".Comparison." in k[0] for k in rows)
    if (broad, comparison) != (63, 64):
        raise ValueError(f"Expected 63 broad and 64 comparison cases; got {broad}, {comparison}")
    return rows

baseline, candidate = map(load, sys.argv[1:])
if baseline.keys() != candidate.keys():
    raise ValueError("Baseline and candidate workload sets differ")

print("## Sortz vs main — same-runner regression gate\n")
print("| Workload | Main (ns) | Candidate (ns) | Speedup | Result |")
print("|---|---:|---:|---:|---|")
regressions = 0
for key in sorted(baseline):
    old, new = baseline[key], candidate[key]
    regression = new > old
    regressions += regression
    name = " / ".join(part.rsplit(".", 1)[-1] for part in key if part)
    print(f"| {name} | {old:.3f} | {new:.3f} | {old/new:.4f}x | {'REGRESSION' if regression else 'PASS'} |")
print(f"\n{len(baseline)-regressions}/{len(baseline)} cases equal or faster; {regressions} regressions.")
print("\nStrict mean comparison; small changes may reflect measurement noise. Re-run before attributing causality.")
sys.exit(1 if regressions else 0)
