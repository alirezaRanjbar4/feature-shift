# Bounded first scope

Fixed on 2026-11-24 before initial publication:

- [x] Offline strict named-numeric JSONL comparison, per-feature descriptive summaries, reference-standardized mean difference, out-of-range and overlapping-ID counts, exact input hashes, tests/examples/docs and three-platform CI.
- [ ] Verified v0.1.0 initial release: actual downloaded package/checksum, final commit/tag, fresh installation and examples.
- [ ] Optional `--max-abs-standardized-mean-difference` gate: fail with exit 1 when any finite absolute ratio exceeds a caller-supplied finite nonnegative threshold; an undefined ratio from zero reference standard deviation is an explicit indeterminate failure under the gate. Equality passes. Default remains report-only. Test zero-scale, equality, strict inequality, input preservation and invalid threshold. No statistical-significance claim.
- [ ] Verified closing release containing that gate and complete docs/examples, exact final commit/tag/assets and supported-platform CI; then public/unarchived maintenance for reproducible bugs.

Categorical data, significance tests, confidence intervals, multivariate drift, training or live production telemetry are outside this fixed scope. See [research](docs/RESEARCH.md).
