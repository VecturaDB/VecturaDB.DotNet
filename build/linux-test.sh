#!/bin/bash
# Usage: build/linux-test.sh [x64|arm64]. In a container of the .NET SDK for Linux, with a copy of the repository: the tests, then
# build/tool-test.sh, the tool installed from its package and run through its shim. The repository is mounted read-only; nothing stays
# in Docker after the run.
set -euo pipefail
arch=${1:-x64}
case "$arch" in x64) platform=linux/amd64 ;; arm64) platform=linux/arm64 ;; *) echo "usage: build/linux-test.sh [x64|arm64]" >&2; exit 2 ;; esac
cd "$(dirname "$0")/.."
root=$(pwd -W 2>/dev/null || pwd)
export MSYS_NO_PATHCONV=1
docker rm -f vecturadb-dotnet-test >/dev/null 2>&1 || true
docker run --rm --name vecturadb-dotnet-test --platform "$platform" -v "$root:/repo:ro" \
  -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c '
set -uo pipefail
mkdir -p /work && cd /repo && tar --exclude=./artifacts --exclude="*/bin" --exclude="*/obj" --exclude=./.git -cf - . | tar -xf - -C /work
cd /work
echo "== $(uname -m), $(ldd --version | head -1)"
dotnet test tests/VecturaDB.Tests -c Release --logger "console;verbosity=normal" > /tmp/tests.log 2>&1; code=$?
grep -E "Total tests|Passed:|Failed:" /tmp/tests.log
[ $code -eq 0 ] && echo "ok   tests" || { echo "FAIL tests: exit $code"; grep -B2 -A20 "^\s*Failed " /tmp/tests.log | head -60; }
bash build/tool-test.sh; tools=$?
exit $(( (code != 0) + tools ))
'
