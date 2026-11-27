# Krynetic.Sortz

The **fastest** .NET sorting library currently!

High-performance, in-place sorting for .NET lists and spans. Sortz uses **hardware-accelerated** sorting networks, radix sorting and specialized primitive algorithms, with comparison-based fallbacks for other types and custom comparers.

## Performance

**Sortz ranks #1 overall by benchmark wins**, taking first place in **41 of 48 workloads (85.4%)** against six competing implementations in [Sorting library comparison — November 27, 2026](https://github.com/Krynetic/Sortz/actions/runs/45970966424). Sortz is **1.75× faster than its previous published comparison on matching workloads** (43.0% lower geometric-mean sorting time), with **5.86× geometric-mean speedup and up to 49.26× speedup over .NET `List<T>.Sort()`**. Zero managed allocation was reported for all 48 Sortz measurements.

### Competitor comparison

| Competitor | Sortz wins | Previous matched speedup | Latest speedup | Peak speedup |
|---|---:|---:|---:|---:|
| .NET `List<T>.Sort()` | 44/48 | 3.69× | 5.86× | 49.26× |
| YamSort | 47/48 | 4.46× | 7.22× | 48.18× |
| Celerity.Sorting | 46/48 | 5.58× | 9.58× | 83.30× |
| HPCsharp | 48/48 | 7.46× | 13.01× | 103.45× |
| Anoprsst | 44/48 | 2.47× | 3.54× | 38.91× |
| SortingNetworksMergeSort | 46/48 | 5.81× | 9.23× | 67.28× |

Sortz wins 275 of 288 head-to-head comparisons (95.5%). Across the seven implementations, Sortz has the lowest reported mean in 41/48 workloads, Anoprsst in 3, .NET in 2 and Celerity in 2. YamSort, HPCsharp and SortingNetworksMergeSort win none.

### Improvement over previous Sortz

| Type | Sortz vs previous Sortz | Improved workloads | Previous first places | Latest first places |
|---|---:|---:|---:|---:|
| double | 2.23× | 11/12 | 9/12 | 10/12 |
| int | 2.22× | 12/12 | 12/12 | 12/12 |
| long | 1.66× | 9/12 | 10/12 | 10/12 |
| string | 1.15× | 10/12 | 9/12 | 9/12 |

| Input pattern | Sortz vs previous Sortz | Fastest of seven | vs .NET | vs YamSort | vs Celerity | vs HPCsharp | vs Anoprsst | vs SortingNetworks |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Random | 1.02× | 10/12 | 3.45× | 6.89× | 3.15× | 4.58× | 2.95× | 4.51× |
| Ascending | 2.89× | 10/12 | 12.88× | 6.73× | 23.08× | 40.25× | 7.30× | 18.05× |
| Descending | 2.35× | 12/12 | 14.26× | 16.91× | 26.06× | 31.72× | 5.53× | 19.79× |
| NearlySorted | 1.37× | 9/12 | 1.86× | 3.46× | 4.45× | 4.90× | 1.31× | 4.50× |

The latest comparison covers `int`, `long`, `double` and ordinal `string`, with 128, 1,024 and 10,000 elements across four input patterns (336 timed benchmark cases). Compared with the same 48 workloads in the [previous comparison (PR #17)](https://github.com/Krynetic/Sortz/actions/runs/45633501866?pr=17), Sortz improves 42/48 reported means and wins 41/48 rather than 40/48. The previous full suite also included 100,000-element workloads (49/64 first places); those were not rerun and are excluded from matched speedups. The earlier run used 20 warmups and 40 measured iterations; the latest uses 8 and 16, respectively. Differences are observed cross-run results, not isolated algorithm effects or statistical significance claims.

SortingNetworksMergeSort combines generated sorting networks with a full-array merge adapter; its timings include merging and scratch allocation. It is not a standalone general-purpose SortingNetworks API. Anoprsst may use parallel sorting on larger inputs.

This is a measured suite-wide ranking, not a universal fastest-library guarantee. See [detailed latest and historical results](docs/benchmarks.md#competitor-comparison), including all six regressions and full timing tables.

### Broader type coverage — random inputs

**Up to 222.04× faster than `List<T>.Sort()`**, with a **5.61× geometric mean speedup**, 63/63 faster cases and 5/63 cases at least 10× faster in [Sorting benchmarks #130](https://github.com/Krynetic/Sortz/actions/runs/45608014802).  
All 648 tests passed in that workflow.

| Type | 128 elements | 1,024 elements | 10,000 elements |
|---|---:|---:|---:|
| bool | 64.28× | 171.02× | 222.04× |
| byte | 1.48× | 3.59× | 9.18× |
| sbyte | 1.37× | 3.69× | 10.31× |
| char | 3.15× | 4.77× | 9.90× |
| short | 2.60× | 4.38× | 9.59× |
| ushort | 2.84× | 4.21× | 9.56× |
| int | 4.21× | 5.99× | 8.60× |
| uint | 3.45× | 4.93× | 7.03× |
| long | 3.55× | 4.56× | 7.19× |
| ulong | 3.62× | 4.52× | 7.33× |
| Int128 | 3.10× | 4.52× | 7.56× |
| UInt128 | 2.88× | 4.39× | 7.48× |
| nint | 3.70× | 4.73× | 7.51× |
| nuint | 3.88× | 4.93× | 7.66× |
| Half | 2.90× | 4.00× | 7.96× |
| float | 3.02× | 3.94× | 6.97× |
| double | 2.94× | 3.61× | 7.01× |
| decimal | 3.78× | 6.24× | 10.26× |
| string (ordinal) | 3.42× | 4.52× | 5.38× |
| string (culture) | 3.69× | 6.25× | 9.15× |
| object benchmark | 4.01× | 4.60× | 4.46× |

Speedups are **(competitor mean)** / **(Sortz mean)** (higher is better); geometric means weight each workload equally. Both workflows ran on **AMD Ryzen 9 7900X, Ubuntu 24.04.5, .NET 11, BenchmarkDotNet 0.16.0-preview.2**.  
The latest comparison uses 8 warmups, 16 measured iterations and 16 independent sorts per invocation, with source restoration outside measured work. The broader type coverage above is historical and was not rerun by the latest comparison.  
Results are hardware- and workload-dependent.

See the [full competitor timings and allocation results](docs/benchmarks.md#competitor-comparison) and [complete primitive benchmark timings](docs/benchmarks.md).

### Performance engineering

- Adaptive, type- and input-aware dispatch: specialized numeric paths, generic comparison fallbacks and ordered-input detection.
- SIMD sorting networks and runtime hardware detection, including AVX-512 on supported x64 processors.
- Counting, bucket and radix sorting for suitable primitive types; compact projected keys and collision handling for wider values.
- Specialized ordinal string prefix sorting and culture-aware sort-key caching within a sort.
- In-place list and span APIs; stack-allocated scratch buffers for smaller inputs and pooled arrays for larger workloads. Zero reported managed allocations in the comparison does not mean zero scratch memory or allocation-free cold starts.
- No runtime package dependencies.

## Installation

Targets **.NET 8**, **.NET 9** and **.NET 11**. Sortz has no additional runtime package dependencies.  
**No target for .NET 7 and below exist** due non-existent hardware acceleration possible.

| Target | Sorting implementation |
|---|---|
| `net11.0` | Full optimized implementation; all published benchmarks run on this target. |
| `net9.0` | Same optimized implementation, including runtime-selected SIMD. |
| `net8.0` | Same optimized implementation, including runtime-selected SIMD and radix sorting. |

NuGet selects the most compatible assembly. All three targets share the same sorting source code.  
Published performance results are measured on .NET 11 only; performance may differ across runtimes.

Once the package is available on NuGet:

```sh
dotnet add package Krynetic.Sortz
```

Or reference the library project directly when working from the repository.

## Quick start

```csharp
using Krynetic.Sortz;

List<int> values = [5, 1, 4, 2, 3];
values.Sortz();

// [1, 2, 3, 4, 5]
```

### Sort arrays and spans

```csharp
using Krynetic.Sortz;

int[] numbers = [9, 3, 7, 1];
Sortz.Sort(numbers.AsSpan());

Span<int> buffer = stackalloc int[] { 8, 4, 2, 6 };
Sortz.Sort(buffer);

// Sort only a range of an array.
Sortz.Sort(numbers.AsSpan(1, 2));
```

### Custom comparers and descending order

```csharp
using Krynetic.Sortz;

List<int> values = [5, 1, 4, 2, 3];
values.Sortz(Comparer<int>.Create(static (a, b) => b.CompareTo(a)));
```

### Strings

```csharp
using Krynetic.Sortz;

List<string> names = ["Charlie", "Alice", "Bob"];

// Default comparison (culture-sensitive for strings).
names.Sortz();

// Ordinal comparison.
names.Sortz(StringComparer.Ordinal);

// Case-insensitive ordinal comparison.
names.Sortz(StringComparer.OrdinalIgnoreCase);
```

### Custom types

```csharp
using Krynetic.Sortz;

List<Product> products = [new("Keyboard", 129), new("Mouse", 49)];
products.Sortz(Comparer<Product>.Create(static (a, b) => a.Price.CompareTo(b.Price)));

record Product(string Name, decimal Price);
```

The extension method is provided by `SortzExtensions`; the span API is provided by `Krynetic.Sortz.Sortz`.

## API

| API | Description |
|---|---|
| `list.Sortz(comparer = null)` | Sorts a `List<T>` in place. |
| `Sortz.Sort<T>(Span<T> values, IComparer<T>? comparer = null)` | Sorts a span in place, including array slices and stack-allocated buffers. |

When no comparer is supplied, sorting uses `Comparer<T>.Default`. Specialized paths are selected automatically when compatible with the type, comparer, input and hardware. Arbitrary comparers use comparison-based sorting.

## Behavior

- Sorts in place; temporary scratch storage may be used internally.
- Sorting is not guaranteed to be stable.
- Supports standard numeric primitives, strings and other comparable types.
- SIMD acceleration is selected at runtime, with fallbacks on unsupported hardware.
- Do not modify a list or its backing storage concurrently while sorting.

## Build and test

```sh
dotnet build Krynetic.Sortz.sln -c Release
dotnet test Krynetic.Sortz.Tests/Krynetic.Sortz.Tests.csproj -c Release
```

## Run benchmarks

From the repository root, using the .NET 11 SDK (both benchmark projects target .NET 11 only):

```sh
dotnet run -c Release -f net11.0 --project Krynetic.Sortz.Benchmarks -- --filter '*SortBenchmarks*' --exporters json
```

For the direct competitor comparison:

```sh
dotnet run -c Release -f net11.0 --project Krynetic.Sortz.Comparison -- --filter '*ComparisonBenchmarks*' --exporters json
```

To discover available benchmarks, select individual types, or locate reports, see [benchmark documentation](docs/benchmarks.md).

Maintainers: see [packaging and publishing](docs/publishing.md).
