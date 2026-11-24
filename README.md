# Sortz

High-performance, in-place sorting for .NET lists and spans. Sortz uses hardware-accelerated sorting networks, radix sorting and specialized primitive algorithms, with comparison-based fallbacks for other types and custom comparers.

## Performance

**Up to 242.29× faster than `List<T>.Sort()`** in the measured random-input benchmarks. The geometric mean speedup across all 63 cases is **5.35×**, with 62 cases faster than the default sorter and 14 cases at least 10× faster.

| Type | 128 elements | 1,024 elements | 10,000 elements |
|---|---:|---:|---:|
| bool | 26.64× | 83.84× | 242.29× |
| byte | 3.80× | 8.31× | 41.45× |
| sbyte | 3.13× | 7.98× | 41.11× |
| char | 4.38× | 3.45× | 14.58× |
| short | 3.66× | 3.42× | 14.54× |
| ushort | 3.93× | 2.46× | 13.95× |
| int | 2.72× | 2.82× | 7.09× |
| uint | 2.36× | 2.84× | 7.16× |
| long | 2.66× | 2.41× | 7.93× |
| ulong | 2.93× | 2.21× | 8.05× |
| Int128 | 2.00× | 2.76× | 8.18× |
| UInt128 | 1.89× | 2.66× | 8.06× |
| nint | 2.55× | 2.10× | 7.77× |
| nuint | 3.10× | 2.26× | 7.87× |
| Half | 4.85× | 3.35× | 19.42× |
| float | 3.06× | 3.34× | 10.08× |
| double | 3.27× | 2.60× | 10.65× |
| decimal | 2.11× | 2.76× | 7.77× |
| string (ordinal) | 3.99× | 6.27× | 12.64× |
| string (culture) | 7.77× | 11.83× | 15.10× |
| object benchmark | 1.02× | 0.97× | 1.15× |

Speedup = DefaultListSort mean / SortzNext mean. Values above 1× favor Sortz. These are measured results, not guaranteed speedups for every input or machine.

Source: [benchmark workflow](https://github.com/Krynetic/Sortz/actions/runs/45511248356), commit [284d315](https://github.com/Krynetic/Sortz/commit/284d315b3ba71049bd74524efa115c11c3845eb9), AMD Ryzen 9 7900X, Ubuntu 24.04, .NET 11, BenchmarkDotNet 0.16.0-preview.2. All 648 tests passed.

See the [complete benchmark timings](docs/benchmarks.md) for the measured means.

## Installation

Requires .NET 11. Sortz has no additional runtime package dependencies.

Once the package is available on NuGet:

```sh
dotnet add package Krynetic.Sortz
```

Or reference the library project directly when working from the repository.

## Quick start

```csharp
using Sortz;

List<int> values = [5, 1, 4, 2, 3];
values.SortNext();

// [1, 2, 3, 4, 5]
```

### Sort arrays and spans

```csharp
using Sortz;

int[] numbers = [9, 3, 7, 1];
SortzNext.Sort(numbers.AsSpan());

Span<int> buffer = stackalloc int[] { 8, 4, 2, 6 };
SortzNext.Sort(buffer);

// Sort only a range of an array.
SortzNext.Sort(numbers.AsSpan(1, 2));
```

### Custom comparers and descending order

```csharp
using Sortz;

List<int> values = [5, 1, 4, 2, 3];
values.SortNext(Comparer<int>.Create(static (a, b) => b.CompareTo(a)));
```

### Strings

```csharp
using Sortz;

List<string> names = ["Charlie", "Alice", "Bob"];

// Default comparison (culture-sensitive for strings).
names.SortNext();

// Ordinal comparison.
names.SortNext(StringComparer.Ordinal);

// Case-insensitive ordinal comparison.
names.SortNext(StringComparer.OrdinalIgnoreCase);
```

### Custom types

```csharp
using Sortz;

List<Product> products = [new("Keyboard", 129), new("Mouse", 49)];
products.SortNext(Comparer<Product>.Create(static (a, b) => a.Price.CompareTo(b.Price)));

record Product(string Name, decimal Price);
```

## API

| API | Description |
|---|---|
| `list.SortNext(comparer = null)` | Sorts a `List<T>` in place. |
| `SortzNext.Sort<T>(Span<T> values, IComparer<T>? comparer = null)` | Sorts a span in place, including array slices and stack-allocated buffers. |

When no comparer is supplied, sorting uses `Comparer<T>.Default`. Specialized paths are selected automatically when compatible with the type, comparer, input and hardware. Arbitrary comparers use comparison-based sorting.

## Behavior

- Sorts in place; temporary scratch storage may be used internally.
- Sorting is not guaranteed to be stable.
- Supports standard numeric primitives, strings and other comparable types.
- SIMD acceleration is selected at runtime, with fallbacks on unsupported hardware.
- Do not modify a list or its backing storage concurrently while sorting.

## Build and test

```sh
dotnet build Sortz.sln -c Release
dotnet test Sortz.Tests/Sortz.Tests.csproj -c Release
```

## Run benchmarks

From the repository root, using the .NET 11 SDK:

```sh
dotnet run -c Release --project Sortz.Benchmarks -- --filter '*SortBenchmarks*' --exporters json
```

This runs the sorting comparison suite. To discover available benchmarks, select individual types, or locate the reports, see [benchmark documentation](docs/benchmarks.md).

Maintainers: see [packaging and publishing](docs/publishing.md).
