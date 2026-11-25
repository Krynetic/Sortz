# Krynetic.Sortz

The **fastest** .NET sorting library currently!

High-performance, in-place sorting for .NET lists and spans. Sortz uses **hardware-accelerated** sorting networks, radix sorting and specialized primitive algorithms, with comparison-based fallbacks for other types and custom comparers.

## Performance

**Sortz ranks #1 overall by benchmark wins** against the **.NET built-in**, **YamSort**, **Celerity.Sorting**, **HPCsharp**, **Anoprsst** and **SortingNetworksMergeSort** in [Sorting library comparison (PR #17)](https://github.com/Krynetic/Sortz/actions/runs/45633501866?pr=17): **49 of 64 workloads (76.6%)**, with zero managed allocation reported for all 64 Sortz measurements.

| Competitor | Sortz wins | Geometric mean speedup | Peak speedup |
|---|---:|---:|---:|
| .NET `List<T>.Sort()` | 59/64 | 4.09× | 15.51× |
| YamSort 1.1.0 | 64/64 | 4.49× | 49.26× |
| Celerity.Sorting 3.6.0 | 60/64 | 5.22× | 28.40× |
| HPCsharp 3.19.8 | 64/64 | 7.30× | 40.92× |
| Anoprsst 1.1.2 | 53/64 | 2.21× | 12.84× |
| SortingNetworksMergeSort (SourceGen 0.1.0) | 64/64 | 6.31× | 20.50× |

| Input pattern | Fastest of seven | vs .NET | vs YamSort | vs Celerity | vs HPCsharp | vs Anoprsst | vs SortingNetworks |
|---|---:|---:|---:|---:|---:|---:|---:|
| Random | 14/16 | 4.74× | 8.66× | 2.78× | 4.46× | 2.88× | 6.04× |
| Ascending | 12/16 | 5.11× | 2.38× | 8.33× | 13.59× | 2.94× | 6.84× |
| Descending | 15/16 | 7.26× | 7.04× | 11.22× | 13.77× | 2.84× | 9.97× |
| NearlySorted | 8/16 | 1.59× | 2.79× | 2.86× | 3.40× | 0.99× | 3.86× |

The comparison covers `int`, `long`, `double` and ordinal `string`, with 128, 1,024, 10,000 and 100,000 elements per pattern (448 timed benchmark cases).  
Sortz has the lowest reported mean in 49/64 workloads; Anoprsst wins 9, .NET wins 3 and Celerity wins 3. YamSort, HPCsharp and SortingNetworksMergeSort win none. Small timing differences are not necessarily statistically significant.  
SortingNetworksMergeSort combines generated sorting networks with a full-array merge adapter; its timings include merging and scratch allocation. It is not a standalone general-purpose SortingNetworks API. Anoprsst may use parallel sorting on larger inputs.  
This is a measured suite-wide ranking, not a universal fastest-library guarantee.

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
The comparison uses 20 warmups, 40 measured iterations and 16 independent sorts per invocation, with source restoration outside measured work.  
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
