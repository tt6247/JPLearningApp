#!/usr/bin/env bash
# Linux / macOS 构建：需要 .NET SDK（提供 Roslyn csc）和 Mono 的 .NET Framework 4.x 参考程序集。
# Windows 请用 build.cmd（无需安装任何东西）。
set -euo pipefail
cd "$(dirname "$0")"
OUT="${1:-dist/假名小步-独立桌面版.exe}"
REF="${REF:-/usr/lib/mono/4.8-api}"
CSC="$(ls -d "$(dirname "$(readlink -f "$(command -v dotnet)")")"/sdk/*/Roslyn/bincore/csc.dll | tail -1)"
mkdir -p "$(dirname "$OUT")"
RSP="$(mktemp)"
{
  echo "-nologo -nostdlib -langversion:5 -target:winexe -platform:anycpu -optimize+ -deterministic -codepage:65001"
  echo "-win32manifest:src/app.manifest"
  for r in mscorlib System System.Core System.Drawing System.Windows.Forms System.Xml System.Xml.Linq; do echo "-r:$REF/$r.dll"; done
  for f in src/Resources/KanaAudio/*; do echo "-resource:$f,$(basename "$f")"; done
  for f in src/Resources/Fonts/*; do echo "-resource:$f,Fonts.$(basename "$f")"; done
  for f in src/Resources/Strokes/*; do echo "-resource:$f,Strokes.$(basename "$f")"; done
  find src -name '*.cs' | sort
  echo "-out:$OUT"
} > "$RSP"
dotnet "$CSC" "@$RSP"
rm -f "$RSP"
echo "已生成：$OUT"
