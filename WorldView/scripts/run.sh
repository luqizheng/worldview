#!/usr/bin/env bash
# 运行 World View 桌面应用（macOS/Linux）。
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project src/WorldView.Desktop "$@"
