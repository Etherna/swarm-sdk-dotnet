# SwarmSDK

SwarmSDK is a [Swarm](https://ethswarm.org/) Development Kit for .NET. It provides core models and tools to work with Swarm's protocol, and a client for the [Bee](https://github.com/ethersphere/bee) node. It is a set of **libraries**, not an application.

## Build, run, test

Libraries multi-target **.NET 8, 9 and 10**; the test and WASM projects target **.NET 10 only**. `TreatWarningsAsErrors=true` and `AnalysisMode=AllEnabledByDefault` are set everywhere — warnings break the build, on every target framework.

```bash
dotnet restore SwarmSdk.sln
dotnet build SwarmSdk.sln -c Release                 # compiles every target framework
dotnet test  SwarmSdk.sln -c Release                 # runs the xUnit test projects
dotnet test test/SwarmSdk.UnitTest/SwarmSdk.UnitTest.csproj   # single project
dotnet test --filter "FullyQualifiedName~ReplicaResolverChunkStoreTest"          # single class
dotnet test --filter "FullyQualifiedName~ReplicaResolverChunkStoreTest.CanFindOriginalChunk"  # single test
dotnet run -c Release --project test/SwarmSdk.Benchmark -- --filter '*'   # benchmarks, run from the repo root
```

Because the libraries compile against the lowest target (`net8.0`), do not use APIs introduced only in a later framework — it will pass locally on `net10.0` and fail the build on `net8.0`. Use `dotnet test -f net10.0 …` to iterate on a single framework, but a green `SwarmSdk.sln` build means all of them compiled.

The libraries are **Native AOT / trim compatible** (`IsAotCompatible=true`). A normal build does *not* prove this (the trim/AOT analyzers skip the `[GeneratedCode]` clients), so verify with an AOT publish of the probe — it fails on any IL warning: `dotnet publish test/SwarmSdk.AotCompatibility -c Release -r <rid>`. See [docs/NativeAot.md](docs/NativeAot.md) for the constraints (BouncyCastle, System.Text.Json, and the manual fixes the generated clients need).

There is no frontend, web host, or database — `dotnet build` produces ready-to-pack NuGet libraries: the packages `Etherna.SwarmSdk` and `Etherna.SwarmSdk.Client`. Their ids carry the `Etherna` prefix, reserved to the organization on nuget.org, through the explicit `PackageId` of each project; project, assembly and namespace names don't follow the id: the assemblies are `SwarmSdk` and `SwarmSdk.Client`, the root namespace of both is `Etherna.SwarmSdk`. A project that becomes packable takes `Etherna.` plus its project name as id.

## Architecture

Three source projects, two test projects, the AOT probe and the benchmarks:

- **`src/SwarmSdk`** (`Etherna.SwarmSdk`) — Core models, tools, and services to work with Swarm, with no Bee-node dependency. Organized by feature folder:
  - `Models/` — Swarm domain types (`SwarmHash`, `SwarmCac`, `SwarmSoc`, `SwarmAddress`, feeds, postage stamps, `EthAddress`, …). Mostly immutable value-like types with `BuildFrom…`/`BuildNew` static factories. `EthPrivateKey` derives its public key and address once, at their first read from any thread: a derivation multiplies on the curve, and every pipeline reads the address of the owner of the replicas. `PostageBuckets` counts the buckets by collisions, one counter per level, and finds the buckets of a level scanning them. The feed models read a chunk store: their lookups and builders (`TryFindFeedChunkAtAsync`, `TryFindFeedAtAsync`, `TryFindLastFeedChunkAsync`, `BuildNextFeedChunkAsync`) and `SwarmFeedChunkBase.ResolveWrappedChunkAsync` take a cancellation token as their last parameter and pass it to every read. The sequence lookup links it to the requests of its concurrent rounds, starts no round once it is cancelled, and throws the cancellation instead of answering a result. A probe of the sequence lookup with no answer within its timeout (`requestsCustomTimeout`, 10 seconds) counts as an update not found, as in bee's own lookup; its first read, at the known index or 0, is bounded only by the token and the store, since it tells an empty feed from a feed with updates.
  - `Chunks/` — chunk-level operations: `ChunkDataStream` (seekable, lazily-fetched stream over a chunk tree), `ChunkTraverser` (it visits the chunk tree at a reference, a raw content or a manifest node of any size, then what a node references; a chunk it doesn't find is reported, and ends the traversal of what only that chunk could tell), `ChunkReplicator`, `ChunkParityDecoder` (Reed–Solomon redundancy; the shards it asks for and doesn't obtain are reported as `SwarmChunksNotFoundException`, a `KeyNotFoundException` of `Exceptions/` listing them, which `ChunkDataStream` wraps in `SwarmChunkChildrenNotFoundException` with the reference of the intermediate chunk they belong to), encryption.
  - `Stores/` — chunk/stamp store abstractions (`IReadOnlyChunkStore`, `IChunkStore`, `IStampStore`) and composable implementations/decorators (`MemoryChunkStore`, `LocalDirectoryChunkStore`, `CacheChunkStore`, `MirroringChunkStore`, `ReplicaResolverChunkStore`). Stores are decorators: compose them rather than adding branches to an existing one.
  - `Hashing/` — BMT hashing, the chunking `Pipeline/`, postage stamping (`Postage/`), and signing (`Signer/`). Cryptography is in-house (BouncyCastle), not Nethereum. A pipeline run takes a cancellation token (`IHasherPipeline.HashDataAsync`): it stops the feed at the next chunk, every chunk write receives it through `HasherPipelineFeedArgs.CancellationToken`, and a feed stopped by a cancellation or by a failed read waits for the chunks in flight before it throws. With redundancy, a full level of the chunk tree is encoded by `ChunkParityGenerator` in slices of its bytes and its parities are hashed and written in parallel, on up to the chunk concurrency of the pipeline, while the aggregator holds its mutex; the parities enter the level in their order once all are hashed, and one Reed-Solomon encoder is built per count of data and parity shards, for the process (`ReedSolomonEncoders`, shared with `ChunkParityDecoder`). The encoders code with `SimdCodingLoop`, an `ICodingLoop` of `ReedSolomon.NET` multiplying in GF(2^8) by nibble tables and a byte shuffle, 32 bytes per instruction with AVX2 and 16 with SSSE3, one byte at a time through `Galois.MultiplicationTable` without them (ARM included): the matrix and the coefficients stay the library's, so the shards are the same; `SimdCodingLoopTest` compares every instruction set the machine supports with the loop of the library. `PipelineTest.ParityChunksOfALevelAreWrittenInParallelUpToTheChunkConcurrency` pins the parallel parity writes and their bound. `PostageStampIssuer` gives the slots of a bucket from 0 to `BucketUpperBound - 1`, as bee's `StampIssuer.increment` does (bee rejects a stamp index `>= BucketUpperBound`): the counter of a bucket is the count of its chunks, and the next slot. A full bucket throws `PostageBucketFullException` (an `InvalidOperationException`) on an immutable batch, and on a mutable one restarts from slot 0 and marks `HasSaturated`: the rollover reuses the slots of the oldest chunks, which the network replaces. Bee master (ethersphere/bee#5696, after v2.8.3) fails on a full bucket for every batch instead, since a rollover can displace chunks of the same upload; the SDK keeps the rollover across uploads.
  - `Manifest/` — Mantaray manifest reading/writing and path resolution. A referenced manifest is walked at one redundancy level, chosen where it is built (`ReferencedMantarayManifest.BuildNew…`, the `SwarmAddressResolver` methods; `Paranoid` by default, as bee's download default): every node, the root and each fork fetched when it is visited, looks up its replicas at that level, and none at `None`. The path methods of a manifest (`GetResourceInfoAsync`, `GetMetadataAsync`, `HasPathPrefixAsync`, declared by `IReadOnlyMantarayManifest` and implemented once by `MantarayManifestBase` for the referenced and the writable manifest) and the `SwarmAddressResolver` ones take a cancellation token that reaches the fetch of every node of the walk, the root and each fork; `GetReferenceAsync` takes one that reaches the pipeline runs hashing the nodes of a writable manifest. A node is read through its chunk tree (`ReferencedMantarayNode.DecodeFromChunkAsync`): with enough forks, 26 with a content type and a file name, it spans more chunks than its root, joined with the redundancy strategy of the walk. A node is told from any other content on its header, read on the first data chunk before the join, and a content larger than a node can be is refused unread. A referenced manifest can be shared by concurrent lookups: a `ReferencedMantarayNode` runs its visits (`OnVisitingAsync`), fetches (`FetchChunkAsync`) and decodes (`DecodeFromChunkAsync`) one at a time, the first caller running the step and the others waiting for it on their own token, and skips each once what it does is done, so a node is fetched and decoded once; a step that fails or is cancelled leaves the node to the next caller. A writable manifest is a builder, not safe for concurrent use.
  - `Services/` — higher-level orchestration (`ChunkService`, `FeedService`) exposed via `I…Service` interfaces. Every asynchronous method of the two services takes a cancellation token as its last parameter and passes it down: to the manifest walk and the root chunk of a read, to the pipeline runs and the manifest hashing of an upload. `ChunkService` also counts the chunks an upload writes without hashing its content (`CountContentChunks`, from the length of a content with the layout `SwarmCac.TryGetIntermediateLayout` gives: data, intermediate and parity chunks and the root replicas; `CountManifestChunksAsync`, from the lengths of the serialized nodes of a manifest), assuming no chunk repeats another; the tests check both against the chunks the hasher pipeline and a writable manifest really write.
  - `Extensions/`, `JsonConverters/`, `TypeConverters/`, `Exceptions/` — supporting code.
- **`src/SwarmSdk.Client`** (`Etherna.SwarmSdk.Client`) — Client to talk to a Bee node. `SwarmClient` / `ISwarmClient` is the public entry point: every call to the node is bounded by the class of its operation (`SwarmClientTimeouts`, from 5 seconds for a read of the node's own state to 10 minutes for a transaction wait or a content transfer, overridable through the constructor; an `HttpClient` passed in needs a timeout no shorter than every bound, or the constructor throws `ArgumentException`, and the one the client builds itself takes the longest bound), and a call the node does not complete within its bound throws `SwarmClientTimeoutException` (`Exceptions/`, a `TimeoutException`), while a cancellation requested by the caller propagates as it is; `Clients/Bee/BeeGeneratedClient.cs` and `Clients/Beehive/BeehiveGeneratedClient.cs` are **NSwag-generated** wrappers over the node's HTTP API. The pin integrity check (`GET /pins/check`) is sent by `SwarmClient` itself, for both compatibilities: its answer is a stream with a JSON object per line (NDJSON), one per pinned reference, which a generated client reads as a single JSON value. `CheckPinsAsync` reads the line of one reference within the `ContentTransfer` bound; `CheckAllPinsAsync` returns the result of each pinned reference as its line arrives, an `IAsyncEnumerable` bounded on the wait for the answer and for each line, never on the whole check, which takes hours on a large node. The headers a node answers about a content are one model, `SwarmResponseHeaders`: the HEAD methods (`GetBytesHeadersAsync`, `TryGetFileHeadersAsync`) return it and a download carries it as `FileResponse.Headers`. It copies every header of the response under names that ignore the case (`All`), and types the ones a Swarm node describes a content with, null when absent or unreadable: `ContentLength`, `ContentType`, `ContentDisposition`, `ContentReference` (the `ETag` a bee node sets to the reference of a file) and `IsFeed`. The generated clients hand those headers over with no manual edit: `wrapResponses` in the `.nswag` configurations wraps the response of the HEAD operations listed in `wrapResponseMethods`, and the generated `FileResponse` is the stock one. Also `Models/` (request/response DTOs like `FileResponse`), `Stores/` (chunk stores backed by the node), and `Tools/`.
- **`src/SwarmSdk.Wasm`** — WebAssembly host exposing the SDK to JS. `IsAotCompatible=true`, not packable, not part of the public NuGet surface. Keep it AOT-safe. Two layers:
  - **Raw exports** (`*Exports.cs`, `[JSExport]`): positional arguments, enumerations as numbers (`RedundancyLevel`, `RedundancyStrategy`) or names (feed types), stampers as numbers, amounts of BZZ as PLUR strings and of xDAI as wei strings, structured results as JSON from the source generated `Json/WasmJsonContext` (camel case; SDK models serialized as they are, plus the Wasm records `ContentResult`, `FeedUpdateResult`, `ResolvedResource`…). `ContentExports` hashes bytes, files and directories offline with every option of the SDK (redundancy, encryption, compaction, stamper, chunk concurrency) and returns the postage evaluation (`ContentResult`, mirroring `UploadEvaluationResult`), handing each chunk with its stamp to an optional callback (`Stores/CallbackChunkStore`); the work is shared with the uploads in `Contents/ContentWriter`. `StamperExports` stamps client side: the pipeline stamps a chunk right before handing it to the store and drops the stamp, so `Postage/RecordingPostageStamper` keeps the stamps of a write for its store. `UploadExports` chunks here and pushes every chunk to `/chunks` and root replicas to `/soc` through `SwarmClient` (`Stores/BeeUploadChunkStore`), with either the node's batch or a stamper, and a tag. `DownloadExports` downloads through `SwarmClientChunkStore`, so every chunk is verified and the tree is walked, decrypted and repaired here (bytes, chunks, manifests resolved with `SwarmAddressResolver`, and the content of a root already at hand, as the chunk a v2 feed update wraps inline). `FeedExports` looks feeds up over verified chunks, publishes updates signed here (raw data wrapped in a chunk, or a chunk payload as is), and builds and decodes feed manifests. `PostageExports`, `TagExports`, `PinExports` (with stewardship) and `NodeExports` call the node API and add the offline postage arithmetic. `SocExports` derives keys, topics, identifiers and SOC addresses, and signs SOCs. ACT is not exported yet (SSDK-225).
  - **JavaScript API** (`wwwroot/swarm-sdk.js`, types in `wwwroot/swarm-sdk.d.ts`): `loadSwarmSdk()` loads the runtime next to it and wraps the raw exports in classes and option objects (`sdk.content`, `sdk.feeds`, `sdk.postage`, `sdk.soc`, `sdk.createStamper`, `sdk.bee(url)` with uploads, downloads, `feeds`, `postage`, `tags`, `pins`), names for enumerations, `bigint` for amounts and indexes, seconds for durations, `Uint8Array` for bytes. Keep the two in step: a raw export change updates the wrapper, its types and `sdk.mjs`.
  - **Interop limits met here**: no array as an argument of a callback, and no `Task<byte[]>`: bytes go through `object` (`JSType.Any`, `JSType.Promise<JSType.Any>`) and arrive as `Uint8Array`; a `Task<object>` holding an array must not complete synchronously (`await Task.Yield()` in `DownloadChunkContentAsync`). The runtime is single threaded: pipeline tasks and `Parallel` loops run on the JS event loop, and an upload keeps 16 chunks in flight to overlap its requests. On Node the runtime's `HttpClient` probes request streaming with `new Request('')`, which Node rejects without a base URL (`swarm-sdk.js` installs a shim, `smoke.mjs` its own), and leaves the timer of the request timeouts pending after the requests end, holding the process up to the 4 minutes of `ChunkPush`: the tests exit explicitly.
  - **Build and tests**: a publish compiles ahead of time (`RunAOTCompilation`, conditioned on `_IsPublishing` or `WasmBuildingForNestedPublish`, the nested publish where the WebAssembly SDK compiles) and needs the `wasm-tools` workload; a build of the solution doesn't (`-p:RunAOTCompilation=false` publishes on the interpreter, quicker to iterate). `test/SwarmSdk.Wasm.SmokeTest/smoke.mjs` runs the raw exports over `<publish>/wwwroot/_framework` against the values a bee node computes and a recording HTTP server; `sdk.mjs` runs the JavaScript API over `<publish>/wwwroot` end to end against `fake-bee.mjs`, which stores the uploaded chunks, serves them by address and answers the other endpoints in the shape of the bee OpenAPI. The `verify-wasm` job of `aot-compatibility.yml` installs the workload and runs both. Round trips use data whose chunks don't repeat: the SDK fails to read back a content that repeats a chunk (`ReadOnlyChunkStoreBase.GetAsync` adds the same hash twice).
- **`test/SwarmSdk.UnitTest`** — xUnit + Moq unit tests of `SwarmSdk`, mirroring its folder layout.
- **`test/SwarmSdk.Client.UnitTest`** — xUnit + Moq unit tests of `SwarmSdk.Client`, mirroring its folder layout, with `ISwarmClient` mocked where the node would be.
- **`test/SwarmSdk.Benchmark`** — BenchmarkDotNet benchmarks of `SwarmSdk`, each reporting the bytes it allocates beside its time: building and running a hasher pipeline (a kilobyte and a megabyte with and without Paranoid redundancy, a gigabyte without and with Medium), a single file uploaded through `ChunkService` with a stamper of real buckets (by length, redundancy and compaction level), deriving the addresses of a key and the replica headers, recovering a Paranoid level through `ChunkParityDecoder`, building postage buckets and the fake stamper. The runs lasting seconds use a monitoring job of a few iterations. Compare two commits by running the same benchmarks on each, on the same machine; BenchmarkDotNet finds the project from the working directory, so run it from the repo root and send `--artifacts` outside the repo.

A test project mirrors the library it tests and references only that library, so logic worth unit-testing belongs in the lowest library that can hold it: prefer placing pure, testable logic in `SwarmSdk` core (where it is covered by `SwarmSdk.UnitTest`) and keeping `SwarmSdk.Client` a thin wrapper over it. `SwarmSdk.Client.UnitTest` covers the client-side logic that cannot live in core, such as the mapping of the node answers in `SwarmClientChunkStore`.

Key cross-cutting points:

- **Root namespace is `Etherna.SwarmSdk` for every project**, so `src/SwarmSdk/Stores/Foo.cs` is `namespace Etherna.SwarmSdk.Stores`, and `src/SwarmSdk.Client/Stores/Bar.cs` is `namespace Etherna.SwarmSdk.Stores` too (the project's `.Client` suffix is dropped, not part of the namespace). The namespace mirrors the folder path *under* the root namespace.
- **`ConfigureAwait(false)` is required** on every awaited call in library code (`SwarmSdk`, `SwarmSdk.Client`) — these are libraries with no synchronization context to preserve.
- **Generated clients are not hand-maintained.** `Clients/Bee/BeeGeneratedClient.cs` and `Clients/Beehive/BeehiveGeneratedClient.cs` are produced by NSwag from the Bee OpenAPI spec; prefer regenerating over editing, and keep manual post-generation fixes minimal and documented. Public ergonomics belong in `SwarmClient`, not in the generated layer. Regeneration lives in `tools/` (see `tools/README.md`): bundle the multi-file spec with `npx @redocly/cli bundle ./bee-openapi/Swarm.yaml -o bee-openapi.yaml` (use `redocly bundle`, **never** a dereferencing tool — that drops `components/schemas` and makes NSwag emit hundreds of anonymous `ResponseN` DTOs), then run `nswag run bee-api.nswag` and `nswag run beehive-api.nswag`. To bump the API version, copy the upstream `*.yaml` from `ethersphere/bee/openapi` into `tools/original-open-api/` and merge. **After regenerating, re-apply the Native AOT post-generation fixes** documented in [docs/NativeAot.md](docs/NativeAot.md) (the source-gen serialization swaps and the `*.Aot.cs` companion contexts) — they are required for the AOT publish to stay clean.
- **Injectable time for delays/timeouts.** Code that schedules work over time takes a `TimeProvider` (defaulting to `TimeProvider.System`) so tests can drive a `FakeTimeProvider` instead of the wall clock. See `ReplicaResolverChunkStore` and its test, and `SwarmClient`, whose per-operation bounds and upload retries run on its `TimeProvider` (`SwarmClientTest` drives them against a `HangingHttpMessageHandler`, waiting for the request through its `WaitRequestOfAsync(operation)`: an operation that ends without sending one fails the test with its own exception, where a bare wait for the request would hang the run).

## Issue tracker

Bugs and features are tracked in Jira project **SSDK** (https://etherna.atlassian.net/projects/SSDK). Branch names follow `feature/SSDK-<id>-<slug>` / `improve/SSDK-<id>-<slug>` / `fix/SSDK-<id>-<slug>` — match this when creating branches.

# Coding Style

## General Principles

- Keep commits clean: only include changes strictly necessary for the task at hand.
- A security fix keeps its commit message and PR title neutral: the Jira key and what changes, never the vulnerability. The write-up stays in the Jira work item, hidden until the fixed version is released; outside reporters are directed to [SECURITY.md](SECURITY.md).
- Keep the documentation wiki aligned: user-facing docs live in the sibling `swarm-sdk-dotnet.wiki` repo (published at https://github.com/Etherna/swarm-sdk-dotnet/wiki, checked out next to this one). Every substantial change to the public API, configuration, features, or architecture must be reported there in the same work line (the wiki branch mirrors the code branch: `dev`, `hotfix/x.y.z`, `release/x.y.z`). See its own `AGENTS.md` for its conventions.
- Never reference AI agents or assistants in commits or code — no agent names, no `Co-Authored-By` agent trailers, no "generated/assisted by" notes. Commit messages and code must read as the team's own work.
- Exceptions to these conventions are accepted when strictly necessary or when they significantly improve code quality. Justify with a comment where needed.
- All elements (usings, properties, methods, fields, enum members, etc.) are always alphabetically ordered within their respective sections.
- Primary constructors are preferred everywhere the constructor is a simple parameter assignment.
- Keep code clean: remove unused variables, dead code, and redundant imports.
- Every source file starts with the standard LGPL copyright header (see any existing file); the NSwag-generated clients keep the header of their generator. The solution license is GNU LGPL, stated by the `COPYING` + `COPYING.LESSER` text pair the GitHub detector reads as LGPL-3.0 (it recognises the lesser terms by that exact file name), and declared to the package registries by the `PackageLicenseExpression` of `Directory.Build.props`: `LGPL-3.0-or-later`, the SPDX form of the header wording, set once for every package and mutually exclusive with `PackageLicenseFile`, which no project sets. Both texts are packed as package content.

## Naming

- **Classes/Structs**: PascalCase (`ChunkService`, `SwarmSoc`)
- **Interfaces**: `I` prefix (`IChunkStore`, `ISwarmClient`)
- **Async methods**: always `Async` suffix (`GetAsync`, `TryGetFeedChunkAsync`)
- **Properties**: PascalCase (`InnerChunk`, `RedundancyLevel`)
- **Private fields**: `_camelCase` only when backing a same-named property (`_items` for `Items`); otherwise plain `camelCase`
- **Primary constructor parameters**: `camelCase` without underscore
- **Constants**: PascalCase (`MaxSocSize`, `SignatureSize`)
- **Enums**: PascalCase type and members (`RedundancyLevel.Paranoid`)
- **Namespaces**: `Etherna.SwarmSdk.<Feature>` mirroring the folder under the root namespace (e.g. `Etherna.SwarmSdk.Models`, `Etherna.SwarmSdk.Stores`)
- **Custom exceptions**: `Exception` suffix, always `sealed`

## Code Organization

- One class per file, filename matches class name
- Namespace mirrors folder structure (under the `Etherna.SwarmSdk` root namespace)
- Block-scoped namespaces: `namespace X { ... }` — NOT file-scoped
- Using directives inside the namespace block, always alphabetically ordered and kept to the minimum necessary
- No global usings — each file declares its own imports

## Comments

Principal comments (generally multiline, important):
```csharp
// Capital start, ending period.
// Continued on next line if needed.
```

Secondary/separator comments:
```csharp
//no space, no capital, no ending period
```

Members should be documented with XML doc comments (`///`), with a well-composed description and no superfluous information — always for public API, and for non-public members whenever it aids understanding.

## Member Ordering Within a Class

Use principal-style section comments to delimit groups, in this order:

```csharp
// Consts.
public const int MaxLength = 100;

// Fields.
private List<Item> _items = [];

// Constructors.
public MyType(string name) { ... }

// Static builders.
public static MyType BuildNew(...) { ... }

// Properties.
public string Name { get; }

// Methods.
public void DoSomething() { ... }

// Internals.
internal void DoInternalThing() { ... }

// Static methods.
public static int Compute(...) { ... }

// Helpers.
private void InternalHelper() { ... }
```

## Class Design

- `internal sealed` for service implementations where they aren't part of the public API
- Primary constructors everywhere the constructor is a simple assignment
- Don't extract a private helper method for logic used in a single place — inline it. Reserve helpers for code shared by two or more call sites (or when extraction materially clarifies an otherwise long, complex method)
- Value-like models are immutable: expose data through read-only properties

## Async Patterns

- Always suffix with `Async`
- `CancellationToken cancellationToken = default` as the optional last parameter (non-nullable, `default` — not `CancellationToken? = null`)
- Return `Task` or `Task<T>`, never `async void`
- `ConfigureAwait(false)` on **every** awaited call in library code
- Inject `TimeProvider` instead of calling `Task.Delay(delay, ct)` directly, so delays are testable

## Null Handling

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- `ArgumentNullException.ThrowIfNull(param)` for parameter validation
- `is null` / `is not null` (not `== null`)
- Prefer `null` over `default` wherever the type admits it: optional parameter defaults, late-init member initializers (`= null!`, not `= default!`), returns and assignments. Keep `default` only where `null` can't apply: non-nullable value types (e.g. `CancellationToken cancellationToken = default`) and unconstrained generic type parameters.
- `??` and `??=` operators

## Formatting

- Allman braces (opening brace on new line)
- 4-space indentation
- Expression-bodied members for single expressions:
  ```csharp
  public override bool CanSeek => true;
  ```
- LINQ method chains: one operation per line, aligned
- Blank line between member sections

## C# Language Features

- Pattern matching: `is`, `is not`, type patterns, property patterns
- Switch expressions for multi-branch returns
- Primary constructors everywhere applicable
- Collection expressions: `[]`, `[..spread]`
- Prefer collection expressions over constructors to initialize any collection: `[]` not `new()`, `["a", "b"]` not `new List<string> { "a", "b" }`. Use a constructor only when a collection expression can't express the intent (e.g. presizing capacity with `new List<T>(capacity)`).
- Target-typed `new()` when type is clear from context (for non-collection types)
- Tuple deconstruction for multiple return values
- Span/`Memory<byte>` and ranges (`data[..32]`, `data[cursor..]`) for byte-level slicing — avoid unnecessary copies
- Lock fields: prefer the dedicated `System.Threading.Lock` type (.NET 9+) over a plain `object` — more expressive, and the compiler enforces correct `lock` usage on it. Here it applies only once the `net8.0` target is dropped: the lowest-target rule forbids .NET 9+ APIs today.

## LINQ

- Method syntax preferred over query syntax
- Query syntax only for complex join/groupby with multiple `from` clauses
- Fluent chaining, one operation per line for readability

## Testing (xUnit + Moq)

- `[Fact]` for basic tests, `[Theory]` with `[MemberData]` for parameterized cases
- AAA pattern with section comments: `// Setup.`, `// Run.`, `// Assert.` (collapse to `// Run & Assert.` when a single statement does both)
- xUnit assertions: `Assert.Equal()`, `Assert.NotNull()`, `Assert.Throws<T>()` / `Assert.ThrowsAsync<T>()`
- Moq for mocking: `new Mock<IChunkStore>()`
- `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) for anything time-dependent — never rely on wall-clock sleeps for ordering; advance the fake clock instead
- Each test project mirrors the folder layout of its library (e.g. `Stores/ReplicaResolverChunkStoreTest.cs`)
- **No `ConfigureAwait` in test code.** The opposite of the library rule: inside `[Fact]`/`[Theory]` methods *and* any async helper `await`ed from a test, write plain `await foo()` — the xUnit analyzer rule `xUnit1030` errors on `ConfigureAwait(false)` and (with `TreatWarningsAsErrors`) breaks the build. `CA2007` is not enforced here, so omitting it is correct. The only exception is an async helper invoked synchronously (e.g. `.Wait()` from a `[MemberData]` property getter), which never runs on a test's async context and may keep `ConfigureAwait(false)`.
