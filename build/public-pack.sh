#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

mode="${1:-default}"

if [[ "$mode" == "--all-targets" ]]; then
	output_root="artifacts/packages"
	mkdir -p "$output_root"

	dotnet pack src/CrudDatastore.NetStandard20/CrudDatastore.NetStandard20.csproj -c Release -p:PackageId=CrudDatastore.NetStandard20 -o "$output_root"
	dotnet pack src/CrudDatastore.Net8/CrudDatastore.Net8.csproj -c Release -p:IsPackable=true -p:PackageId=CrudDatastore.Net8 -o "$output_root"
	dotnet pack src/CrudDatastore.Net10/CrudDatastore.Net10.csproj -c Release -p:IsPackable=true -p:PackageId=CrudDatastore.Net10 -o "$output_root"
	dotnet pack src/CrudDatastore.Net481/CrudDatastore.Net481.csproj -c Release -p:IsPackable=true -p:PackageId=CrudDatastore.Net481 -o "$output_root"
else
	dotnet pack CrudDatastore.sln -c Release
fi