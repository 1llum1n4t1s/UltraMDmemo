#!/bin/bash
# PNGファイルからmacOS用のicnsファイルを生成するスクリプト
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PNG_PATH="$SCRIPT_DIR/app_icon.png"
ICNS_PATH="$SCRIPT_DIR/app.icns"
ICONSET_DIR="$SCRIPT_DIR/app.iconset"

if [ ! -f "$PNG_PATH" ]; then
    echo "ERROR: PNG file not found: $PNG_PATH" >&2
    exit 1
fi

echo "Processing: $PNG_PATH -> $ICNS_PATH"

# iconset ディレクトリを作成
rm -rf "$ICONSET_DIR"
mkdir -p "$ICONSET_DIR"

# sips コマンドで各サイズのアイコンを生成
# macOS の iconutil が要求するサイズと命名規則に従う
declare -A icon_sizes
icon_sizes=(
    ["icon_16x16.png"]=16
    ["icon_16x16@2x.png"]=32
    ["icon_32x32.png"]=32
    ["icon_32x32@2x.png"]=64
    ["icon_128x128.png"]=128
    ["icon_128x128@2x.png"]=256
    ["icon_256x256.png"]=256
    ["icon_256x256@2x.png"]=512
    ["icon_512x512.png"]=512
    ["icon_512x512@2x.png"]=1024
)

for name in "${!icon_sizes[@]}"; do
    size=${icon_sizes[$name]}
    sips -z $size $size "$PNG_PATH" --out "$ICONSET_DIR/$name" >/dev/null 2>&1
done

# iconutil で icns に変換
iconutil -c icns "$ICONSET_DIR" -o "$ICNS_PATH"

# 一時ディレクトリを削除
rm -rf "$ICONSET_DIR"

echo "[OK] Successfully created: $ICNS_PATH"
