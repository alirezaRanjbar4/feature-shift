# Troubleshooting

Exit 2 with no JSON means an argument, file, UTF-8/JSONL, schema, numeric or resource error. Check stderr, ensure both datasets are nonempty and each row has unique nonblank `id` plus the same case-sensitive numeric feature names. Null/missing/nonfinite values and duplicate properties are rejected. More than 32 features or values outside ±`1e12` are not accepted. Choose a bounded representative sample if the 32 MiB or 50,000-row limit is exceeded.

A null standardized mean difference means reference population standard deviation is zero or underflowed; it does **not** mean no change. Inspect raw mean differences and reference/candidate ranges. Out-of-range counts compare against observed sample extrema and are not a validated data-quality threshold. Overlap counts only detect exact shared IDs. See [design](DESIGN.md).

The v0.1.0 package is a GitHub Release asset, not on NuGet.org. Confirm download location/checksum, .NET 10 runtime/SDK and PATH; use a fresh tool directory to reproduce install issues. Share only synthetic data and review feature names, counts and hashes before publishing reports; see [contributing](../CONTRIBUTING.md).
