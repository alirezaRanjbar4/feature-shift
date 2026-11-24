# Feature Shift

An offline C#/.NET 10 CLI to compare named numeric features in a **reference dataset** (for example, training data) and a candidate dataset (for example, validation or a later batch). It reports descriptive means, population standard deviations, ranges, reference-standardized mean differences, candidate values outside the reference range and overlapping record-ID counts.

These summaries help locate differences for investigation. They are not a statistical test, a complete distribution comparison or evidence that a model's quality changed. Equal means can hide substantial changes in shape. It does not train or call a model. Existing alternatives and limits are in [research](docs/RESEARCH.md).

## Quick start

With the .NET 10 SDK from a checkout:

```sh
dotnet run --project src/Cli -- --reference examples/reference.jsonl --candidate examples/candidate.jsonl
```

The synthetic reference `x` values are 1 and 3; the candidate values are 3 and 5. Their means are 2 and 4, reference population standard deviation is 1, signed mean difference is 2, standardized difference is 2 and one candidate value exceeds the reference maximum. The constant feature has zero reference standard deviation, so its standardized difference is `null`. One ID appears in both datasets; this demonstrates overlap detection, not leakage detection. See [examples](examples/README.md).

## Input and output

Each UTF-8 JSONL line needs `id` (a unique, nonblank string) and `features` (an object containing 1–32 named finite numeric values, magnitude at most `1e12`):

```json
{"id":"example-01","features":{"load":2,"workers":1},"target":17}
```

Every row and dataset must have the same exact case-sensitive feature names; JSON object order does not matter. Additional top-level fields such as `target` or metadata are ignored, but duplicate JSON properties anywhere are rejected. IDs are exact/case-sensitive, unique within each dataset; the report gives only the count of IDs shared across datasets. It cannot detect different IDs for the same entity, semantic duplicates or temporal leakage. Optional UTF-8 BOM is accepted.

The JSON report on stdout contains `schemaVersion: 1`, SHA-256 of each input's exact bytes, row counts, overlap count and features in ordinal name order. For each feature, `meanDifference = candidate.mean - reference.mean`; `referenceStandardizedMeanDifference` divides that by the reference population standard deviation and is null when it is zero (including floating-point variance underflow). `candidateBelowReferenceMin` and `candidateAboveReferenceMax` use strict inequalities. Counts and feature names appear in output, but individual IDs, feature rows and metadata do not; aggregate values and hashes can still disclose sensitive information.

Exit 0 means the descriptive report was produced; exit 2 means invalid arguments/data, numeric error, resource limit or inaccessible file, with an error on stderr and no partial JSON. The command creates or modifies no files; shell redirection remains the caller's responsibility. Input is limited to 32 MiB per file, 50,000 rows, 1 Mi UTF-16 characters per line, 32 features, 256-character IDs, 64-character names and JSON depth 32. Input snapshots and rows are held in memory. See [design](docs/DESIGN.md) and [troubleshooting](docs/TROUBLESHOOTING.md).

## Install

Download `FeatureShift.Tool.0.1.0.nupkg` and `SHA256SUMS.txt` from [v0.1.0](https://github.com/alirezaRanjbar4/feature-shift/releases/tag/v0.1.0), compare the SHA-256 values, then install using the .NET 10 SDK:

```sh
dotnet tool install FeatureShift.Tool --version 0.1.0 --tool-path ./tool --add-source ./downloaded-package
./tool/feature-shift --reference examples/reference.jsonl --candidate examples/candidate.jsonl
```

The installed tool needs the .NET 10 runtime and has no runtime package dependencies. It is distributed as a GitHub Release asset, not NuGet.org. A neighboring checksum detects corruption, not authenticity. Windows uses `tool\feature-shift.exe`.

See [contributing](CONTRIBUTING.md), the [bounded roadmap](ROADMAP.md), [changelog](CHANGELOG.md) and [MIT license](LICENSE).
