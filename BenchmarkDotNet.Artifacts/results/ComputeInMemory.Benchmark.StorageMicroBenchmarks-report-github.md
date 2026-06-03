```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8457)
AMD Ryzen 7 6800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.312
  [Host]   : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=10  LaunchCount=1  
WarmupCount=3  

```
| Method                                 | DataSize | Mean          | Error         | StdDev        | P95           | Rank | Gen0     | Gen1     | Gen2     | Allocated |
|--------------------------------------- |--------- |--------------:|--------------:|--------------:|--------------:|-----:|---------:|---------:|---------:|----------:|
| **&#39;StorageBlock zero-copy span access&#39;**   | **1024**     |      **39.04 ns** |      **1.179 ns** |      **0.780 ns** |      **40.35 ns** |    **1** |        **-** |        **-** |        **-** |         **-** |
| &#39;ZeroCopyAccessor copy between blocks&#39; | 1024     |     132.51 ns |      3.305 ns |      2.186 ns |     135.21 ns |    3 |        - |        - |        - |         - |
| &#39;Allocate storage block&#39;               | 1024     |     517.66 ns |     28.406 ns |     16.904 ns |     540.43 ns |    5 |   0.1516 |        - |        - |    1272 B |
| &#39;ZeroCopyAccessor reinterpret cast&#39;    | 1024     |     187.48 ns |     13.410 ns |      8.870 ns |     196.03 ns |    4 |   0.1252 |   0.0005 |        - |    1048 B |
| &#39;ZeroCopyAccessor pin and get memory&#39;  | 1024     |      60.12 ns |      1.896 ns |      1.128 ns |      61.54 ns |    2 |        - |        - |        - |         - |
| **&#39;StorageBlock zero-copy span access&#39;**   | **65536**    |      **38.63 ns** |      **0.445 ns** |      **0.294 ns** |      **39.07 ns** |    **1** |        **-** |        **-** |        **-** |         **-** |
| &#39;ZeroCopyAccessor copy between blocks&#39; | 65536    |   1,815.91 ns |     37.011 ns |     24.480 ns |   1,848.67 ns |    6 |        - |        - |        - |         - |
| &#39;Allocate storage block&#39;               | 65536    |   5,079.03 ns |  1,184.628 ns |    783.558 ns |   6,148.79 ns |    7 |   7.8049 |        - |        - |   65784 B |
| &#39;ZeroCopyAccessor reinterpret cast&#39;    | 65536    |   8,471.01 ns |  2,351.966 ns |  1,555.680 ns |  10,590.21 ns |    8 |   7.8125 |   1.9379 |        - |   65560 B |
| &#39;ZeroCopyAccessor pin and get memory&#39;  | 65536    |      58.87 ns |      1.808 ns |      1.076 ns |      60.47 ns |    2 |        - |        - |        - |         - |
| **&#39;StorageBlock zero-copy span access&#39;**   | **1048576**  |      **38.26 ns** |      **0.401 ns** |      **0.265 ns** |      **38.69 ns** |    **1** |        **-** |        **-** |        **-** |         **-** |
| &#39;ZeroCopyAccessor copy between blocks&#39; | 1048576  |  42,922.14 ns |  1,199.760 ns |    793.567 ns |  43,852.58 ns |    9 |        - |        - |        - |         - |
| &#39;Allocate storage block&#39;               | 1048576  |  55,915.50 ns |  5,373.700 ns |  3,197.803 ns |  59,770.80 ns |   10 | 105.2856 | 105.1025 | 105.1025 | 1049267 B |
| &#39;ZeroCopyAccessor reinterpret cast&#39;    | 1048576  | 337,593.32 ns | 27,667.793 ns | 18,300.532 ns | 365,051.70 ns |   11 | 124.5117 | 124.5117 | 124.5117 | 1048638 B |
| &#39;ZeroCopyAccessor pin and get memory&#39;  | 1048576  |      63.38 ns |      5.980 ns |      3.955 ns |      67.89 ns |    2 |        - |        - |        - |         - |
