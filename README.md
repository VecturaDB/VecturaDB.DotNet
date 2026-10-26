# VecturaDB for .NET

The command `vecturadb` runs [VecturaDB](https://github.com/VecturaDB/VecturaDB) on this machine, without Python and without Docker.

> Preview: the tool and VecturaDB can still change.

```
dotnet tool install -g VecturaDB --prerelease
vecturadb run --path ./vecturadb-data --port 8000
```

The arguments go to VecturaDB as they are: `vecturadb --help` shows its commands. The exit code is the one of VecturaDB.

## The executable

The first run downloads the VecturaDB executable of the platform from the GitHub release of VecturaDB, and checks its SHA-256 hash,
which the tool has for each file: Windows x64, also on Windows on Arm through emulation, Linux x64 and arm64 (glibc 2.17 and later),
macOS x64 and arm64. It keeps the executable in the cache of the user, and checks the hash again before each run:

| Platform | Cache |
|---|---|
| Windows | `%LOCALAPPDATA%\VecturaDB` |
| macOS | `~/Library/Caches/VecturaDB` |
| Linux | `$XDG_CACHE_HOME/vecturadb`, or `~/.cache/vecturadb` |

| Environment variable | |
|---|---|
| `VECTURADB_CACHE` | The directory of the cache. |
| `VECTURADB_DOWNLOAD_URI` | A mirror with the files of the release, in place of GitHub. |
| `VECTURADB_EXECUTABLE` | An executable of VecturaDB to run, without the download. |

On Linux and macOS the command becomes the VecturaDB process. On Windows VecturaDB runs as a child process, which ends with the
command.

## Versions

The version of the tool is the one of VecturaDB that it runs: 1.0.0-preview.3.post1 runs VecturaDB 1.0.0-preview.3.post1. When only the
tool changes, a revision follows, as 1.0.0-preview.3.post1.1.

## License

Apache License 2.0. The package carries the NOTICE, the THIRD-PARTY-NOTICES and the LICENSE of VecturaDB at its tag, in
`THIRD-PARTY-NOTICES.md`.

VecturaDB is an independent fork of Chroma, under the Apache License 2.0. The original code is copyright Chroma Inc. and its
contributors. Not affiliated with, endorsed by or sponsored by Chroma Inc. CHROMA is a registered trademark of Chroma Inc., used here
only to describe compatibility.
