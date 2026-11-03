#!/usr/bin/env bash
# 一键构建 CharonMascot（Linux/macOS）
set -e
cd "$(dirname "$0")"
RID="${1:-linux-x64}"
OUT="dist/${RID}"
echo "== 还原依赖"
dotnet restore src/CharonMascot
echo "== 发布 ${RID}"
dotnet publish src/CharonMascot -c Release -r "${RID}" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "${OUT}"
echo "== 完成：${OUT}"
