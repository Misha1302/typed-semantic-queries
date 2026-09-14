# Typed Semantic Queries — compiler architecture experiment

[![CI](https://github.com/Misha1302/typed-semantic-queries/actions/workflows/ci.yml/badge.svg)](https://github.com/Misha1302/typed-semantic-queries/actions/workflows/ci.yml)

A runnable .NET 10 experiment for one compiler-architecture question:

> Can independently authored compiler components exchange small typed semantic results so that a new provider improves existing optimizations without pairwise integration?

**Result: yes — but a generic semantic runtime is not required for the main extensibility win.** The measured conclusion is [`GO_WITH_SIMPLIFICATIONS`](docs/VERDICT.md).

## Main experiment

```text
BasicRange ----+
LoopRange -----+--> Range(i, P) --+
                                  +--> BoundsBridge --> InBounds(a,i,P) --> BoundsCheckOptimizer
ArrayLength ------> Length(a, P) -+

same Range(i, P) -----------------------------------------------> BranchSimplifier
```

`LoopRangeProvider` is installed later. The existing optimizer, bridge and old producers do not change, yet the same loop goes from **keep bounds check** to **remove bounds check**. The same provider also strengthens `BranchSimplifier`.

A deliberately strong conventional typed-services baseline passes the same independent-extension test. That counter-result is central to the verdict.

## What is implemented

- typed query contracts with query-owned combination and validation;
- query-type-safe provider dispatch (different contracts may share key/value CLR types safely);
- startup-built `StaticPlan`; no reflection/plugin discovery on the query hot path;
- revision-scoped, memoized, single-threaded `SemanticSession`;
- deterministic conflict, stale-session and query-cycle failures;
- `Range`, `Length`, `InBounds`, `BranchProbability` and `Alignment` queries;
- separate `LoopAnalysisEngine` exported through `LoopRangeProvider`;
- proof/heuristic separation: branch probability can change layout but not legality;
- one additional slice: `Effects + NoAlias -> CanHoist -> TinyLicmPass`;
- package-separated conventional baseline;
- **41 xUnit tests**, including all 24 provider-registration permutations;
- runnable demo and an allocation-aware toy benchmark;
- a read-only integration spike against the actual Universal Toolchain repository.

## Run everything

Requires the .NET 10 SDK.

```bash
./scripts/run-all.sh
```

Or run the parts separately:

```bash
dotnet restore SemanticQueryMvp.sln
dotnet build SemanticQueryMvp.sln -c Release --no-restore
dotnet test SemanticQueryMvp.sln -c Release --no-build
dotnet run --project demo/SemanticQueryDemo -c Release --no-build
dotnet run --project benchmarks/SemanticQueries.Microbenchmarks -c Release --no-build
```

## Demo

The executable demonstration covers the required metamorphic case, heuristic layout, conflict handling and the extra LICM slice:

```text
=== Without LoopRangeProvider ===
Optimization: bounds check kept

=== With LoopRangeProvider ===
Optimization: bounds check removed

=== Conflicting Length providers ===
Result: Conflict
Bounds check: kept

=== Tiny LICM ===
load p: hoisted
```

## Evidence and design notes

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — runtime boundary, lifecycle and guarantees
- [`docs/EXPERIMENTS.md`](docs/EXPERIMENTS.md) — executed acceptance/adversarial matrix
- [`docs/BASELINE_COMPARISON.md`](docs/BASELINE_COMPARISON.md) — honest comparison with ordinary typed services
- [`docs/BENCHMARK_RESULTS.md`](docs/BENCHMARK_RESULTS.md) — measured toy numbers and limitations
- [`docs/UT_INTEGRATION.md`](docs/UT_INTEGRATION.md) — mapping to actual Universal Toolchain owners
- [`docs/VERDICT.md`](docs/VERDICT.md) — final architectural decision

## Non-goals

The project intentionally does **not** contain a semantic database, reactive dependency graph, Datalog/SMT runtime, generic fixed-point engine, universal evidence model, source-generated dispatcher, or automatic cross-revision transfer. Those abstractions need their own evidence before entering the design.

## License

MIT.
