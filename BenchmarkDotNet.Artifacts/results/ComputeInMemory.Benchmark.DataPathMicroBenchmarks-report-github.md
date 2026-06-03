```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8457)
AMD Ryzen 7 6800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.312
  [Host]   : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=10  LaunchCount=1  
WarmupCount=3  

```
| Method                                      | DataSize | Mean        | Error      | StdDev     | P95         | Allocated |
|-------------------------------------------- |--------- |------------:|-----------:|-----------:|------------:|----------:|
| **&#39;NativeMemoryBridge copy managed to native&#39;** | **1024**     |    **14.09 ns** |   **1.547 ns** |   **1.023 ns** |    **15.59 ns** |         **-** |
| &#39;NativeMemoryBridge copy native to managed&#39; | 1024     |    16.72 ns |   0.532 ns |   0.317 ns |    17.14 ns |         - |
| &#39;PIM simulate vector add&#39;                   | 1024     |   129.79 ns |   4.373 ns |   2.892 ns |   133.98 ns |         - |
| &#39;SharedMemory write&#39;                        | 1024     |    17.82 ns |   0.479 ns |   0.317 ns |    18.29 ns |         - |
| &#39;SharedMemory read&#39;                         | 1024     |    16.75 ns |   0.375 ns |   0.196 ns |    16.95 ns |         - |
| **&#39;NativeMemoryBridge copy managed to native&#39;** | **65536**    | **1,451.19 ns** |  **34.643 ns** |  **22.914 ns** | **1,487.81 ns** |         **-** |
| &#39;NativeMemoryBridge copy native to managed&#39; | 65536    | 1,455.52 ns |  42.628 ns |  28.195 ns | 1,495.25 ns |         - |
| &#39;PIM simulate vector add&#39;                   | 65536    | 8,408.44 ns | 860.440 ns | 512.034 ns | 9,286.66 ns |         - |
| &#39;SharedMemory write&#39;                        | 65536    | 1,415.09 ns |  29.295 ns |  15.322 ns | 1,432.72 ns |         - |
| &#39;SharedMemory read&#39;                         | 65536    | 1,399.65 ns |  36.790 ns |  24.334 ns | 1,434.66 ns |         - |
