```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8457)
AMD Ryzen 7 6800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.312
  [Host]   : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=2  

```
| Method                           | Mean      | Error     | StdDev   | Gen0    | Gen1    | Allocated |
|--------------------------------- |----------:|----------:|---------:|--------:|--------:|----------:|
| &#39;DAG topological sort 100 tasks&#39; | 104.26 μs | 18.025 μs | 4.681 μs | 25.3906 |  8.5449 | 207.74 KB |
| &#39;DAG cycle detection 100 tasks&#39;  |  98.00 μs |  4.351 μs | 0.673 μs | 25.1465 |  9.0332 |  205.6 KB |
| &#39;DAG get ready tasks 100 tasks&#39;  |  86.34 μs |  5.933 μs | 1.541 μs | 24.6582 | 12.2070 |  201.7 KB |
