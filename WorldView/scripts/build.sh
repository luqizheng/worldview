#!/usr/bin/env bash
# 构建 World View 新架构（macOS/Linux）。
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build WorldView.sln "$@"
