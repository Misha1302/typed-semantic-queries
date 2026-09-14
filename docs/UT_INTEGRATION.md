# Universal Toolchain integration spike

This is a read-only mapping against the actual `Misha1302/UniversalToolchain` repository, commit `40117eb68c630f7129c120aaaadc69be8f4ecbfb` (`master`). No UT code was changed by this experiment.

## Existing owners found in UT

- `UniversalToolchain.LanguageSdk.LanguagePlan` owns selected features, contributions and routes and has a canonical `PlanHash`.
- `SelectedCapabilityCatalogBuilder` explicitly materializes capabilities from implementation types **already selected by `LanguagePlan`**; it does not perform package/route selection.
- `CapabilityCatalog` owns selected capability providers/feature ownership.
- `BasicCore.Registration.CoreIntrinsicServiceCollectionExtensions` proves UT already uses Microsoft DI/service registration.
- `IrStageContract`, `IrPipelineContext`, `IrStageResult` and `IrFactSet` own coarse pass-boundary fact validity and invalidation.
- `CompilerFactState` / module-contract infrastructure own module-level compiler-fact invalidation and verification routing.
- `SsaOptimizerPipeline` applies pass contracts and carries fact state between SSA passes.
- `UniversalToolchain.Semantics.Abstractions` already owns semantic type/callable/effect descriptors; it is descriptor semantics, not a per-SSA-value query database.
- SSA already has typed stable-looking IDs such as `SsaValueId`, `SsaBlockId` and `SsaOperationId`.

A search of the current source did **not** find a general IR revision/epoch token suitable for safe cross-pass query-cache reuse. Existing invalidation is fact/stage based.

## Minimal mapping

| MVP concept | UT owner / proposed seam |
|---|---|
| typed query contract | small analysis contract keyed by SSA IDs; do not encode parameterized `Range(value, point)` as a string `FactId` |
| provider registration | use LanguagePlan-selected components / existing DI and capability composition; do not create parallel plugin discovery |
| `StaticPlan` | materialize providers from the already-selected LanguagePlan/runtime component set |
| `SemanticSession` | conservative per-SSA-pass-input analysis session initially |
| coarse fact validity | continue using `IrStageContract` / `IrFactSet` / module-contract invalidation |
| structured engine | keep in AIR/SSA analysis packages; export only small query results |

## Revision boundary

Until UT has an explicit artifact revision/epoch identity, do **not** carry memoized parameterized semantic answers across mutating pass boundaries. The safe first integration treats each optimizer-pass input artifact as a new query session. Existing `IrStageContract.InvalidatesFacts` remains useful for coarse facts, but it does not by itself identify whether `Range(v, p)` cached for a previous artifact is safe.

## First queries worth trying

1. `Range(SsaValueId, program point)` — directly exercises independent analysis reuse.
2. `Effects(operation/region)` — UT already models semantic effects at descriptor level, so a value/region analysis can complement rather than replace that owner.
3. `NoAlias(value, value, point)` — enables a bridge such as `Effects + NoAlias -> CanHoist` without teaching LICM concrete analyses.

`Length/InBounds` should wait until the relevant array/reference IR model is stable enough to give their keys clear identity.

## Versioning and dispatch

Use normal .NET/API/package compatibility plus the existing LanguagePlan package/version/manifest identity. Do not introduce stringly `QueryId`s merely for versioning. Start with explicit typed registration; source generation or reflection is unjustified by the standalone experiment.

## Recommendation

**Do not integrate the standalone `StaticPlan` runtime into UT as a parallel subsystem.** Prototype the contracts on top of existing LanguagePlan/DI composition and create a session scoped to one SSA artifact/pass input. Add a real UT revision owner only if measurements show cross-pass cache reuse is valuable enough to justify it.


## Answers to the integration questions

**What belongs in Core?** Only the smallest stable typed query/provider contracts and lifecycle protocol, if UT cannot express them with an existing analysis owner. Do not move concrete analyses into Core.

**What can remain a normal library?** Query implementations, memoization/session hosting and bridges can begin as an analysis library layered on existing UT abstractions rather than a privileged subsystem.

**Is the word `semantic` required?** No. UT already uses `Semantics.Abstractions` for descriptors; calling per-IR analysis contracts simply `analysis queries` may avoid conflating two different meanings.

**Can UT reuse its existing registry/composition?** Yes. `LanguagePlan`, selected capability materialization and Microsoft DI already own composition. A second plugin registry would duplicate authority.

**How should contracts be versioned?** Through ordinary .NET API/package compatibility plus existing package version/manifest identity carried by LanguagePlan, not an untyped string query registry.

**How should keys identify IR nodes?** Prefer existing typed SSA identities (`SsaValueId`, `SsaBlockId`, `SsaOperationId`) and an explicit program-point identity appropriate to the analysis. Avoid object-reference identity when artifacts may be rebuilt.

**Where is the revision boundary?** Today the safest boundary is one SSA optimizer-pass input/artifact. There is no general revision token in the inspected source, so cross-mutating-pass caches should not be reused yet.

**Which analyses should remain engines?** CFG construction, loop/IV analysis, dataflow/liveness, alias engines and recursive/fixed-point analyses. They should export small query results rather than be rewritten as generic facts.

**Which first three queries should be tried?** `Range`, `Effects`, and `NoAlias`; they can then support a `CanHoist` bridge. `Length/InBounds` can follow when the array/reference model has stable keys.

**How can a third-party module add a provider?** Through its already-selected LanguagePlan/runtime component registration and existing DI/capability composition; the provider should implement the public typed analysis contract without requiring changes to consumers.

**Is source generation needed?** Not for the first integration. The standalone experiment did not demonstrate a dispatch bottleneck large enough to justify it.

**Can runtime reflection be avoided?** Yes. Explicit registration from the selected component set is sufficient; no reflection is needed on query hot paths.
