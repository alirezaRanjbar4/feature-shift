# Contributing

Use small synthetic reproductions. Never upload private data, real model inputs or credentials. Read [design](docs/DESIGN.md), [troubleshooting](docs/TROUBLESHOOTING.md) and the fixed [roadmap](ROADMAP.md).

With the .NET 10 SDK and PowerShell 7, run `dotnet restore`, `dotnet test -c Release --no-restore`, `dotnet format --verify-no-changes --no-restore`, `dotnet pack src/Cli -c Release --no-restore -o artifacts`, and `pwsh -File examples/Smoke.ps1`. Commit intended changes and repack first so the final-commit assertion is meaningful. Tests use hand-calculated summaries, schema/encoding/bounds and read-only/privacy contracts. No runtime dependency or network client is required. Do not imply descriptive summaries establish statistical significance or model quality.
