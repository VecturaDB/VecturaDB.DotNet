Preview of VecturaDB VERSION for .NET: the command vecturadb runs VecturaDB VECTURADB_VERSION, without Python and without Docker.

```
dotnet tool install -g VecturaDB --version VERSION
vecturadb run --path ./vecturadb-data --port 8000
```

The first run downloads the VecturaDB executable of its platform from the GitHub release
[VECTURADB_TAG](https://github.com/VecturaDB/VecturaDB/releases/tag/VECTURADB_TAG) of VecturaDB, for Windows x64, Linux x64 and arm64
(glibc 2.17 and later), macOS x64 and arm64, and checks its SHA-256 hash.

The package of this release is signed with Sigstore, with its build provenance:

```
cosign verify-blob --bundle VecturaDB.VERSION.nupkg.sigstore.json --certificate-identity-regexp '^https://github[.]com/VecturaDB/VecturaDB[.]DotNet/[.]github/workflows/ci[.]yml@refs/tags/' --certificate-oidc-issuer https://token.actions.githubusercontent.com VecturaDB.VERSION.nupkg
gh attestation verify VecturaDB.VERSION.nupkg --repo VecturaDB/VecturaDB.DotNet --signer-workflow VecturaDB/VecturaDB.DotNet/.github/workflows/ci.yml --source-ref refs/tags/vVERSION
```

VecturaDB is an independent fork of Chroma, under the Apache License 2.0. The original code is copyright Chroma Inc. and its
contributors. Not affiliated with, endorsed by or sponsored by Chroma Inc. CHROMA is a registered trademark of Chroma Inc., used here
only to describe compatibility.
