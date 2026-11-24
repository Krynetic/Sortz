# Sortz

High-performance, in-place sorting for .NET lists and spans. Sortz selects specialized SIMD and radix sorting paths for supported primitive types and uses comparison-based fallbacks for other types and custom comparers.

## Requirements

- .NET 11
- No additional runtime package dependencies

## Usage

```csharp
using Sortz;

List<int> values = [5, 1, 4, 2, 3];
values.SortNext();

int[] numbers = [9, 3, 7, 1];
SortzNext.Sort(numbers.AsSpan());

List<string> names = ["Charlie", "Alice", "Bob"];
names.SortNext(StringComparer.Ordinal);
```

Sorting modifies the original list or span. Hardware-accelerated paths are selected when supported; other systems use fallback algorithms.

## Build and test

```sh
dotnet build Sortz.sln -c Release
dotnet test Sortz.Tests/Sortz.Tests.csproj -c Release
```

## NuGet packaging

Create a package locally from the library project:

```sh
dotnet pack Sortz/Sortz.csproj -c Release -o artifacts/nuget
```

The default package version is defined in the library project. Override it for a release:

```sh
dotnet pack Sortz/Sortz.csproj -c Release -p:Version=0.1.0 -o artifacts/nuget
```

This produces a `.nupkg` library package and a `.snupkg` portable symbol package. Tests and benchmarks are not packed.

After choosing a license, verifying availability or ownership of the package ID, and obtaining a NuGet.org API key, publish the package manually:

```sh
dotnet nuget push artifacts/nuget/Sortz.0.1.0.nupkg --source https://api.nuget.org/v3/index.json --api-key "$NUGET_API_KEY"
```

Keep the API key in an environment variable or secure credential store. The project does not publish automatically.
