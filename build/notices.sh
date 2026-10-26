#!/bin/bash
# Usage: build/notices.sh. Writes artifacts/notices/THIRD-PARTY-NOTICES.md for the package: the NOTICE, the THIRD-PARTY-NOTICES and the
# LICENSE of VecturaDB at the tag of Directory.Build.props, as they are. The package does not carry the executable, which the first run
# downloads from the release of that tag; its notices come with the package all the same.
set -euo pipefail
cd "$(dirname "$0")/.."
tag=$(sed -n 's:.*<VecturaDBTag>\(.*\)</VecturaDBTag>.*:\1:p' Directory.Build.props)
out=artifacts/notices
mkdir -p "$out"
for file in NOTICE THIRD-PARTY-NOTICES LICENSE; do
  curl -sSfL -o "$out/$file" "https://raw.githubusercontent.com/VecturaDB/VecturaDB/$tag/$file"
done
{
  echo "# Notices of VecturaDB"
  echo
  echo "The command vecturadb runs the VecturaDB executable of the GitHub release $tag of https://github.com/VecturaDB/VecturaDB,"
  echo "which the first run downloads. VecturaDB is under the Apache License 2.0. Below, as they are at the tag $tag: its NOTICE, its"
  echo "THIRD-PARTY-NOTICES and its LICENSE."
  for file in NOTICE THIRD-PARTY-NOTICES LICENSE; do
    echo
    echo "## $file"
    echo
    echo '```'
    cat "$out/$file"
    echo '```'
  done
} > "$out/THIRD-PARTY-NOTICES.md"
echo "$out/THIRD-PARTY-NOTICES.md: $(wc -c < "$out/THIRD-PARTY-NOTICES.md") bytes, from $tag"
