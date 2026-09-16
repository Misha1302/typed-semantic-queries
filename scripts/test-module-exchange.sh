#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

configuration="${CONFIGURATION:-Release}"
plugin_project="tests/ExternalModules.RangeProvider/ExternalModules.RangeProvider.csproj"
host_project="tests/SemanticQueries.ModuleExchange.Acceptance/SemanticQueries.ModuleExchange.Acceptance.csproj"

dotnet restore "$plugin_project"
dotnet restore "$host_project"
dotnet build "$plugin_project" -c "$configuration" --no-restore
dotnet build "$host_project" -c "$configuration" --no-restore

plugin_dll="$(find tests/ExternalModules.RangeProvider/bin -type f -path "*/$configuration/*" -name 'ExternalModules.RangeProvider.dll' -print -quit)"
if [[ -z "$plugin_dll" ]]; then
  echo "External provider DLL was not produced." >&2
  exit 1
fi

dotnet run --project "$host_project" -c "$configuration" --no-build -- "$plugin_dll"
