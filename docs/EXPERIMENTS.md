# Experiments

| Experiment | Expected | Result | Meaning |
|---|---|---|---|
| Independent bridge | `Range + Length -> InBounds` without concrete producer references | Pass | The bridge depends only on contracts. |
| Better provider | Add `LoopRangeProvider`; old bounds optimizer improves | Pass | New providers can strengthen old consumers. |
| Shared query reuse | Same provider also strengthens branch simplification | Pass | The semantic result is reusable. |
| Multiple Range providers | Intersection | Pass | Combination belongs to the query contract. |
| Conflicting Length providers | Conflict / fail closed | Pass | Exact facts do not silently override each other. |
| Registration order | Same semantic result | Pass | No first-provider-wins semantics. |
| IR mutation | Old session rejected | Pass | Cache cannot cross revisions through normal API. |
| Query cycle | Deterministic error | Pass | The runtime does not become a hidden fixed-point engine. |
| Probability vs proof | Layout may change; legality may not | Pass | Heuristic and proof knowledge remain separate. |
