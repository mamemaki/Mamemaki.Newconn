## Overview
We are measuring server performance for several libraries:

- Kestrel
- Newconn
- SuperSocket

The purpose of this benchmark is to measure throughput and memory allocation under workloads. Measurements are performed as follows.

1.  Start the Echo server in the target library.
1.  Connect six clients to the Echo server.
1.  Start the measurement.
1.  Each client sends 54-byte message 1000 times in parallel and receives the response.
1.  End the measurement and perform cleanup.

## Results

| Method | Framework   | Mean     | Error    | StdDev   | Allocated |
|------- |------------ |---------:|---------:|---------:|----------:|
| Run    | Kestrel     | 60.80 ms | 0.202 ms | 0.179 ms |   1.88 MB |
| Run    | Newconn     | 60.79 ms | 0.214 ms | 0.190 ms |   1.88 MB |
| Run    | SuperSocket | 65.75 ms | 0.502 ms | 0.469 ms |   8.02 MB |
