```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8457)
AMD Ryzen 7 6800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.312
  [Host]   : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2

Job=ShortRun  LaunchCount=1  

```
| Method                                      | IterationCount | WarmupCount | DataSize | Mean        | Error        | StdDev     | P95         | Allocated |
|-------------------------------------------- |--------------- |------------ |--------- |------------:|-------------:|-----------:|------------:|----------:|
| **&#39;NativeMemoryBridge copy managed to native&#39;** | **10**             | **3**           | **1024**     |    **10.43 ns** |     **0.449 ns** |   **0.297 ns** |    **10.85 ns** |         **-** |
| &#39;NativeMemoryBridge copy native to managed&#39; | 10             | 3           | 1024     |    12.51 ns |     0.525 ns |   0.347 ns |    12.97 ns |         - |
| &#39;PIM simulate vector add&#39;                   | 10             | 3           | 1024     |    97.14 ns |     2.379 ns |   1.574 ns |    99.37 ns |         - |
| &#39;SharedMemory write&#39;                        | 10             | 3           | 1024     |    12.58 ns |     0.283 ns |   0.187 ns |    12.81 ns |         - |
| &#39;SharedMemory read&#39;                         | 10             | 3           | 1024     |    10.70 ns |     0.507 ns |   0.335 ns |    11.18 ns |         - |
| &#39;NativeMemoryBridge copy managed to native&#39; | 3              | 1           | 1024     |    10.35 ns |     4.967 ns |   0.272 ns |    10.62 ns |         - |
| &#39;NativeMemoryBridge copy native to managed&#39; | 3              | 1           | 1024     |    12.15 ns |     2.981 ns |   0.163 ns |    12.31 ns |         - |
| &#39;PIM simulate vector add&#39;                   | 3              | 1           | 1024     |    97.16 ns |    14.307 ns |   0.784 ns |    97.87 ns |         - |
| &#39;SharedMemory write&#39;                        | 3              | 1           | 1024     |    12.28 ns |     4.953 ns |   0.271 ns |    12.55 ns |         - |
| &#39;SharedMemory read&#39;                         | 3              | 1           | 1024     |    11.77 ns |     0.576 ns |   0.032 ns |    11.80 ns |         - |
| **&#39;NativeMemoryBridge copy managed to native&#39;** | **10**             | **3**           | **65536**    | **1,011.01 ns** |    **27.891 ns** |  **16.597 ns** | **1,036.99 ns** |         **-** |
| &#39;NativeMemoryBridge copy native to managed&#39; | 10             | 3           | 65536    | 1,035.86 ns |    42.236 ns |  27.936 ns | 1,073.01 ns |         - |
| &#39;PIM simulate vector add&#39;                   | 10             | 3           | 65536    | 8,202.31 ns |   182.714 ns |  95.563 ns | 8,294.17 ns |         - |
| &#39;SharedMemory write&#39;                        | 10             | 3           | 65536    | 1,021.32 ns |    31.905 ns |  18.986 ns | 1,048.53 ns |         - |
| &#39;SharedMemory read&#39;                         | 10             | 3           | 65536    |   994.60 ns |    27.733 ns |  18.343 ns | 1,019.13 ns |         - |
| &#39;NativeMemoryBridge copy managed to native&#39; | 3              | 1           | 65536    | 1,000.15 ns |   219.247 ns |  12.018 ns | 1,011.85 ns |         - |
| &#39;NativeMemoryBridge copy native to managed&#39; | 3              | 1           | 65536    | 1,009.02 ns |   486.483 ns |  26.666 ns | 1,035.22 ns |         - |
| &#39;PIM simulate vector add&#39;                   | 3              | 1           | 65536    | 8,512.24 ns | 7,075.062 ns | 387.808 ns | 8,839.27 ns |         - |
| &#39;SharedMemory write&#39;                        | 3              | 1           | 65536    | 1,054.43 ns |   849.279 ns |  46.552 ns | 1,100.29 ns |         - |
| &#39;SharedMemory read&#39;                         | 3              | 1           | 65536    |   996.18 ns |   267.680 ns |  14.672 ns | 1,008.33 ns |         - |
