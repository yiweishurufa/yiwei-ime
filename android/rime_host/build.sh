#!/bin/bash
# Build the SAME librime (+ librime-lua) that the Trime checkout bundles, for the CI host (Linux).
# The phone runs Trime's librime; dictionaries precompiled with any other librime version
# (e.g. Ubuntu's apt librime-bin 1.10) don't match and get rebuilt on the phone.
#
# usage: android/rime_host/build.sh <trime_dir> <install_prefix>
# result: <prefix>/bin/rime_deployer, <prefix>/bin/rime_gate, <prefix>/lib/librime.so
set -euo pipefail
HERE=$(cd "$(dirname "$0")" && pwd)
J=$(cd "$1/app/src/main/jni" && pwd)
OUT=$(mkdir -p "$2" && cd "$2" && pwd)
W=$(mktemp -d)

test -f "$J/librime/CMakeLists.txt" || { echo "librime submodule missing in $J" >&2; exit 1; }
test -f "$J/librime-lua/CMakeLists.txt" || { echo "librime-lua submodule missing in $J" >&2; exit 1; }
echo "librime: $(grep -m1 -oE 'set\(rime_version [0-9.]+' "$J/librime/CMakeLists.txt") ($(git -C "$J/librime" rev-parse --short HEAD 2>/dev/null || echo ?))"
echo "librime-lua: $(git -C "$J/librime-lua" rev-parse --short HEAD 2>/dev/null || echo ?)"

# copy so the Android (gradle/ndk) build of the Trime tree is untouched
cp -a "$J/librime" "$W/librime"
rm -rf "$W/librime/plugins/lua"
cp -a "$J/librime-lua" "$W/librime/plugins/lua"
rm -rf "$W/librime/plugins/lua/thirdparty"   # use the system lua5.4 on the host

cmake -S "$W/librime" -B "$W/build" -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_INSTALL_PREFIX="$OUT" \
  -DCMAKE_INSTALL_RPATH="$OUT/lib" \
  -DBUILD_TEST=OFF -DBUILD_MERGED_PLUGINS=ON -DENABLE_EXTERNAL_PLUGINS=OFF \
  -DENABLE_LOGGING=ON -DALSO_LOG_TO_STDERR=OFF
cmake --build "$W/build"
cmake --install "$W/build" > /dev/null

cc -O2 -Wall -o "$OUT/bin/rime_gate" "$HERE/rime_gate.c" -I"$OUT/include" -L"$OUT/lib" -lrime -Wl,-rpath,"$OUT/lib"
"$OUT/bin/rime_gate" --version
ls -la "$OUT/bin"
