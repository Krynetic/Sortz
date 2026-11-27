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

[Sorting library comparison — November 27, 2026](https://github.com/Krynetic/Sortz/actions/runs/45970966424). BenchmarkDotNet 0.16.0-preview.2, .NET 11, AMD Ryzen 9 7900X, Ubuntu 24.04.5. 4 types × 3 sizes × 4 patterns × 7 implementations = 336 timed cases. 8 warmups, 16 measured iterations, 16 sorts per invocation, one launch, in-process no-emit. Results are hardware- and workload-dependent.

All speedups use reported means, with equal weighting of workloads in geometric means. Bold in the timing tables identifies the lowest reported mean. Small timing differences may not be statistically significant. The previous comparison used 20 warmups and 40 iterations; these results compare matching workloads, not a controlled isolation of algorithm changes. The 100,000-element workloads were not rerun. The historical 21-type random benchmark above is unchanged.

| Competitor | Sortz wins | Previous matched speedup | Latest speedup | Peak speedup |
|---|---:|---:|---:|---:|
| .NET `List<T>.Sort()` | 44/48 | 3.69× | 5.86× | 49.26× |
| YamSort | 47/48 | 4.46× | 7.22× | 48.18× |
| Celerity.Sorting | 46/48 | 5.58× | 9.58× | 83.30× |
| HPCsharp | 48/48 | 7.46× | 13.01× | 103.45× |
| Anoprsst | 44/48 | 2.47× | 3.54× | 38.91× |
| SortingNetworksMergeSort | 46/48 | 5.81× | 9.23× | 67.28× |

Sortz wins 41/48 workloads overall, up from 40/48 matching workloads in the previous run. It is 1.75× faster than previous Sortz across those workloads (43.0% lower geometric-mean time), improving 42/48 means. It wins 275/288 head-to-head comparisons, versus 277/288 in the matching previous suite. First places: Sortz 41, Anoprsst 3, .NET 2, Celerity 2.

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

### Observed regressions vs previous Sortz

| Type | Count | Pattern | Previous ns | Latest ns | Time increase |
|---|---:|---|---:|---:|---:|
| double | 1,024 | Random | 3,765.6 | 3,771.68 | +0.16% |
| long | 128 | Random | 378.05 | 599.82 | +58.66% |
| long | 128 | NearlySorted | 371.79 | 594.66 | +59.95% |
| long | 10,000 | Random | 32,043.82 | 34,067.3 | +6.31% |
| string | 10,000 | Random | 68,954.5 | 71,165.7 | +3.21% |
| string | 10,000 | NearlySorted | 75,492.5 | 77,443.5 | +2.58% |

### Workloads where a competitor wins

| Type | Count | Pattern | Fastest implementation | Winner ns | Sortz ns |
|---|---:|---|---|---:|---:|
| double | 128 | Random | .NET `List<T>.Sort()` | 402.07 | 609.69 |
| double | 128 | NearlySorted | Anoprsst | 210.43 | 353.11 |
| long | 128 | Random | .NET `List<T>.Sort()` | 386.77 | 599.82 |
| long | 128 | NearlySorted | Anoprsst | 216.55 | 594.66 |
| string | 128 | Ascending | Celerity.Sorting | 155.9 | 199.6 |
| string | 128 | NearlySorted | Anoprsst | 488.3 | 514 |
| string | 1,024 | Ascending | Celerity.Sorting | 2,196.1 | 2,377 |

### Latest full mean timings (ns per sort)

#### double

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp | Anoprsst | SortingNetworksMergeSort | Previous Sortz | Speedup vs previous |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 128 | Random | **402.07** | 609.69 | 698.95 | 2,062.09 | 1,702.98 | 516.21 | 529.09 | 620.6 | 1.02× |
| 128 | Ascending | 207 | **28.59** | 123.97 | 2,081.01 | 1,481.26 | 134.92 | 378.33 | 120.1 | 4.20× |
| 128 | Descending | 340.37 | **35.21** | 716.18 | 2,089.83 | 1,683.97 | 156.69 | 525.88 | 111.3 | 3.16× |
| 128 | NearlySorted | 247.02 | 353.11 | 379.2 | 2,074.43 | 1,601.52 | **210.43** | 593.56 | 383.2 | 1.09× |
| 1,024 | Random | 6,251.75 | **3,771.68** | 8,130.5 | 10,965.36 | 12,389.39 | 7,341.52 | 8,222.58 | 3,765.6 | 1.00× |
| 1,024 | Ascending | 2,279.57 | **129.91** | 927.06 | 10,820.94 | 8,578.41 | 1,830.77 | 3,097.54 | 772.7 | 5.95× |
| 1,024 | Descending | 4,542.73 | **205.24** | 3,526.25 | 10,593.15 | 11,469.69 | 2,138.62 | 5,794.91 | 742.3 | 3.62× |
| 1,024 | NearlySorted | 3,465.9 | **1,764.35** | 4,482.96 | 10,708.34 | 10,298.24 | 2,466.63 | 7,942.67 | 3,786.1 | 2.15× |
| 10,000 | Random | 332,893.79 | **33,491.41** | 390,943.87 | 105,563.68 | 304,979.16 | 161,836.26 | 462,126.68 | 33,858.2 | 1.01× |
| 10,000 | Ascending | 32,649.85 | **1,605.43** | 8,930.65 | 102,518.8 | 135,450.86 | 34,798.62 | 36,738.32 | 7,287.8 | 4.54× |
| 10,000 | Descending | 61,926.44 | **2,396.97** | 38,331.6 | 103,175.95 | 161,518.31 | 36,380.18 | 77,182.57 | 6,872.4 | 2.87× |
| 10,000 | NearlySorted | 57,072.38 | **19,721.34** | 59,169.74 | 104,052.89 | 172,342.84 | 31,736.37 | 129,963.44 | 33,160.3 | 1.68× |

#### int

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp | Anoprsst | SortingNetworksMergeSort | Previous Sortz | Speedup vs previous |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 128 | Random | 392.16 | **100.4** | 642.93 | 958.54 | 1,375.2 | 666.11 | 463.37 | 117.34 | 1.17× |
| 128 | Ascending | 210.92 | **16.17** | 100.37 | 964.36 | 1,380.41 | 73.32 | 356.27 | 60.61 | 3.75× |
| 128 | Descending | 258.91 | **21.65** | 652.14 | 963.03 | 1,377.83 | 75.55 | 403.84 | 78.21 | 3.61× |
| 128 | NearlySorted | 203.32 | **101.26** | 290.85 | 954.44 | 1,378.32 | 213.84 | 442.57 | 117.74 | 1.16× |
| 1,024 | Random | 5,560.85 | **1,770.74** | 7,722.56 | 4,543.7 | 5,857.31 | 7,789.59 | 7,240.37 | 1,802.49 | 1.02× |
| 1,024 | Ascending | 2,256.94 | **67.78** | 737.11 | 4,592.4 | 5,943.35 | 1,380.15 | 2,652.61 | 322.23 | 4.75× |
| 1,024 | Descending | 3,763.65 | **107.72** | 2,987.24 | 4,555.39 | 5,931.56 | 1,370.5 | 4,960.69 | 427.69 | 3.97× |
| 1,024 | NearlySorted | 2,588.96 | **1,477.08** | 3,942.82 | 4,558.72 | 5,906.25 | 2,415.89 | 7,045.72 | 1,798.33 | 1.22× |
| 10,000 | Random | 239,890.5 | **33,568.49** | 285,472.08 | 46,029.3 | 54,457.33 | 103,745.31 | 252,353.86 | 33,832.99 | 1.01× |
| 10,000 | Ascending | 27,255.72 | **758.68** | 6,727.22 | 47,771.36 | 57,362.06 | 29,518.12 | 28,952.3 | 2,928.78 | 3.86× |
| 10,000 | Descending | 49,470.39 | **1,004.26** | 29,991.37 | 47,787.54 | 57,339.09 | 30,414.14 | 67,570.81 | 3,896.45 | 3.88× |
| 10,000 | NearlySorted | 41,163.99 | **14,783.3** | 53,251.73 | 46,980.17 | 55,184.18 | 36,977.34 | 116,905.29 | 33,306.78 | 2.25× |

#### long

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp | Anoprsst | SortingNetworksMergeSort | Previous Sortz | Speedup vs previous |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 128 | Random | **386.77** | 599.82 | 635.44 | 1,965.3 | 2,757.44 | 688.37 | 640.43 | 378.05 | 0.63× |
| 128 | Ascending | 220.41 | **26.38** | 104.25 | 1,937.4 | 2,728.95 | 73.65 | 348.72 | 70.49 | 2.67× |
| 128 | Descending | 274.18 | **36.84** | 639.93 | 1,940.39 | 2,749.68 | 79.55 | 509.59 | 94.09 | 2.55× |
| 128 | NearlySorted | 219.32 | 594.66 | 297.2 | 1,971.35 | 2,746.87 | **216.55** | 524.19 | 371.79 | 0.63× |
| 1,024 | Random | 5,573.87 | **2,573.6** | 7,880.95 | 9,279.12 | 10,213.27 | 7,414.35 | 8,822.03 | 3,796.83 | 1.48× |
| 1,024 | Ascending | 2,352.47 | **132.72** | 768.89 | 10,109.35 | 10,009.45 | 1,173.54 | 2,812.53 | 404.53 | 3.05× |
| 1,024 | Descending | 3,537.17 | **224.4** | 3,491.52 | 9,397.16 | 10,066.6 | 1,201.21 | 5,633.46 | 477.04 | 2.13× |
| 1,024 | NearlySorted | 2,703.39 | **1,611.12** | 3,908.3 | 9,268.76 | 9,995.15 | 2,110.8 | 7,103.89 | 3,751.45 | 2.33× |
| 10,000 | Random | 233,078.54 | **34,067.3** | 278,786.59 | 91,445.43 | 86,394.73 | 108,945.2 | 374,870.19 | 32,043.82 | 0.94× |
| 10,000 | Ascending | 28,506.44 | **1,753.5** | 7,323.69 | 92,809.39 | 87,377.95 | 28,997.78 | 33,862.04 | 3,680.59 | 2.10× |
| 10,000 | Descending | 52,776.26 | **2,366.43** | 36,198.47 | 92,844.15 | 86,858.71 | 29,575.23 | 74,083.45 | 4,181.01 | 1.77× |
| 10,000 | NearlySorted | 41,847.83 | **16,065.2** | 56,610.89 | 91,191.06 | 86,600.73 | 33,598.31 | 118,714.48 | 33,169.37 | 2.06× |

#### string

| Count | Pattern | .NET List.Sort | Sortz | YamSort | Celerity | HPCsharp | Anoprsst | SortingNetworksMergeSort | Previous Sortz | Speedup vs previous |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 128 | Random | 2,552.3 | **511.7** | 16,749.6 | 1,329.7 | 2,457.3 | 1,994.1 | 2,761.7 | 587.4 | 1.15× |
| 128 | Ascending | 993.4 | 199.6 | 2,685.8 | **155.9** | 1,316.3 | 355.8 | 2,335 | 247.5 | 1.24× |
| 128 | Descending | 1,613.5 | **406.7** | 5,666 | 1,162.1 | 1,609 | 526.6 | 2,653.7 | 414.3 | 1.02× |
| 128 | NearlySorted | 1,162.1 | 514 | 8,641.5 | 1,244.5 | 1,152 | **488.3** | 2,787.1 | 584.6 | 1.14× |
| 1,024 | Random | 30,251.8 | **5,976** | 236,835.8 | 12,799.8 | 30,259.4 | 23,691.2 | 34,511.4 | 6,280.3 | 1.05× |
| 1,024 | Ascending | 14,098.9 | 2,377 | 23,398.3 | **2,196.1** | 14,067.5 | 6,218.3 | 19,487.2 | 2,945.5 | 1.24× |
| 1,024 | Descending | 25,666.7 | **3,732.6** | 36,446.2 | 17,725 | 25,300 | 7,948.6 | 25,741.1 | 4,357.6 | 1.17× |
| 1,024 | NearlySorted | 16,533.5 | **6,178.2** | 81,631.2 | 14,363.5 | 16,463.8 | 9,361.3 | 31,303.3 | 6,436.2 | 1.04× |
| 10,000 | Random | 883,804.4 | **71,165.7** | 3,428,782 | 366,087.4 | 888,877.2 | 264,482.8 | 1,086,638 | 68,954.5 | 0.97× |
| 10,000 | Ascending | 288,532.1 | **39,536.6** | 260,036.3 | 42,960.9 | 288,352.1 | 91,997.9 | 384,698.6 | 65,441.9 | 1.66× |
| 10,000 | Descending | 497,324.2 | **57,067.9** | 397,917.6 | 404,139.5 | 497,741 | 123,885.6 | 535,468 | 76,474.3 | 1.34× |
| 10,000 | NearlySorted | 340,952 | **77,443.5** | 1,075,885.9 | 441,660.3 | 341,240.4 | 120,357 | 814,180.5 | 75,492.5 | 0.97× |

### Latest managed allocation results

| Implementation | Zero reported allocation | Nonzero reported allocation |
|---|---:|---:|
| DefaultListSort | 36 | 12 |
| SortzSort | 48 | 0 |
| YamSort | 48 | 0 |
| CeleritySorting | 48 | 0 |
| HPCsharp | 0 | 48 |
| Anoprsst | 32 | 16 |
| SortingNetworksMergeSort | 0 | 48 |

A dash in the BenchmarkDotNet Allocated column is counted as zero reported managed allocation, not as proof of no scratch memory or allocation-free cold starts.

## Historical competitor comparison (November 25, 2026)

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
