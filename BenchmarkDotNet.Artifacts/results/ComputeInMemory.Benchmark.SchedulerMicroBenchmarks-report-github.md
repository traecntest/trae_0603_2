```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8457)
AMD Ryzen 7 6800H with Radeon Graphics, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.312
  [Host]   : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2

Job=ShortRun  LaunchCount=1  

```
| Method                           | IterationCount | WarmupCount | Mean     | Error     | StdDev   | Gen0    | Gen1    | Allocated |
|--------------------------------- |--------------- |------------ |---------:|----------:|---------:|--------:|--------:|----------:|
| &#39;DAG topological sort 100 tasks&#39; | 3              | 1           | 74.67 μs | 59.003 μs | 3.234 μs | 25.3906 |  8.5449 | 207.74 KB |
| &#39;DAG cycle detection 100 tasks&#39;  | 3              | 1           | 71.51 μs | 32.154 μs | 1.762 μs | 25.1465 |  9.0332 |  205.6 KB |
| &#39;DAG get ready tasks 100 tasks&#39;  | 3              | 1           | 68.36 μs | 86.056 μs | 4.717 μs | 24.6582 | 12.2681 |  201.7 KB |
| &#39;DAG topological sort 100 tasks&#39; | 5              | 2           | 79.22 μs | 13.028 μs | 3.383 μs | 25.3906 |  8.5449 | 207.74 KB |
| &#39;DAG cycle detection 100 tasks&#39;  | 5              | 2           | 74.37 μs |  8.661 μs | 1.340 μs | 25.1465 |  9.0332 |  205.6 KB |
| &#39;DAG get ready tasks 100 tasks&#39;  | 5              | 2           | 63.89 μs |  4.302 μs | 1.117 μs | 24.6582 | 12.2070 |  201.7 KB |
