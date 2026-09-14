# Architecture

The experiment deliberately keeps the shared layer small: typed query contracts, explicit providers, a startup-built `StaticPlan`, and a revision-scoped `SemanticSession`.

```text
consumer -> typed query -> SemanticSession -> StaticPlan -> provider(s)
                              |                 |
                              |                 +-- contract-owned combination
                              +-- memoization / cycle / stale-revision guards
```

`Range`, `Length`, `InBounds`, and `BranchProbability` are separate contracts. `Range` combines sound providers by intersection; `Length` requires exact agreement; `BranchProbability` is heuristic and never substitutes for proof. Structured analyses such as loop discovery stay outside the runtime and export only small typed results.
