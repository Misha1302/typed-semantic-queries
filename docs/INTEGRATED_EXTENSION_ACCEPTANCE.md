# Integrated external extension acceptance

`bash ./scripts/test-integrated-extension.sh` is the strongest zero-core-edit acceptance scenario in this repository.

The script builds `ExternalModules.ExactValue` separately from `SemanticQueries.IntegratedExtension.Acceptance`. The acceptance host has no compile-time reference to the external assembly and loads it only after both projects have been built.

The external package owns all of the new semantic concept:

- `ExactValueQuery`, a typed query contract absent from `SemanticContracts`;
- `ExactValueAnalysis`, mutable package-owned derived state with its own revision;
- `ExactValueProvider`, which exposes that state and implements `ISemanticRevisionSource`;
- `RangeFromExactValueProvider`, a typed bridge from the new contract into the old public `RangeQuery` contract.

The host keeps the existing `ArrayLengthProvider`, `BoundsBridgeProvider` and `BoundsCheckOptimizer` unchanged. Before opt-in they keep the bounds check. After explicit host opt-in to the independently built providers, the old optimizer removes the check using knowledge that originated in the new query contract.

The acceptance also checks that the host and old bridge/consumer assemblies do not reference the external module, that the external module does not reference the old producer/bridge/consumer packages, that reversing external-provider registration order does not change behavior, and that mutation of `ExactValueAnalysis` makes an already-created `SemanticSession` stale while a fresh session sees the new value.

For this open extension path the expected cost is `E_core = 0`, `E_consumer = 0`, `E_host > 0`, `C = 0`: explicit host composition is allowed, but framework/core and old producer/bridge/consumer source are not edited for the new semantic participant.
