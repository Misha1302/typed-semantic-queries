# Baseline comparison

The baseline is intentionally strong: ordinary typed service interfaces plus an explicit registry. It also accepts an independently authored loop-range service and improves bounds-check elimination without changing the consumer.

That means the generic semantic-query runtime does **not** win merely because it is extensible. Its incremental value is narrower: one place for query-owned merge/conflict semantics, revision-scoped memoization, and cycle/staleness checks.

If an existing compiler already has a good service registry and analysis cache, adopting the contracts and lifecycle conventions is likely cheaper than adding a parallel "semantic world" abstraction.
