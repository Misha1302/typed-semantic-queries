# Independent module exchange acceptance

`bash ./scripts/test-module-exchange.sh` is the architecture-level extension experiment. It builds an `ExternalModules.RangeProvider` assembly separately from the acceptance host. The host has no compile-time reference to that assembly and loads it only after build through `AssemblyLoadContext` and `IQueryProviderRegistration`.

The fixture checks all of the following at runtime:

- without the external range provider, the existing `BoundsCheckOptimizer` keeps the check;
- the acceptance host does not reference the external provider assembly;
- the external provider does not reference the existing array producer, bounds bridge, or bounds optimizer packages;
- after explicit host opt-in, the unchanged `BoundsBridgeProvider` observes the external `RangeQuery` result and the unchanged `BoundsCheckOptimizer` removes the check.

This is a structural interoperability test, not a claim that the production system already has dynamic plugin discovery. The deployment model exercised here is explicit host opt-in: `E_core = 0`, `E_consumer = 0`, while host composition is allowed to select an independently built provider.
