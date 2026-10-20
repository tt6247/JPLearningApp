#!/usr/bin/env bash
# 构建《假名小步 · 安卓版》APK（不需要 Gradle / Android Studio）。
# 需要：JDK 17+，以及 Ubuntu/Debian 软件包 android-sdk-platform-23 aapt zipalign apksigner dalvik-exchange
#   sudo apt-get install openjdk-21-jdk-headless android-sdk-platform-23 aapt zipalign apksigner dalvik-exchange
# Windows 上可在 WSL（Ubuntu）里运行本脚本。
set -euo pipefail
cd "$(dirname "$0")"
JAR="${ANDROID_JAR:-/usr/lib/android-sdk/platforms/android-23/android.jar}"
VERSION=$(sed -n 's/.*android:versionName="\([^"]*\)".*/\1/p' app/AndroidManifest.xml)
OUT="dist/假名小步-安卓版-${VERSION}.apk"
source <(sed 's/\r$//' signing/keystore.properties)

echo "== 1/6 核心逻辑自检"
node tests/core.test.js

echo "== 2/6 生成 R.java"
rm -rf build && mkdir -p build/gen build/classes build/dex dist
aapt package -f -m -J build/gen -M app/AndroidManifest.xml -S app/res -I "$JAR"

echo "== 3/6 编译 Java"
javac --release 8 -nowarn -encoding UTF-8 -classpath "$JAR" -d build/classes $(find app/java build/gen -name '*.java')

echo "== 4/6 转换为 dex"
dalvik-exchange --dex --min-sdk-version=23 --output=build/dex/classes.dex build/classes

echo "== 5/6 打包资源与页面"
aapt package -f -M app/AndroidManifest.xml -S app/res -A app/assets -I "$JAR" -F build/unsigned.apk
(cd build/dex && aapt add ../unsigned.apk classes.dex >/dev/null)
zipalign -f -p 4 build/unsigned.apk build/aligned.apk

echo "== 6/6 签名"
apksigner sign --v4-signing-enabled false --ks "signing/$storeFile" --ks-key-alias "$keyAlias" --ks-pass "pass:$storePassword" --key-pass "pass:$keyPassword" --out build/signed.apk build/aligned.apk
apksigner verify --print-certs build/signed.apk | head -3
mv build/signed.apk "$OUT"  # Java 在非 UTF-8 环境下写不了中文文件名，所以先用英文名签名再改名
echo "已生成：$OUT"
