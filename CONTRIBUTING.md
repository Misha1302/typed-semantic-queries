# Contributing

This repository is an architecture experiment, so changes should preserve its falsification-oriented character.

Before opening a pull request:

```bash
./scripts/run-all.sh
```

Prefer a small experiment or counterexample over a new abstraction. If a change affects the verdict, update `docs/EXPERIMENTS.md`, `docs/BASELINE_COMPARISON.md`, and `docs/VERDICT.md` together.
