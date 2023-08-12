```

BenchmarkDotNet v0.13.7, Debian GNU/Linux trixie/sid
Intel Xeon CPU X5650 2.67GHz, 2 CPU, 24 logical and 12 physical cores
.NET SDK 7.0.306
  [Host]     : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT SSE4.2
  Job-INXKYK : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT SSE4.2
  Job-GXGCQI : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT SSE4.2


```
|         Method |        Job | Server | ItemCount | parallel |        Mean |       Error |      StdDev |        Gen0 |        Gen1 |      Gen2 | Allocated |
|--------------- |----------- |------- |---------- |--------- |------------:|------------:|------------:|------------:|------------:|----------:|----------:|
| **RebuildIndexes** | **Job-INXKYK** |  **False** |    **250000** |    **False** | **14,164.0 ms** |    **98.15 ms** |    **91.81 ms** | **189000.0000** |  **95000.0000** | **2000.0000** |   **1.11 GB** |
| RebuildIndexes | Job-GXGCQI |   True |    250000 |    False |  4,863.8 ms |    29.85 ms |    27.92 ms |           - |           - |         - |   1.11 GB |
| **RebuildIndexes** | **Job-INXKYK** |  **False** |    **250000** |     **True** | **24,011.2 ms** | **1,905.70 ms** | **5,280.68 ms** | **200000.0000** | **101000.0000** | **2000.0000** |   **1.11 GB** |
| RebuildIndexes | Job-GXGCQI |   True |    250000 |     True |    738.4 ms |    14.62 ms |    25.61 ms |   1000.0000 |           - |         - |   1.12 GB |
