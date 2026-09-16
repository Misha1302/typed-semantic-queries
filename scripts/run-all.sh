#!/usr/bin/env bash
set -euo pipefail
dotnet restore SemanticQueryMvp.sln
dotnet build SemanticQueryMvp.sln -c Release --no-restore
dotnet test SemanticQueryMvp.sln -c Release --no-build
dotnet run --project tests/SemanticQueries.ArchitectureAcceptance -c Release
bash ./scripts/test-module-exchange.sh
dotnet run --project demo/SemanticQueryDemo -c Release --no-build
dotnet run --project benchmarks/SemanticQueries.Microbenchmarks -c Release --no-build
