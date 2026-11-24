# Synthetic comparison

The [reference](reference.jsonl) and [candidate](candidate.jsonl) contain two invented rows each with numeric `x` and `constant` features. For `x`, reference mean 2, population standard deviation 1 and range [1,3] compare to candidate mean 4 and range [3,5]; the standardized mean difference is 2 and one candidate value exceeds the observed reference maximum. The constant feature has undefined/null standardized difference. `r2` is shared, so the exact-ID overlap count is 1. Targets are extra metadata and ignored.

Run the [quick start](../README.md) or `pwsh -File examples/Smoke.ps1` after committing and packing. The checkout smoke installs the package from a fresh local feed/cache, checks hand arithmetic, input hashes, invalid-schema exit and final commit metadata. [CI](../.github/workflows/ci.yml) repeats tests, formatting, packaging and installed smoke on Windows/Linux/macOS. These examples are not real-world monitoring or model-quality evidence.
