# Architecture

The experiment keeps the shared layer deliberately small. A query is a typed contract with its own combination and validation rules; providers are registered once in a `StaticPlan`; a `SemanticSession` is scoped to exactly one IR revision.

```text
consumer -> Query<TKey,TValue> -> SemanticSession -> StaticPlan -> provider(s)
                                  |                  |
                                  |                  +-- providers selected at startup
                                  +-- memoization / cycle / stale-revision checks
```

## What the runtime owns

`SemanticSession` owns dispatch, revision checks, memoization and cycle detection. `StaticPlan` owns the already-composed provider list. It does not discover plugins, scan assemblies, run a fixed-point solver, or track a dependency DAG.

Provider lookup is keyed by the **query contract type**, not merely by key/value types. This matters: two independent queries can both be `string -> int` without seeing each other's providers. `QueryTypesWithSameKeyAndValue_DoNotShareProviders` is the regression test for that boundary.

The session is single-threaded in this MVP. Nothing in the runtime claims provider purity: arbitrary C# providers can still read clocks, globals or mutable state. Determinism therefore depends on the provider contract and tests, not on an automatic purity proof.

## Query-owned semantics

- `Range` intersects sound answers and returns `Conflict` for disjoint answers.
- `Length` requires exact agreement.
- `InBounds`, `NoAlias` and `CanHoist` are proof-oriented; absence of a proof is `Unknown`.
- `BranchProbability` is heuristic, validates `[0, 1]`, and rejects multiple providers rather than picking one by registration order.
- `Effects` is single-provider in the experiment.
- `Alignment` is an extra query used to compare the cost of extending both architectures.

This keeps legality knowledge separate from heuristic knowledge: `P(condition)=0.999999` may affect layout, but cannot remove a bounds check.

## Revision model

Every IR mutation increments `CompilationUnit.Revision`. `StaticPlan` also carries a composition revision, and providers with mutable analysis-owned state expose `ISemanticRevisionSource`. A session snapshots all of those revisions. Every query checks them before dispatch; changing IR, provider composition, or tracked provider state makes the old session throw `StaleSemanticSessionException`.

Legacy providers without an explicit stability or revision contract remain source-compatible but make the plan cache-unsafe, so the session does not memoize their results. Providers that are immutable for a session can opt into memoization with `IStableQueryProvider`. The model is intentionally coarse: there is no cross-revision cache reuse and no automatic transfer through rewrites.

## Engines stay engines

`LoopAnalysisEngine` computes induction-variable information. `LoopRangeProvider` exposes only the small public result `Range(i, P)`. The query runtime does not absorb loop discovery or a worklist algorithm.

The same rule applies to analyses that naturally need lattices or fixed points: liveness, dataflow, recursive inference and Attributor-like analyses should remain specialized engines until a separate experiment proves otherwise.

## Additional vertical slice

The one additional optimization is deliberately different from bounds analysis:

```text
Effects(loop) + NoAlias(load, write, loop)
                 |
                 v
              CanHoist(load, loop)
                 |
                 v
              TinyLicmPass
```

`LoopEffectsAnalysis` owns the derived loop-write side table and its revision; `MiniCompiler.IR` does not store that analysis result. `LoopEffectsProvider` exposes the state through `EffectsQuery` and lets `SemanticSession` detect stale analysis state through `ISemanticRevisionSource`.

`CanHoistBridgeProvider` fails closed if effects are unknown, a write targets the loaded location, or any required no-alias proof is missing.
