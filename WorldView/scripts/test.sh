#!/usr/bin/env bash
# 运行全部单元测试（macOS/Linux）。
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet test WorldView.sln "$@"
