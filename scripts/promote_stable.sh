#!/bin/bash
# W44 把当前测试版（update/appcast.xml，指向 nightly 安装包）提升为稳定版。
# 用法：scripts/promote_stable.sh [版本号]   例：scripts/promote_stable.sh 0.1.0.70
# 做的事：从 nightly release 下载该版本安装包，另建 release v<版本>（非 prerelease）上传，
#         写 update/appcast-stable.xml 指向这个永久地址，然后提交推送。需要 gh CLI 已登录。
set -euo pipefail
cd "$(dirname "$0")/.."
REPO=yiweishurufa/yiwei-ime
VER=${1:-$(grep -o 'sparkle:version="[^"]*"' update/appcast.xml | head -1 | cut -d'"' -f2)}
FILE="yiwei-ime-${VER}-installer.exe"
tmp=$(mktemp -d)
gh release download nightly -R "$REPO" -p "$FILE" -D "$tmp"
gh release create "v${VER}" "$tmp/$FILE" -R "$REPO" -t "一维输入法 ${VER}" -n "稳定版 ${VER}" --latest
URL="https://github.com/${REPO}/releases/download/v${VER}/${FILE}"
LEN=$(stat -c %s "$tmp/$FILE"); SHA=$(sha256sum "$tmp/$FILE" | cut -d' ' -f1)
sed -e "s#url=\"[^\"]*\"#url=\"${URL}\"#" -e "s#length=\"[^\"]*\"#length=\"${LEN}\"#" -e "s#sha256=\"[^\"]*\"#sha256=\"${SHA}\"#" \
    -e "s#sparkle:version=\"[^\"]*\"#sparkle:version=\"${VER}\"#" -e "s#sparkle:shortVersionString=\"[^\"]*\"#sparkle:shortVersionString=\"${VER}\"#" update/appcast.xml > update/appcast-stable.xml
git add update/appcast-stable.xml && git commit -m "稳定版 ${VER} [skip ci]" && git push
echo "stable -> $URL"
