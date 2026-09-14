# Experiments

All `Actual` entries below come from executable tests or the final candidate run, not from intended behavior.

| Experiment | Hypothesis | Setup | Expected | Actual | Test / evidence | Result | Interpretation |
|---|---|---|---|---|---|---|---|
| Constant Range | A tiny producer can expose exact knowledge | constant `i=5` + BasicRange | `[5,5]` | `[5,5]` | `Range_Constant_IsExact` | PASS | Basic typed query works. |
| Arithmetic Range | Nested typed queries can compose | constants + Add/Mul IR | exact result | exact | `Range_SimpleArithmetic_IsExact` | PASS | Provider composition is not test-name special casing. |
| Multiple Range | sound answers refine monotonically | `[0,100]` + `[20,50]` | intersection | `[20,50]` | `Range_MultipleSoundProviders_AreIntersected` | PASS | Merge law belongs to `Range`. |
| Disjoint Range | incompatible sound claims must fail closed | `[0,10]` + `[20,30]` | conflict | `Conflict` | `Range_DisjointProviders_FailClosedOrConflict` | PASS | Runtime never picks registration winner. |
| Equal Length | exact facts may agree | two providers return 10 | 10 | 10 | `Length_EqualProviders_Agree` | PASS | Multi-provider exact knowledge works. |
| Different Length | disagreement is not refinement | 10 + 11 | conflict | `Conflict` | `Length_DifferentProviders_ReturnConflict` | PASS | Correctness path detects disagreement. |
| Independent bridge | package X can compose public semantics only | Range + Length + BoundsBridge | InBounds proof | proven | `IndependentBridge_WorksWithoutProducerChanges` | PASS | Bridge has no concrete producer references. |
| Better provider | new D improves old C without edits | same loop, composition ± LoopRange | keep -> remove | keep -> remove | `BetterRangeProvider_ImprovesExistingBoundsOptimizer` | PASS | Main acceptance test succeeds. |
| Shared reuse | one new Range improves another old consumer | LoopRange + BranchSimplifier | simplify branch | simplified | `BetterRangeProvider_AlsoImprovesBranchSimplifier` | PASS | Range is reusable, not optimizer-specific. |
| Structured engine | engine may stay specialized | LoopAnalysisEngine -> LoopRangeProvider | `[0,9]` | `[0,9]` | `InductionVariableProvider_InfersLoopRange` | PASS | Runtime receives a small result, not the engine internals. |
| Registration order | composition order is not semantics | deterministic orders + all 24 permutations | same result | same result | `ProviderRegistrationOrder_*` | PASS | No first-provider-wins dependency. |
| Memoization | repeated identical query should not re-run provider | 100 same Range queries | one provider call | one | `SemanticSession_MemoizesRepeatedQuery` | PASS | Session cache works. |
| IR mutation | session must be revision-scoped | mutate constant after query | revision changes | changed | `IRMutation_ChangesRevision` | PASS | Mutation creates new lifecycle boundary. |
| Stale session | cached answer cannot survive mutation | query then mutate then query old session | deterministic failure | throws | `OldSemanticSession_CannotServeNewRevision` | PASS | Old cache is unusable. |
| New revision | fresh session sees changed semantics | 5 -> 105 | `[105,105]` | `[105,105]` | `Mutation_DoesNotReuseOldRange` | PASS | No stale answer reuse. |
| Cycle | ordinary queries must not become generic fixed point | A -> B -> A | error | throws | `QueryCycle_IsDetected` | PASS | Recursive analyses need specialized engines. |
| Length conflict consumer | conflict must protect legality | valid Range + Length 10/11 | keep check | kept | `LengthConflict_DoesNotRemoveBoundsCheck` | PASS | Consumer fails closed. |
| Probability layout | heuristic knowledge may guide non-legality decisions | `P(true)=0.05` | hot fall-through first | hot first | `Probability_ReordersBranchLayout` | PASS | Heuristic query is useful. |
| Probability legality | high probability is not proof | probability + no Range proof | keep check | kept | `Probability_DoesNotRemoveBoundsCheck` | PASS | Proof/heuristic meanings stay separate. |
| Unknown knowledge | absence must be safe | no proof-producing analyses | keep check | kept | `UnknownSemanticKnowledge_FailsClosed` | PASS | Unknown is fail closed. |
| Assembly independence | consumers must not know concrete producers | inspect assembly references | none | none | `Consumer_DoesNotReferenceConcreteProvider` | PASS | Package independence is observable. |
| Query identity | equal CLR key/value types must remain distinct contracts | Alpha/Beta both `string -> int` | isolated providers | isolated | `QueryTypesWithSameKeyAndValue_DoNotShareProviders` | PASS | Dispatch keys on query type. |
| Bad provider value | contract validation should reject malformed semantics | probability 2.4 | exception | throws | `MalformedProbability_IsRejected` | PASS | Validation boundary is deterministic. |
| Additional optimization | different semantics should compose too | Effects + NoAlias -> CanHoist -> LICM | hoist | hoisted | `TinyLicm_HoistsWithEffectsAndNoAlias` | PASS | Model generalizes beyond bounds. |
| LICM unknown alias | missing proof must block transform | write q, no no-alias proof | keep | kept | `TinyLicm_KeepsWhenAliasUnknown` | PASS | Additional slice fails closed. |
| LICM same-location write | direct conflict must block transform | load p + write p | keep | kept | `TinyLicm_KeepsWhenLoopWritesSameLocation` | PASS | Bridge respects effects. |
| Strong baseline | ordinary typed services may solve main scenario too | baseline RangeRegistry + bridge ± LoopRange | extension works | works | `ConventionalBaseline_AlsoAcceptsIndependentLoopRange` | PASS | Generic runtime is not uniquely extensible. |
| Add new query | extension cost should be comparable | `Alignment` in both models | both work | both work | `Alignment_NewQuery_WorksInBothArchitectures` | PASS | Baseline remains strong. |
| Deep nested chain | nested dispatch should terminate normally | A -> B -> C -> D | 42 | 42 | `DeepNestedQueryChain_ReturnsValue` | PASS | Non-cyclic nested queries work. |
| Full runnable candidate | restore/build/test/demo/benchmark are reproducible | `./scripts/run-all.sh` | all green | 0 warnings/errors, 41/41 | `evidence/final-candidate-run.txt` | PASS | Repository is executable evidence. |

The suite contains **41 xUnit tests**. The main metamorphic test changes only installed providers; the second metamorphic test checks optimized IR across all provider-order permutations.
