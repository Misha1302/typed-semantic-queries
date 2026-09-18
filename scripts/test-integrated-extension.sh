#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

configuration="${CONFIGURATION:-Release}"
plugin_project="tests/ExternalModules.ExactValue/ExternalModules.ExactValue.csproj"
host_project="tests/SemanticQueries.IntegratedExtension.Acceptance/SemanticQueries.IntegratedExtension.Acceptance.csproj"

dotnet restore "$plugin_project"
dotnet restore "$host_project"
dotnet build "$plugin_project" -c "$configuration" --no-restore
dotnet build "$host_project" -c "$configuration" --no-restore

plugin_dll="$(find tests/ExternalModules.ExactValue/bin -type f -path "*/$configuration/*" -name 'ExternalModules.ExactValue.dll' -print -quit)"
if [[ -z "$plugin_dll" ]]; then
  echo "External exact-value extension DLL was not produced." >&2
  exit 1
fi

dotnet run --project "$host_project" -c "$configuration" --no-build -- "$plugin_dll"
