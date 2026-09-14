# Baseline comparison

The baseline is intentionally strong. It uses ordinary typed interfaces, explicit package-level services and constructor wiring; it does not contain a generic query runtime.

```text
IRangeService[] -> RangeRegistry --+
                                    +-> BaselineBoundsBridge -> BaselineBoundsOptimizer
ILengthService --------------------+
```

`Baseline.BasicRange`, `Baseline.LoopRange`, `Baseline.ArraySemantics` and `Baseline.BoundsBridge` are separate projects. This makes the comparison about architecture rather than namespaces inside one assembly.

## Observable edit surface

| Change | Semantic-query version | Conventional baseline | Existing consumers changed? | Core/runtime changed? |
|---|---|---|---|---|
| Add better LoopRange provider | new `Packages.LoopRange` provider + one composition registration | new `Baseline.LoopRange` service + one registry entry | No / No | No / No |
| Add independent bounds bridge | new `Packages.BoundsBridge` project consuming public `Range`/`Length` contracts | new `Baseline.BoundsBridge` project consuming `RangeRegistry`/`ILengthService` | No / No | No / No |
| Add `Alignment` semantic/service | one query contract + provider project | one typed service interface + provider project | No consumer required for probe | generic runtimes unchanged |

The main extensibility claim is therefore a **tie**, not a semantic-query victory: both approaches let an independently authored `LoopRange` implementation improve an old optimizer without modifying it.

## Boilerplate snapshot

The measured source files in this candidate are:

| Owner | LOC |
|---|---:|
| `SemanticQueries.Core/Core.cs` | 136 |
| `SemanticContracts/Contracts.cs` | 130 |
| semantic `LoopRange` engine + provider | 37 |
| semantic `BoundsBridge` | 23 |
| `Baseline.DirectServices/Baseline.cs` | 58 |
| baseline `LoopRange` | 20 |
| baseline `BoundsBridge` | 14 |
| semantic `Alignment` provider | 14 |
| baseline `Alignment` provider | 10 |

LOC is not a quality metric; it only makes the one-time abstraction cost visible. The semantic layer carries more generic machinery.

## What the semantic layer actually adds

The meaningful incremental value is shared behavior that otherwise has to be reimplemented or conventionally coordinated:

- one `Unknown / Known<T> / Conflict` protocol;
- query-owned merge/conflict/validation semantics;
- revision-scoped memoization;
- deterministic stale-session rejection;
- cycle detection across nested queries;
- a uniform way for bridge providers to issue other typed queries.

The baseline remains simpler for direct service graphs and has no generic cold-dispatch allocation cost. If a compiler already owns a good DI/registry and analysis-cache lifecycle, reusing those owners plus typed semantic contracts is preferable to introducing a second "semantic world".

## Conclusion

The experiment rejects the claim that a generic semantic runtime is required for independent extensibility. The useful result is narrower: **typed contracts and disciplined lifecycle semantics are worth keeping; the hosting mechanism should usually reuse the compiler's existing composition infrastructure.**
