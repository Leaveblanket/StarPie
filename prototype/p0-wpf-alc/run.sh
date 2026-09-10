#!/usr/bin/env bash
# P0 打样：一条命令跑完 13 项探针，产出 p0-report.md
set -euo pipefail
cd "$(dirname "$0")"

dotnet build src/P0.Plugin/P0.Plugin.csproj -c Release -v minimal
dotnet build src/P0.Host/P0.Host.csproj -c Release -v minimal
dotnet src/P0.Host/bin/Release/net10.0-windows/P0.Host.dll "$@"
