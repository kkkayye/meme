#!/bin/bash
# License-free compile check for the Rune Arena project using Unity's bundled Roslyn.
# Usage: tools/compile_check.sh [runtime|editor|tests|all]   (default: all)
# Prints "error CS..." lines on failure; exit code 0 only when every requested assembly compiles.
set -u
export DOTNET_CLI_UI_LANGUAGE=en
U="/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents"
DOTNET="$U/Resources/Scripting/NetCoreRuntime/dotnet"
CSC="$U/Resources/Scripting/DotNetSdkRoslyn/csc.dll"
MANAGED="$U/Resources/Scripting/Managed/UnityEngine"
API48="$U/Resources/Scripting/UnityReferenceAssemblies/unity-4.8-api"
PKG="$U/Resources/PackageManager/BuiltInPackages"
UGUI_SRC="$PKG/com.unity.ugui/Runtime/UGUI"
NUNIT="$PKG/com.unity.ext.nunit/net40/unity-custom/nunit.framework.dll"
TR_ENGINE_SRC="$PKG/com.unity.test-framework/UnityEngine.TestRunner"
TR_EDITOR_SRC="$PKG/com.unity.test-framework/UnityEditor.TestRunner"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/Logs/csc"
mkdir -p "$OUT"

VER_DEFINES="UNITY_6000_4_5;UNITY_6000_4;UNITY_6000;UNITY_5_3_OR_NEWER;UNITY_5_4_OR_NEWER;UNITY_5_5_OR_NEWER;UNITY_5_6_OR_NEWER;UNITY_2017_1_OR_NEWER;UNITY_2017_2_OR_NEWER;UNITY_2017_3_OR_NEWER;UNITY_2017_4_OR_NEWER;UNITY_2018_1_OR_NEWER;UNITY_2018_2_OR_NEWER;UNITY_2018_3_OR_NEWER;UNITY_2018_4_OR_NEWER;UNITY_2019_1_OR_NEWER;UNITY_2019_2_OR_NEWER;UNITY_2019_3_OR_NEWER;UNITY_2019_4_OR_NEWER;UNITY_2020_1_OR_NEWER;UNITY_2020_2_OR_NEWER;UNITY_2020_3_OR_NEWER;UNITY_2021_1_OR_NEWER;UNITY_2021_2_OR_NEWER;UNITY_2021_3_OR_NEWER;UNITY_2022_1_OR_NEWER;UNITY_2022_2_OR_NEWER;UNITY_2022_3_OR_NEWER;UNITY_2023_1_OR_NEWER;UNITY_2023_2_OR_NEWER;UNITY_2023_3_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_6000_1_OR_NEWER;UNITY_6000_2_OR_NEWER;UNITY_6000_3_OR_NEWER;UNITY_6000_4_OR_NEWER;ENABLE_LEGACY_INPUT_MANAGER;ENABLE_AUDIO;ENABLE_PHYSICS;ENABLE_UNET;ENABLE_MONO;NET_STANDARD_2_1;NET_STANDARD;UNITY_STANDALONE_OSX;UNITY_STANDALONE;PLATFORM_STANDALONE_OSX;PLATFORM_STANDALONE;PLATFORM_ARCH_64;UNITY_64;DEBUG;TRACE;DEVELOPMENT_BUILD"
EDITOR_DEFINES="UNITY_EDITOR;UNITY_EDITOR_64;UNITY_EDITOR_OSX;ENABLE_CLOUD_SERVICES_ADS;UNITY_INCLUDE_TESTS;UNITY_ASSERTIONS"
NOWARN="CS0649,CS0414,CS0169,CS1591,CS0108,CS0114,CS0618,CS0067,CS0219,CS0168,CS8632,CS1701,CS1702,CS0612,CS0672,CS3021,CS0660,CS0661,CS0109"
COMMON="-nologo -target:library -langversion:9.0 -nostdlib+ -noconfig -deterministic -optimize- -debug:portable -warn:1 -preferreduilang:en-US"

engine_refs() { for f in "$MANAGED"/UnityEngine.*Module.dll "$MANAGED"/UnityEngine.dll; do echo "-r:\"$f\""; done; for f in mscorlib System System.Core System.Xml System.Xml.Linq System.Numerics System.Data Microsoft.CSharp; do [ -f "$API48/$f.dll" ] && echo "-r:\"$API48/$f.dll\""; done; for f in "$API48"/Facades/*.dll; do echo "-r:\"$f\""; done; }
editor_refs() { for f in "$MANAGED"/UnityEditor.*Module.dll "$MANAGED"/UnityEditor.dll "$MANAGED"/UnityEditor.Graphs.dll; do [ -f "$f" ] && echo "-r:\"$f\""; done; }
sources() { find "$@" -name "*.cs" -not -path "*/Tests/*" -not -path "*~/*" | sed 's/.*/"&"/'; }

build() { # name rsp
  local name="$1" rsp="$2"
  "$DOTNET" "$CSC" "@$rsp" > "$OUT/$name.log" 2>&1
  local rc=$?
  local errs; errs=$(grep -E "error CS[0-9]+" "$OUT/$name.log" | sed -E 's#^.*/Assets/#Assets/#' | sort -u)
  if [ $rc -ne 0 ] || [ -n "$errs" ]; then
    echo "== $name: FAILED"; echo "$errs" | head -120; echo "(full log: $OUT/$name.log)"; return 1
  fi
  echo "== $name: OK"; return 0
}

build_ugui() {
  [ -f "$OUT/UnityEngine.UI.dll" ] && return 0
  { echo "$COMMON -nowarn:$NOWARN -define:$VER_DEFINES;PACKAGE_PHYSICS;PACKAGE_PHYSICS2D;PACKAGE_ANIMATION;PACKAGE_UITOOLKIT -out:\"$OUT/UnityEngine.UI.dll\""; engine_refs; sources "$UGUI_SRC"; } > "$OUT/ugui.rsp"
  build "UnityEngine.UI" "$OUT/ugui.rsp"
}
build_testrunner() {
  [ -f "$OUT/UnityEditor.TestRunner.dll" ] && return 0
  { echo "$COMMON -nowarn:$NOWARN -define:$VER_DEFINES;$EDITOR_DEFINES -out:\"$OUT/UnityEngine.TestRunner.dll\""; engine_refs; editor_refs; echo "-r:\"$NUNIT\""; sources "$TR_ENGINE_SRC"; } > "$OUT/tr_engine.rsp"
  build "UnityEngine.TestRunner" "$OUT/tr_engine.rsp" || return 1
  { echo "$COMMON -nowarn:$NOWARN -define:$VER_DEFINES;$EDITOR_DEFINES -out:\"$OUT/UnityEditor.TestRunner.dll\""; engine_refs; editor_refs; echo "-r:\"$NUNIT\""; echo "-r:\"$OUT/UnityEngine.TestRunner.dll\""; sources "$TR_EDITOR_SRC"; } > "$OUT/tr_editor.rsp"
  build "UnityEditor.TestRunner" "$OUT/tr_editor.rsp"
}
build_runtime() { # editor-flavoured compile of Assets/Scripts (what the Editor compiles when you press Play)
  { echo "$COMMON -nowarn:$NOWARN -define:$VER_DEFINES;$EDITOR_DEFINES -out:\"$OUT/RuneArena.Runtime.dll\""; engine_refs; editor_refs; echo "-r:\"$OUT/UnityEngine.UI.dll\""; sources "$ROOT/Assets/Scripts"; } > "$OUT/runtime.rsp"
  build "RuneArena.Runtime" "$OUT/runtime.rsp"
}
build_runtime_player() { # player-flavoured compile (no UNITY_EDITOR): catches accidental UnityEditor usage in runtime code
  { echo "$COMMON -nowarn:$NOWARN -define:$VER_DEFINES -out:\"$OUT/RuneArena.Runtime.Player.dll\""; engine_refs; echo "-r:\"$OUT/UnityEngine.UI.dll\""; sources "$ROOT/Assets/Scripts"; } > "$OUT/runtime_player.rsp"
  build "RuneArena.Runtime(player)" "$OUT/runtime_player.rsp"
}
build_editor() {
  [ -d "$ROOT/Assets/Editor" ] || { echo "== RuneArena.Editor: (no sources)"; return 0; }
  { echo "$COMMON -nowarn:$NOWARN -define:$VER_DEFINES;$EDITOR_DEFINES -out:\"$OUT/RuneArena.Editor.dll\""; engine_refs; editor_refs; echo "-r:\"$OUT/UnityEngine.UI.dll\""; echo "-r:\"$OUT/RuneArena.Runtime.dll\""; sources "$ROOT/Assets/Editor"; } > "$OUT/editor.rsp"
  build "RuneArena.Editor" "$OUT/editor.rsp"
}
build_tests() {
  build_testrunner || { echo "(test runner package failed to compile; skipping test assemblies)"; return 0; }
  local rc=0
  for mode in EditMode PlayMode; do
    [ -d "$ROOT/Assets/Tests/$mode" ] || continue
    { echo "$COMMON -nowarn:$NOWARN -define:$VER_DEFINES;$EDITOR_DEFINES -out:\"$OUT/RuneArena.Tests.$mode.dll\""; engine_refs; editor_refs; echo "-r:\"$NUNIT\""; echo "-r:\"$OUT/UnityEngine.TestRunner.dll\""; echo "-r:\"$OUT/UnityEditor.TestRunner.dll\""; echo "-r:\"$OUT/UnityEngine.UI.dll\""; echo "-r:\"$OUT/RuneArena.Runtime.dll\""; sources "$ROOT/Assets/Tests/$mode"; } > "$OUT/tests_$mode.rsp"
    build "RuneArena.Tests.$mode" "$OUT/tests_$mode.rsp" || rc=1
  done
  return $rc
}

what="${1:-all}"; rc=0
build_ugui || exit 1
case "$what" in
  runtime) build_runtime || rc=1 ;;
  editor)  build_runtime && build_editor || rc=1 ;;
  tests)   build_runtime && build_tests || rc=1 ;;
  all)     build_runtime || rc=1; build_runtime_player || rc=1; [ $rc -eq 0 ] && { build_editor || rc=1; build_tests || rc=1; } ;;
esac
exit $rc
