#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

dotnet pack src/CrudDatastore.NetStandard20/CrudDatastore.NetStandard20.csproj -c Release