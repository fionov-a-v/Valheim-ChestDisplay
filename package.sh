#!/bin/sh
# Собирает мод и кладёт архив для Thunderstore/r2modman (или чтобы передать друзьям) в dist/.
set -e
cd "$(dirname "$0")"
[ -x "$HOME/.dotnet/dotnet" ] && export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
VERSION=$(python3 -c "import json;print(json.load(open('package/manifest.json'))['version_number'])")
dotnet build -c Release
rm -rf dist/pkg && mkdir -p dist/pkg/plugins
cp bin/Release/ChestDisplay.dll dist/pkg/plugins/
cp package/manifest.json package/icon.png README.md LICENSE.md dist/pkg/
(cd dist/pkg && rm -f "../ChestDisplay-$VERSION.zip" && python3 -m zipfile -c "../ChestDisplay-$VERSION.zip" manifest.json icon.png README.md LICENSE.md plugins)
rm -rf dist/pkg
echo "dist/ChestDisplay-$VERSION.zip"
