# Toy benchmark results

This is a **toy microbenchmark, not a production compiler benchmark**. It uses `Stopwatch`, five samples, median reporting and a warm-up loop. Tiered JIT, GC scheduling, CPU frequency and host load are not controlled, so small differences — especially warm-cache numbers — must not be overinterpreted.

The numbers below come from the final candidate-bound run checked in as [`evidence/final-candidate-run.txt`](evidence/final-candidate-run.txt); the benchmark-only extract is [`evidence/benchmark-final.txt`](evidence/benchmark-final.txt).

| Case | Median ns/op | Median B/op |
|---|---:|---:|
| direct typed call | 110.2 | 0.0 |
| ordinary typed registry | 158.5 | 0.0 |
| semantic query warm | 70.4 | 32.0 |
| semantic query cold (`BasicRange`) | 567.0 | 800.0 |
| semantic cold, 1 constant provider | 310.7 | 800.0 |
| semantic cold, 2 constant providers | 360.9 | 800.0 |
| semantic cold, 10 constant providers | 853.2 | 1040.0 |
| semantic cold, 100 constant providers | 3901.1 | 2904.0 |
| deep `A -> B -> C -> D` cold | 3697.3 | 1776.0 |
| deep `A -> B -> C -> D` warm | 35.5 | 0.0 |

The robust signal is not the exact warm-cache ranking. It is that cold generic dispatch allocates and grows more expensive with provider count/nested queries, while direct and conventional registry calls allocate nothing in this harness. Memoization makes repeated queries cheap enough to be interesting, but it does not erase the cold-path cost.
