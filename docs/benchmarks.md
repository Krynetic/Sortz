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

[Sorting library comparison (PR #17)](https://github.com/Krynetic/Sortz/actions/runs/45633501866?pr=17) (commit `b318bf5`, 2026-11-25). BenchmarkDotNet 0.16.0-preview.2, .NET 11.0.0, AMD Ryzen 9 7900X, Ubuntu 24.04.5. 4 types × 4 sizes × 4 patterns × 7 implementations = 448 benchmark cases. All completed successfully.

The seeded source values are identical across implementations for each workload. Sorted inputs use the same comparer; nearly sorted inputs swap every 31st position with a seeded random position. List buffers are used for .NET, Sortz and YamSort; array buffers for Celerity, HPCsharp, Anoprsst and SortingNetworksMergeSort. Correctness is validated against a sorted reference during setup. Buffers are restored outside the timed workload. Means are per sort (16 sorts per invocation; 20 warmups, 40 iterations, one launch, in-process no-emit). This is a single-machine benchmark, not a cross-platform guarantee.

SortingNetworksMergeSort combines generated sorting networks with a full-array merge adapter; its timings include merging and scratch allocation. It is not a standalone general-purpose SortingNetworks API. Anoprsst may use parallel sorting on larger inputs.

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

Sortz has the lowest reported mean in 49/64 workloads; Anoprsst wins 9, .NET wins 3 and Celerity wins 3. YamSort, HPCsharp and SortingNetworksMergeSort win none. Small timing differences are not necessarily statistically significant.

### Full mean timings (ns per sort)

Bold indicates the lowest reported mean in each row. Values are converted from the reported units; speedups use these reported means rather than rounded BenchmarkDotNet ratios.

#### int

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp | Anoprsst | SortingNetworksMergeSort |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 128 | Random | 582.12 | **117.34** | 813.1 | 987.34 | 1421.05 | 801.81 | 538.68 |
| 128 | Ascending | 247.99 | **60.61** | 129 | 980.59 | 1391.96 | 93.15 | 374.65 |
| 128 | Descending | 319.43 | **78.21** | 694.17 | 988.02 | 1393.96 | 92.58 | 457.27 |
| 128 | NearlySorted | 294.77 | **117.74** | 374.42 | 1009.09 | 1389.52 | 275.19 | 483.08 |
| 1,024 | Random | 6922.3 | **1802.49** | 9347.77 | 4608.68 | 5529.29 | 9521.27 | 7123.4 |
| 1,024 | Ascending | 2313.11 | **322.23** | 772.49 | 4614.22 | 5552.33 | 1412.55 | 2390.44 |
| 1,024 | Descending | 3514.37 | **427.69** | 3065.46 | 4526.02 | 5558.78 | 1411.44 | 4617.48 |
| 1,024 | NearlySorted | 2946.8 | **1798.33** | 4616.47 | 4614.01 | 5643.2 | 2712.98 | 6458.57 |
| 10,000 | Random | 242222.2 | **33832.99** | 289999.39 | 42340.05 | 49066.68 | 133564.27 | 234037.16 |
| 10,000 | Ascending | 26832.98 | **2928.78** | 6668.3 | 44743.61 | 50849.44 | 37602.4 | 23968.57 |
| 10,000 | Descending | 49449.81 | **3896.45** | 29133.82 | 43964.47 | 50695.78 | 38628.66 | 61294.28 |
| 10,000 | NearlySorted | 42815.06 | **33306.78** | 56604.55 | 42484.89 | 49077.57 | 46935.77 | 104236.85 |
| 100,000 | Random | 3251095.89 | **340441.11** | 4163597.73 | 440949.59 | 500689.32 | 683957.15 | 3731021.75 |
| 100,000 | Ascending | 331479.99 | **29887.97** | 77867.22 | 441023.36 | 508419.08 | 158207.19 | 275151.18 |
| 100,000 | Descending | 631017.58 | **40690.42** | 318777.79 | 440575.8 | 502600 | 251593.88 | 834016.35 |
| 100,000 | NearlySorted | 612534.11 | 357721.58 | 857141.25 | 550080.04 | 512815.32 | **237199.5** | 1507624.46 |

#### long

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp | Anoprsst | SortingNetworksMergeSort |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 128 | Random | 559.94 | **378.05** | 817.16 | 1969.89 | 2789.75 | 821.31 | 829.96 |
| 128 | Ascending | 254.35 | **70.49** | 138.57 | 2001.93 | 2884.66 | 99.58 | 596.43 |
| 128 | Descending | 347.12 | **94.09** | 715.48 | 1908.64 | 2766.88 | 95.78 | 749.48 |
| 128 | NearlySorted | **276.13** | 371.79 | 387.15 | 1948.89 | 2750.49 | 305.05 | 836.99 |
| 1,024 | Random | 6888.83 | **3796.83** | 9867.77 | 9533.36 | 9766.69 | 9445.36 | 11008.38 |
| 1,024 | Ascending | 2347.8 | **404.53** | 815.94 | 9186.38 | 9836.36 | 1456.48 | 2900.68 |
| 1,024 | Descending | 3630.55 | **477.04** | 3337.96 | 9236.3 | 9926.58 | 1455.94 | 5494.88 |
| 1,024 | NearlySorted | **2925.1** | 3751.45 | 4610.26 | 9194.6 | 10113.23 | 3057.67 | 8114.33 |
| 10,000 | Random | 241770.32 | **32043.82** | 291162.37 | 90564.32 | 82939.1 | 140370.14 | 374745.69 |
| 10,000 | Ascending | 26530.86 | **3680.59** | 6916.6 | 91240.59 | 82573.29 | 37024.98 | 31496.97 |
| 10,000 | Descending | 47306.39 | **4181.01** | 35111.67 | 94658.35 | 82710.74 | 38660.61 | 68951.98 |
| 10,000 | NearlySorted | 49440.64 | **33169.37** | 58816.53 | 90598.22 | 81451.62 | 48284.61 | 123243.37 |
| 100,000 | Random | 3225952.58 | 716305.81 | 4235137.2 | 934981.98 | 866555.63 | **689621.85** | 5065662.64 |
| 100,000 | Ascending | 335557.37 | **38272.15** | 80888.65 | 940165.65 | 874942.58 | 184857.74 | 403097.25 |
| 100,000 | Descending | 617587.52 | **46664.13** | 412431.3 | 933697.08 | 875869.49 | 269049.63 | 935299.08 |
| 100,000 | NearlySorted | 680829.74 | 708372.3 | 933403.33 | 936539.83 | 873727.32 | **250739.54** | 1945073.06 |

#### double

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp | Anoprsst | SortingNetworksMergeSort |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 128 | Random | **569.9** | 620.6 | 878.9 | 2071.4 | 1897.6 | 810.7 | 763.3 |
| 128 | Ascending | 239 | **120.1** | 153.4 | 2117.9 | 1527 | 141.5 | 585.3 |
| 128 | Descending | 414 | **111.3** | 781.1 | 2156.2 | 1775.1 | 156.1 | 741.8 |
| 128 | NearlySorted | 318.8 | 383.2 | 482.9 | 2099.9 | 1743.4 | **268.2** | 944.4 |
| 1,024 | Random | 8489.9 | **3765.6** | 10459.1 | 10957.3 | 14341.8 | 11918.2 | 11720.4 |
| 1,024 | Ascending | 2335.7 | **772.7** | 925.2 | 11350.9 | 8653 | 2030.4 | 2835 |
| 1,024 | Descending | 4561.7 | **742.3** | 3515.5 | 10637.1 | 11754.7 | 2101.5 | 5554.8 |
| 1,024 | NearlySorted | 3953.1 | 3786.1 | 5067.7 | 10796.6 | 10989 | **3335.7** | 8979.2 |
| 10,000 | Random | 343853.6 | **33858.2** | 389690.3 | 107807.4 | 338191.5 | 185663.8 | 466634.2 |
| 10,000 | Ascending | 31921 | **7287.8** | 8517.9 | 100996.1 | 133064.2 | 38109.8 | 30718.1 |
| 10,000 | Descending | 60537.2 | **6872.4** | 37131.5 | 102097.9 | 161313.7 | 41380.3 | 72297.7 |
| 10,000 | NearlySorted | 58757.5 | **33160.3** | 62055.7 | 103558.2 | 177631.3 | 42682.4 | 133755.2 |
| 100,000 | Random | 4608295.2 | **741057.6** | 5742410.6 | 1226026.8 | 3832678.7 | 1056347.4 | 6399810.9 |
| 100,000 | Ascending | 381303.5 | **74183.4** | 90033.7 | 1035040.5 | 1743592.3 | 197478 | 403409.6 |
| 100,000 | Descending | 772214.5 | **72735.3** | 424877.7 | 1035872.6 | 2121151.9 | 295697.2 | 989703.2 |
| 100,000 | NearlySorted | 840880.9 | 751276.9 | 1047567.8 | 1151325.7 | 2340835.4 | **295718.9** | 2179651.4 |

#### string

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp | Anoprsst | SortingNetworksMergeSort |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 128 | Random | 2915.2 | **587.4** | 17330.2 | 1537.7 | 2922.8 | 2203.1 | 3505.8 |
| 128 | Ascending | 1127.8 | 247.5 | 2892.1 | **222** | 1118.4 | 494.3 | 2982.3 |
| 128 | Descending | 1829.8 | **414.3** | 5937.9 | 1286.8 | 1797.5 | 645.2 | 3254 |
| 128 | NearlySorted | 1325.7 | **584.6** | 9117.5 | 1441.3 | 1322.4 | 627.9 | 3574.5 |
| 1,024 | Random | 32871.7 | **6280.3** | 238930.4 | 14593.2 | 38668.4 | 26915.7 | 40909.3 |
| 1,024 | Ascending | 13974.1 | 2945.5 | 24373.2 | **2825.9** | 14229.8 | 7409.4 | 23183.1 |
| 1,024 | Descending | 24432.7 | **4357.6** | 35974.8 | 17814.2 | 24387.7 | 8681.3 | 28590.5 |
| 1,024 | NearlySorted | 16675.8 | **6436.2** | 83232.4 | 14881.9 | 16679.8 | 10216.7 | 36614.1 |
| 10,000 | Random | 899536.6 | **68954.5** | 3396984.6 | 363359.6 | 891612.2 | 288457.3 | 1082894.8 |
| 10,000 | Ascending | 287526.2 | 65441.9 | 255481.7 | **43114.8** | 287871.8 | 125259.4 | 406141.1 |
| 10,000 | Descending | 479710.8 | **76474.3** | 401493.7 | 404822.2 | 475937.6 | 159422.5 | 513933.7 |
| 10,000 | NearlySorted | 342893.5 | **75492.5** | 1070309.2 | 438582.6 | 338547.2 | 138820.6 | 773186.8 |
| 100,000 | Random | 13231495.1 | **1067993.4** | 48865954.9 | 4893845.6 | 23362456.4 | 1689223.9 | 16511639.5 |
| 100,000 | Ascending | 4516854.5 | 1074378.7 | 2926336.1 | 986939.7 | 4141200.2 | **711016.2** | 5369634 |
| 100,000 | Descending | 7861494.5 | 1226617.5 | 4689820.2 | 6733931.5 | 9327515 | **882670.9** | 7660431.5 |
| 100,000 | NearlySorted | 5437526.5 | 1173497.6 | 12583575.3 | 4723278.7 | 13645283.1 | **858128.2** | 11895650.5 |

### Managed allocation results

| Implementation | Zero reported allocation | Nonzero reported allocation |
|---|---:|---:|
| DefaultListSort | 48 | 16 |
| SortzSort | 64 | 0 |
| YamSort | 64 | 0 |
| CeleritySorting | 64 | 0 |
| HPCsharp | 0 | 64 |
| Anoprsst | 32 | 32 |
| SortingNetworksMergeSort | 0 | 64 |

A dash in the BenchmarkDotNet Allocated column is counted as zero reported managed allocation, not as proof of no scratch memory or allocation-free cold starts.

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
