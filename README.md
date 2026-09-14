# Typed Semantic Queries — compiler architecture experiment

[![CI](https://github.com/Misha1302/typed-semantic-queries/actions/workflows/ci.yml/badge.svg)](https://github.com/Misha1302/typed-semantic-queries/actions/workflows/ci.yml)

A runnable .NET 10 experiment that asks one narrow compiler-architecture question:

> Can independently authored compiler components exchange small typed semantic results so that a new provider improves existing optimizations without pairwise integration?

The answer is **yes, but the generic runtime is less important than the contracts and lifecycle discipline**. The final verdict is [`GO_WITH_SIMPLIFICATIONS`](docs/VERDICT.md).

## The experiment

```text
BasicRange ─┐
LoopRange ──┼─> Range(i, P) ─┐
            │                 ├─> BoundsBridge ─> InBounds(a,i,P) ─> BoundsCheckOptimizer
ArrayLength ─> Length(a, P) ─┘

same Range(i, P) ────────────────────────────────────────────────> BranchSimplifier
```

`LoopRangeProvider` is added later. `BoundsCheckOptimizer`, `BoundsBridge`, and the original providers are not changed, yet an existing bounds check becomes removable. The same provider also strengthens a second consumer.

## Why this repository exists

This is not a framework proposal and not a Universal Toolchain integration. It is an executable falsification-oriented MVP: a strong conventional typed-services baseline sits next to the semantic-query version, and both are tested on the same extension scenario.

## Run

Requires .NET 10 SDK.

```bash
./scripts/run-all.sh
```

Or individually:

```bash
dotnet build SemanticQueryMvp.sln -c Release
dotnet test SemanticQueryMvp.sln -c Release
dotnet run --project demo/SemanticQueryDemo -c Release
dotnet run --project benchmarks/SemanticQueries.Microbenchmarks -c Release
```

## What is implemented

- typed `QuerySpec<TKey,TValue>`-style contracts;
- explicit startup composition (`StaticPlan`), no reflection on query hot paths;
- revision-scoped `SemanticSession` with memoization;
- cycle detection and deterministic stale-session failure;
- query-owned multiple-provider semantics (`Range` intersection, `Length` conflict);
- independent `Range + Length -> InBounds` bridge;
- loop/induction-variable provider that strengthens old consumers;
- branch probability as heuristic knowledge, separated from correctness proofs;
- a strong conventional typed-services baseline;
- runnable demo, tests, toy microbenchmark, and CI.

## Read next

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — the small runtime and boundaries
- [`docs/EXPERIMENTS.md`](docs/EXPERIMENTS.md) — acceptance/adversarial experiments
- [`docs/BASELINE_COMPARISON.md`](docs/BASELINE_COMPARISON.md) — what ordinary typed services already solve
- [`docs/VERDICT.md`](docs/VERDICT.md) — why the conclusion is `GO_WITH_SIMPLIFICATIONS`

## Non-goals

No semantic database, reactive graph, Datalog/SMT runtime, generic fixed-point engine, reflection-based plugin discovery, or automatic cross-revision dependency graph. Those abstractions are intentionally excluded until a concrete experiment proves they are needed.

## License

MIT.
