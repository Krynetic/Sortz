# Sorting library comparison

The standalone comparison project is deliberately excluded from the ordinary solution and iteration benchmark project. It uses the local Krynetic.Sortz project and pins YamSort 1.1.0, Celerity.Sorting 3.6.0 and HPCsharp 3.19.8.

Run locally with .NET 11:

```sh
dotnet run -c Release --project Krynetic.Sortz.Comparison -- --filter '*ComparisonBenchmarks*' --exporters json
```

Run only Int32:

```sh
dotnet run -c Release --project Krynetic.Sortz.Comparison -- --filter '*Int32ComparisonBenchmarks*'
```

The separate comparison.yaml workflow runs on manual dispatch and changes to its own project/workflow, not ordinary sorting implementation edits. Results are written to the job summary and uploaded as an artifact.

## Methodology

- DefaultListSort is the baseline; Sortz and YamSort use native list APIs, Celerity and HPCsharp use native array APIs. Input creation and resetting are outside timing. No conversion costs are included; this measures native sorting APIs rather than end-to-end List conversion.
- All five receive identical deterministic data, with 16 independent buffers per timed invocation and per-sort normalization.
- Int32, Int64, Double and String; 128, 1,024, 10,000 and 100,000 elements; random input only (80 benchmark cases).
- Single-threaded APIs: Celerity RadixSort.Sort for numbers and StringSort.Sort for strings; HPCsharp SortRadixLsd for integers, SortRadixMsd for doubles and SortMergeInPlace for strings; YamSorter.Sort; Sortz.Sortz extension.
- Double inputs are finite. String inputs are random lowercase ASCII strings of length 8–24. List.Sort, Sortz, Celerity and HPCsharp use ordinal ordering; YamSort uses its default comparison in invariant culture, with exact output validation against ordinal sorting for these inputs. This is not a benchmark of general Unicode or culture-sensitive sorting.
- Each case validates all five outputs against Array.Sort before measuring. Mean, ratio and managed allocations are reported. Lower time and ratio are better; speedup is baseline mean divided by competitor mean.

Sources: [YamSort](https://www.nuget.org/packages/YamSort/1.1.0), [Celerity.Sorting](https://www.nuget.org/packages/Celerity.Sorting/3.6.0), [HPCsharp](https://www.nuget.org/packages/HPCsharp/3.19.8).
