#!/bin/bash
# Usage: build/tool-test.sh. The tool as a user gets it: packed into artifacts/packages, installed with dotnet tool install into
# artifacts/tools, and run through its shim with the cache of the tests: --version, then a server that answers on a port and ends with
# the command. On Linux and macOS the shim becomes VecturaDB, and SIGTERM ends it; on Windows the shim alone is killed, and its job
# object ends VecturaDB.
set -uo pipefail
cd "$(dirname "$0")/.."
fails=0
check() { if [ "$2" = "$3" ]; then echo "ok   $1"; else echo "FAIL $1: expected $3, got $2"; fails=$((fails + 1)); fi; }
native() { if command -v cygpath >/dev/null; then cygpath -w "$1"; else echo "$1"; fi; }
heartbeat() { curl -s -o /dev/null -w '%{http_code}' -m 2 "http://127.0.0.1:$port/api/v2/heartbeat"; }
case "$(uname -s)" in MINGW*|MSYS*|CYGWIN*) windows=1; exe=.exe ;; *) windows=0; exe= ;; esac
version=$(dotnet msbuild src/VecturaDB -getProperty:Version -nologo | tr -d '\r')
server=$(dotnet msbuild src/VecturaDB -getProperty:VecturaDBVersion -nologo | tr -d '\r')
mkdir -p artifacts && rm -rf artifacts/tools
dotnet pack src/VecturaDB -c Release -nologo -v q > artifacts/tool-pack.log 2>&1; check "pack" "$?" 0
dotnet tool install --tool-path artifacts/tools --add-source artifacts/packages VecturaDB --version "$version" > artifacts/tool-install.log 2>&1
check "dotnet tool install VecturaDB $version" "$?" 0
tool=artifacts/tools/vecturadb$exe
check "the command vecturadb" "$([ -f "$tool" ] && echo found || echo missing)" found
VECTURADB_CACHE=$(native "$(pwd)/artifacts/test-cache"); export VECTURADB_CACHE
check "vecturadb --version says vecturadb" "$("$tool" --version 2>/dev/null | tr -d '\r')" "vecturadb $server"
port=$((20000 + RANDOM % 20000))
data=$(mktemp -d)
"$tool" run --path "$(native "$data")" --host 127.0.0.1 --port $port > artifacts/tool-server.log 2>&1 & pid=$!
for _ in $(seq 1 600); do [ "$(heartbeat)" = 200 ] && break; sleep 0.1; done
check "the server answers" "$(heartbeat)" 200
if [ -e "/proc/$pid/exe" ]; then
  name=$(basename "$(readlink "/proc/$pid/exe")")
  # Under the emulation of another architecture in Docker the executable of the process is QEMU, which has VecturaDB in its arguments.
  case "$name" in qemu-*) name=$(tr '\0' '\n' < "/proc/$pid/cmdline" | grep -m1 vecturadb | xargs basename) ;; esac
  check "the process is VecturaDB" "$name" vecturadb
elif [ $windows = 0 ]; then
  check "the process is VecturaDB" "$(basename "$(ps -p $pid -o comm= | tr -d ' ')")" vecturadb
fi
if [ $windows = 1 ]; then taskkill //PID "$(cat "/proc/$pid/winpid")" //F > /dev/null; else kill -TERM $pid; fi
for _ in $(seq 1 150); do [ "$(heartbeat)" = 000 ] && break; sleep 0.1; done
check "the server ends with the command" "$(heartbeat)" 000
wait $pid 2>/dev/null
rm -rf "$data"
echo "failures: $fails"
exit $fails
