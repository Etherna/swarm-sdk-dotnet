# SwarmSDK

[![SwarmSdk on NuGet](https://img.shields.io/nuget/v/SwarmSdk?label=SwarmSdk)](https://www.nuget.org/packages/SwarmSdk/)
[![SwarmSdk.Client on NuGet](https://img.shields.io/nuget/v/SwarmSdk.Client?label=SwarmSdk.Client)](https://www.nuget.org/packages/SwarmSdk.Client/)
[![Target frameworks](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4)](#supported-frameworks)
[![License: LGPL-3.0](https://img.shields.io/badge/license-LGPL--3.0-blue)](COPYING-LESSER)

**SwarmSDK** is a [Swarm](https://ethswarm.org/) Development Kit for .NET. It provides the models and tools
to work with Swarm's protocol, plus a client for the [Bee](https://github.com/ethersphere/bee) node.

SwarmSDK is a set of **libraries**, not an application. Add it to your own project to upload and download
data from Swarm, compute Swarm addresses, work with feeds and postage stamps, and drive a Bee node's API.

## Contents

- [Features](#features)
- [Packages](#packages)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Documentation](#documentation)
- [Supported frameworks](#supported-frameworks)
- [Building and testing](#building-and-testing)
- [Project layout](#project-layout)
- [Package repositories](#package-repositories)
- [Contributing](#contributing)
- [Issue reports](#issue-reports)
- [Questions? Problems?](#questions-problems)
- [License](#license)

## Features

**`SwarmSdk` — the SDK (models, crypto, chunking, manifests, feeds, postage, services):**

- **Content-addressed chunking** — a configurable hashing pipeline that splits data into chunks, builds the
  chunk tree, optionally encrypts, and computes the resulting Swarm reference locally.
- **Chunk compaction** — optimistically search for the chunk key that lands in the postage bucket with the
  fewest collisions (`compactLevel`), packing data into fewer buckets for better postage-stamp utilization.
- **BMT hashing & signing** — in-house Binary Merkle Tree hashing and ECDSA signing primitives (BouncyCastle).
- **Chunk types** — Content Addressed Chunks (CAC) and Single Owner Chunks (SOC), with BMT address
  computation and SOC signing as intrinsic model behavior.
- **Redundancy** — Reed–Solomon erasure coding and chunk replication, with a `ReplicaResolverChunkStore`
  that resolves replicas on read.
- **Manifests** — read and write Mantaray manifests and resolve paths to references.
- **Feeds** — epoch and sequence feeds: build, sign, look up feed chunks and resolve feed manifests.
- **Postage stamping** — issue and apply postage stamps, track buckets, and estimate batch usage.
- **Composable chunk stores** — memory, local-directory, caching, mirroring and replica-resolving stores,
  designed as decorators you compose rather than monolithic implementations.
- **High-level services** — `ChunkService` and `FeedService` orchestrating uploads, downloads and address
  resolution.
- **Value & identity types** — `BzzValue`/`XDaiValue` money types and Ethereum address/key primitives.

**`SwarmSdk.Client` — Bee node HTTP client:**

- A high-level `SwarmClient` over the Bee HTTP API: upload/download bytes, files and directories, manage
  pins, tags, postage batches, chequebook and peers, query node health/info, plus chunk-stream uploads
  over WebSocket. A Beehive-compatible API mode is also available.

## Packages

Two NuGet packages are published:

| Package | Description | Depends on |
| --- | --- | --- |
| [**SwarmSdk**](https://www.nuget.org/packages/SwarmSdk/) | The Swarm SDK: value models, BMT hashing & signing, chunking pipeline, manifests, composable chunk stores, postage stamping and high-level services. No Bee node required. | — |
| [**SwarmSdk.Client**](https://www.nuget.org/packages/SwarmSdk.Client/) | A client to connect to and operate a Bee node, built on top of `SwarmSdk`. | `SwarmSdk` |

## Installation

```bash
# Talk to a Bee node (brings in the full SDK):
dotnet add package SwarmSdk.Client

# Just the SDK — offline chunking, manifests, feeds, models and services:
dotnet add package SwarmSdk
```

## Quick start

### Talk to a Bee node

Connect to a Bee node, upload a file, and download it again:

```csharp
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Models;

// Connect to a local Bee node (default Bee API on port 1633).
using var client = new SwarmClient(new Uri("http://localhost:1633"));

// Buy a postage batch to pay for storage (amount in BZZ, depth defines capacity).
var (batchId, _) = await client.BuyPostageBatchAsync(BzzValue.FromInt32(1), depth: 20);

// Upload a file and get back its Swarm reference.
await using var content = File.OpenRead("hello.txt");
var reference = await client.UploadFileAsync(content, batchId, name: "hello.txt");
Console.WriteLine($"Uploaded to {reference}");

// Download it again.
using var file = await client.GetFileAsync(new SwarmAddress(reference));
await using var target = File.Create("downloaded.txt");
await file.Stream.CopyToAsync(target);
```

To target a Beehive-compatible API instead of a standard Bee node, pass
`SwarmClients.Beehive` to the `SwarmClient` constructor.

### Compute a Swarm reference locally (no node)

`SwarmSdk` works **without a Bee node** — for example, to chunk data and compute its Swarm reference offline
with `ChunkService`. The same evaluation also reports how much postage batch space the upload would need, so
you can size a batch before paying for one.

```csharp
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Services;
using System.Text;

var chunkService = new ChunkService();
var data = Encoding.UTF8.GetBytes("Hello Swarm!");

var result = await chunkService.UploadSingleFileAsync(
    data,
    fileContentType: "text/plain",
    fileName: "hello.txt",
    hasher: new Hasher());

Console.WriteLine($"Swarm reference: {result.Reference}");
Console.WriteLine($"Required postage batch size: {result.RequiredPostageBatchByteSize} bytes");
```

Beyond chunking, the SDK can also read and write Mantaray manifests and build, sign and resolve feeds — see
the `Services/`, `Chunks/`, `Manifest/` and `Models/` folders under `src/SwarmSdk`.

## Documentation

The complete documentation lives in the **[SwarmSDK wiki](https://github.com/Etherna/swarm-sdk-dotnet/wiki)**:
getting started, the core concepts (hashes and references, chunks, chunk stores), uploading and reading
content, manifests, feeds, postage stamps, redundancy, the Bee client, and the exceptions and best
practices references.

## Supported frameworks

The libraries multi-target **.NET 8, 9 and 10**. Install them into any project on a compatible framework.

## Building and testing

SwarmSDK builds with the standard .NET SDK:

```bash
dotnet restore SwarmSdk.sln
dotnet build   SwarmSdk.sln -c Release   # compiles every target framework
dotnet test    SwarmSdk.sln -c Release   # runs the xUnit test project
```

`TreatWarningsAsErrors=true` and `AnalysisMode=AllEnabledByDefault` are enabled across the solution, so
warnings break the build on every target framework. Because the libraries compile against the lowest
target (`net8.0`), avoid APIs introduced only in a later framework — code can pass locally on `net10.0`
yet fail the `net8.0` build. A green `dotnet build SwarmSdk.sln` means all target frameworks compiled.

Coding conventions and architecture notes live in [AGENTS.md](AGENTS.md).

## Project layout

```
src/
  SwarmSdk         the SDK — models, hashing/signing, chunking, manifests, stores, postage, services
  SwarmSdk.Client  SwarmClient over the Bee node HTTP API (→ SwarmSdk)
  SwarmSdk.Wasm    WebAssembly host exposing SDK utilities to JS (not packaged, → SwarmSdk)
test/
  SwarmSdk.UnitTest   xUnit + Moq unit tests
tools/                NSwag client generation from the Bee OpenAPI spec (see tools/README.md)
```

The generated Bee/Beehive HTTP clients are produced with NSwag from the upstream OpenAPI spec and are not
hand-maintained — see [tools/README.md](tools/README.md) for how to regenerate them.

## Package repositories

You can get the latest public releases from the [NuGet.org feed](https://www.nuget.org/profiles/etherna).

If you'd like to work with the latest internal releases, you can use our
[custom MyGet feed](https://www.myget.org/F/etherna/api/v3/index.json) (NuGet V3).

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) and our
[Code of Conduct](CODE_OF_CONDUCT.md) before opening a pull request.

## Issue reports

If you've discovered a bug, or have an idea for a new feature, please report it to our issue manager
based on Jira: https://etherna.atlassian.net/projects/SSDK.

Detailed reports with stack traces, actual and expected behaviours are welcome.

## Questions? Problems?

For questions or problems please write an email to [info@etherna.io](mailto:info@etherna.io).

## License

![LGPL Logo](https://www.gnu.org/graphics/lgplv3-with-text-154x68.png)

We use the GNU Lesser General Public License v3 (LGPL-3.0) for this project.
If you require a custom license, you can contact us at [license@etherna.io](mailto:license@etherna.io).
