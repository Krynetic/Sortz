# Benchmark timings

## Broader type coverage

Measured means from [Sorting benchmarks #130](https://github.com/Krynetic/Sortz/actions/runs/45608014802), commit `5283e78`. AMD Ryzen 9 7900X, Ubuntu 24.04.5, .NET 11, BenchmarkDotNet 0.16.0-preview.2. All 648 tests passed.

All times are in nanoseconds (ns) per sort. The input distribution is random, and results are hardware- and workload-dependent.

| Type | Default 128 | Sortz 128 | Default 1,024 | Sortz 1,024 | Default 10,000 | Sortz 10,000 |
|---|---:|---:|---:|---:|---:|---:|
| bool | 261.088 | 4.062 | 2707.145 | 15.829 | 35969.198 | 161.993 |
| byte | 196.5 | 132.4 | 3111.4 | 867.3 | 44133.5 | 4805.6 |
| sbyte | 197.7 | 144.3 | 3185.6 | 863.7 | 48889.2 | 4741.5 |
| char | 181.99 | 57.73 | 2242.52 | 470.45 | 45467.4 | 4592.29 |
| short | 180.3 | 69.33 | 2225.23 | 508.01 | 44679.79 | 4656.93 |
| ushort | 171.22 | 60.3 | 2296.8 | 544.97 | 44238.55 | 4627.83 |
| int | 255.84 | 60.72 | 3101.59 | 517.88 | 48456.14 | 5635.04 |
| uint | 217.29 | 62.9 | 2568.31 | 521.29 | 40536.85 | 5767.34 |
| long | 226.09 | 63.64 | 2579.53 | 565.72 | 41646.98 | 5793.51 |
| ulong | 217.97 | 60.2 | 2537.16 | 561.61 | 40437.44 | 5518.64 |
| Int128 | 312.8 | 100.9 | 3847.8 | 852.1 | 61928.9 | 8193.7 |
| UInt128 | 294.1 | 102 | 3649.2 | 831 | 60988.6 | 8154.6 |
| nint | 233.34 | 63.1 | 2716 | 573.91 | 41782.92 | 5567.05 |
| nuint | 237.34 | 61.18 | 2893.37 | 587.45 | 41704.44 | 5442.95 |
| Half | 296.6 | 102.1 | 3543.9 | 886.8 | 70489.9 | 8858.4 |
| float | 253.13 | 83.8 | 2774.16 | 703.75 | 51815.47 | 7435.88 |
| double | 253.18 | 86.07 | 2826.33 | 782.97 | 51099.16 | 7290.61 |
| decimal | 926.8 | 245.5 | 12225.5 | 1959.8 | 224039.5 | 21825.7 |
| string (ordinal) | 1231 | 359.9 | 17274.8 | 3819.2 | 347039.1 | 64527.4 |
| string (culture) | 11938 | 3232 | 175418.2 | 28066.2 | 2748120.4 | 300188.1 |
| object benchmark | 912.2 | 227.6 | 11307.5 | 2457.2 | 168473.9 | 37774.5 |

## Competitor comparison

[Sorting library comparison #6](https://github.com/Krynetic/Sortz/actions/runs/45609922336) (commit `39256e5`, 2026-11-25). BenchmarkDotNet 0.16.0-preview.2, .NET 11.0.0, AMD Ryzen 9 7900X, Ubuntu 24.04.5. 4 types × 4 sizes × 4 patterns × 5 implementations = 320 benchmark cases. All completed successfully.

The seeded source values are identical across implementations for each workload. Sorted inputs use the same comparer; nearly sorted inputs swap every 31st position with a seeded random position. List buffers are used for .NET, Sortz and YamSort; array buffers for Celerity and HPCsharp. Correctness is validated against a sorted reference during setup. Buffers are restored outside the timed workload. Means are per sort (16 sorts per invocation; 20 warmups, 40 iterations, one launch, in-process no-emit). This is a single-machine benchmark, not a cross-platform guarantee.

| Competitor | Sortz wins | Geometric mean speedup | Peak speedup |
|---|---:|---:|---:|
| .NET List<T>.Sort() | 59/64 | 4.16× | 14.38× |
| YamSort 1.1.0 | 63/64 | 4.56× | 50.38× |
| Celerity.Sorting 3.6.0 | 63/64 | 5.26× | 23.01× |
| HPCsharp 3.19.8 | 64/64 | 7.48× | 33.77× |

| Input pattern | Fastest of five | vs .NET | vs YamSort | vs Celerity | vs HPCsharp |
|---|---:|---:|---:|---:|---:|
| Random | 15/16 | 4.85× | 9.03× | 2.86× | 4.55× |
| Ascending | 15/16 | 5.34× | 2.45× | 8.53× | 14.23× |
| Descending | 16/16 | 7.42× | 7.20× | 11.39× | 14.44× |
| NearlySorted | 12/16 | 1.56× | 2.71× | 2.75× | 3.34× |

Sortz wins 58/64 workloads by lowest reported mean; .NET wins 5, Celerity wins 1, and YamSort and HPCsharp win none. Small timing differences are not necessarily statistically significant.

### Full mean timings (ns per sort)

Bold indicates the lowest reported mean in each row. Values converted from the reported units, not independently rounded ratios.

#### int

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp |
|---:|---|---:|---:|---:|---:|---:|
| 128 | Random | 574.84 | **118.02** | 835.46 | 1010.38 | 1426.88 |
| 128 | Ascending | 252.77 | **59.87** | 131.97 | 994.17 | 1426.17 |
| 128 | Descending | 331.61 | **71.61** | 684.96 | 994.33 | 1419.89 |
| 128 | NearlySorted | 271.79 | **117.86** | 381.84 | 1016.89 | 1429.29 |
| 1,024 | Random | 6706.95 | **1823.3** | 9630.05 | 4529.49 | 5585.28 |
| 1,024 | Ascending | 2472.24 | **321.61** | 841.09 | 4691.87 | 5953.62 |
| 1,024 | Descending | 3588.13 | **431.43** | 3057.91 | 4582.93 | 6063.4 |
| 1,024 | NearlySorted | 2863.9 | **1806.12** | 4666.91 | 4482.52 | 5616.12 |
| 10,000 | Random | 241472.3 | **33199.51** | 299222.74 | 42963.34 | 49221.23 |
| 10,000 | Ascending | 26316.51 | **2947.95** | 6660.23 | 44943.85 | 52029.65 |
| 10,000 | Descending | 45174.43 | **4118.97** | 29911.04 | 44479.32 | 51469.91 |
| 10,000 | NearlySorted | 50513.16 | **33960.5** | 56550.3 | 43230.96 | 50674.51 |
| 100,000 | Random | 3215882.22 | **345390.05** | 4327598.56 | 439348.26 | 506703.93 |
| 100,000 | Ascending | 327922.32 | **30088.27** | 78296.56 | 446839.71 | 513944.18 |
| 100,000 | Descending | 595122.73 | **41384.2** | 324053.81 | 447188.71 | 511914.65 |
| 100,000 | NearlySorted | 678982.7 | **358785.46** | 867524.53 | 453972.1 | 515962.9 |

#### long

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp |
|---:|---|---:|---:|---:|---:|---:|
| 128 | Random | 590.29 | **374.12** | 848.65 | 1984.92 | 2821.58 |
| 128 | Ascending | 252.66 | **85.83** | 138.97 | 1950.09 | 2898.87 |
| 128 | Descending | 321.98 | **97.5** | 753.79 | 1962.59 | 2764.2 |
| 128 | NearlySorted | **292.79** | 374.84 | 390.54 | 1965.14 | 2863.56 |
| 1,024 | Random | 7297.4 | **3780.75** | 9959.49 | 10222.95 | 9915.61 |
| 1,024 | Ascending | 2355.99 | **426.92** | 805.75 | 9511.65 | 10174.59 |
| 1,024 | Descending | 3752.65 | **502.41** | 3517.5 | 9213.16 | 10149.28 |
| 1,024 | NearlySorted | **3006.01** | 3803.19 | 4728.96 | 9346.43 | 9785.66 |
| 10,000 | Random | 240704.47 | **32870.95** | 324750.94 | 91189.97 | 83633.42 |
| 10,000 | Ascending | 27061.54 | **3996.31** | 6927.61 | 91800.17 | 83036.72 |
| 10,000 | Descending | 49970.93 | **4238.27** | 35698.45 | 91019.78 | 84305.03 |
| 10,000 | NearlySorted | 43331.61 | **32786.12** | 59349.04 | 90203.73 | 84481.7 |
| 100,000 | Random | 3242032.63 | **723555.07** | 4212141.55 | 939283.97 | 872534.32 |
| 100,000 | Ascending | 347917.71 | **41033.61** | 81768.52 | 944091.76 | 877152.28 |
| 100,000 | Descending | 638651.07 | **46019.26** | 418421.63 | 963099.94 | 867794.78 |
| 100,000 | NearlySorted | **622108.83** | 730426.53 | 948711.34 | 956991.97 | 882936.7 |

#### double

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp |
|---:|---|---:|---:|---:|---:|---:|
| 128 | Random | **589.9** | 628.8 | 878.7 | 2130.5 | 1918.1 |
| 128 | Ascending | 249 | **118.6** | 154.2 | 2057.3 | 1543.5 |
| 128 | Descending | 404.3 | **111.8** | 782.7 | 2156.4 | 1827.3 |
| 128 | NearlySorted | **338.5** | 626.7 | 476.9 | 2309 | 1768.8 |
| 1,024 | Random | 8177.6 | **3072.9** | 10552.1 | 10721.3 | 14733.8 |
| 1,024 | Ascending | 2362.8 | **772** | 934.1 | 10465 | 8787.4 |
| 1,024 | Descending | 4533.5 | **751.5** | 3501.3 | 10594.9 | 12013.2 |
| 1,024 | NearlySorted | 4040.6 | **3780.3** | 5154.6 | 10730.2 | 11456.1 |
| 10,000 | Random | 351080.1 | **32715.5** | 403189.7 | 105745.6 | 336979.3 |
| 10,000 | Ascending | 32012.6 | **7322.4** | 8307.4 | 101627.6 | 135229.1 |
| 10,000 | Descending | 60454.2 | **7262.1** | 38866.9 | 102263.4 | 166015.9 |
| 10,000 | NearlySorted | 58148.6 | **34949.5** | 65658.4 | 103547.6 | 178074.8 |
| 100,000 | Random | 4637801 | **747162.7** | 5815867.5 | 1228897.9 | 3871185.5 |
| 100,000 | Ascending | 387572.7 | **74028.9** | 91551.5 | 1041850.4 | 1780787.4 |
| 100,000 | Descending | 766712.4 | **73073.2** | 431147.8 | 1047021.2 | 2152998.3 |
| 100,000 | NearlySorted | 862225.9 | **762008.3** | 1056940.6 | 1066520.2 | 2347664.9 |

#### string (ordinal)

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp |
|---:|---|---:|---:|---:|---:|---:|
| 128 | Random | 2877.3 | **565.4** | 17691 | 1577.1 | 3027.9 |
| 128 | Ascending | 1181.1 | **199.1** | 2964.4 | 217.6 | 1184.1 |
| 128 | Descending | 1860.4 | **421.3** | 6089.8 | 1311.8 | 1981.8 |
| 128 | NearlySorted | 1411 | **585.4** | 9284.7 | 1482.7 | 1394.5 |
| 1,024 | Random | 34332.3 | **5974.9** | 243047.6 | 14681.5 | 34891.5 |
| 1,024 | Ascending | 16012.4 | **2439.6** | 25031.6 | 3054.2 | 15272.7 |
| 1,024 | Descending | 28119 | **3927.1** | 36719.6 | 18268.5 | 28167.5 |
| 1,024 | NearlySorted | 18021.3 | **6259.8** | 82677.1 | 15637.1 | 18063 |
| 10,000 | Random | 894776.4 | **67789.7** | 3415430 | 377174.3 | 898899.6 |
| 10,000 | Ascending | 304892.2 | 54999.5 | 263616.1 | **45386.4** | 313214.4 |
| 10,000 | Descending | 535349.4 | **67930.6** | 408392.6 | 407081.7 | 533730.6 |
| 10,000 | NearlySorted | 352543.8 | **75894** | 1083468.5 | 451885.7 | 352129.2 |
| 100,000 | Random | 13387286.8 | **1096931.7** | 50000232.5 | 5023457.7 | 23473616.8 |
| 100,000 | Ascending | 4693692.9 | **1001791.5** | 2996439.1 | 1030302.2 | 4284089.2 |
| 100,000 | Descending | 8551460.4 | **1178676.9** | 4597444.3 | 6981759 | 9798959.5 |
| 100,000 | NearlySorted | 5662989.3 | **1210656.8** | 12687785 | 4902596.8 | 13801591.8 |

### Managed allocations

BenchmarkDotNet MemoryDiagnoser reported no managed allocations for 64/64 Sortz cases. A dash is the reporter's no-allocation value; temporary stack or pooled storage is not represented by this metric.

| Implementation | Cases with no reported allocation | Cases with reported allocation |
|---|---:|---:|
| DefaultListSort | 48 | 16 |
| SortzSort | 64 | 0 |
| YamSort | 64 | 0 |
| CeleritySorting | 64 | 0 |
| HPCsharp | 0 | 64 |

### Reproduce the comparison

```sh
dotnet run -c Release --project Krynetic.Sortz.Comparison -- --filter '*ComparisonBenchmarks*' --exporters json
```

## Running benchmarks locally

Install the .NET 11 SDK, clone the repository, and run these commands from its root.

### Run the complete sorting comparison suite

```sh
dotnet run -c Release --project Krynetic.Sortz.Benchmarks -- --filter '*SortBenchmarks*' --exporters json
```

### List available benchmarks

```sh
dotnet run -c Release --project Krynetic.Sortz.Benchmarks -- --list flat
```

### Filter benchmarks

Use BenchmarkDotNet wildcard filters against the names listed above:

```sh
dotnet run -c Release --project Krynetic.Sortz.Benchmarks -- --filter '*DoubleSortBenchmarks*' --exporters json
dotnet run -c Release --project Krynetic.Sortz.Benchmarks -- --filter '*SortzSort*' --exporters json
```

The second command runs Sortz only; retain both sorting methods when measuring comparative speedups.

### Results and CLI options

```sh
dotnet run -c Release --project Krynetic.Sortz.Benchmarks -- --help
```

BenchmarkDotNet prints the summary to the terminal and writes reports under `BenchmarkDotNet.Artifacts/results/` by default, including JSON when requested above. Compare the Mean column for DefaultListSort and SortzSort at matching parameter values. Use Release builds, consistent hardware and runtime settings, and avoid other intensive workloads when measuring performance.
