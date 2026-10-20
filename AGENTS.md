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
```

Because the libraries compile against the lowest target (`net8.0`), do not use APIs introduced only in a later framework — it will pass locally on `net10.0` and fail the build on `net8.0`. Use `dotnet test -f net10.0 …` to iterate on a single framework, but a green `SwarmSdk.sln` build means all of them compiled.

The libraries are **Native AOT / trim compatible** (`IsAotCompatible=true`). A normal build does *not* prove this (the trim/AOT analyzers skip the `[GeneratedCode]` clients), so verify with an AOT publish of the probe — it fails on any IL warning: `dotnet publish test/SwarmSdk.AotCompatibility -c Release -r <rid>`. See [docs/NativeAot.md](docs/NativeAot.md) for the constraints (BouncyCastle, System.Text.Json, and the manual fixes the generated clients need).

There is no frontend, web host, or database — `dotnet build` produces ready-to-pack NuGet libraries (`SwarmSdk`, `SwarmSdk.Client`).

## Architecture

Three source projects plus two test projects:

- **`src/SwarmSdk`** (`SwarmSdk`) — Core models, tools, and services to work with Swarm, with no Bee-node dependency. Organized by feature folder:
  - `Models/` — Swarm domain types (`SwarmHash`, `SwarmCac`, `SwarmSoc`, `SwarmAddress`, feeds, postage stamps, `EthAddress`, …). Mostly immutable value-like types with `BuildFrom…`/`BuildNew` static factories.
  - `Chunks/` — chunk-level operations: `ChunkDataStream` (seekable, lazily-fetched stream over a chunk tree), `ChunkTraverser`, `ChunkReplicator`, `ChunkParityDecoder` (Reed–Solomon redundancy), encryption.
  - `Stores/` — chunk/stamp store abstractions (`IReadOnlyChunkStore`, `IChunkStore`, `IStampStore`) and composable implementations/decorators (`MemoryChunkStore`, `LocalDirectoryChunkStore`, `CacheChunkStore`, `MirroringChunkStore`, `ReplicaResolverChunkStore`). Stores are decorators: compose them rather than adding branches to an existing one.
  - `Hashing/` — BMT hashing, the chunking `Pipeline/`, postage stamping (`Postage/`), and signing (`Signer/`). Cryptography is in-house (BouncyCastle), not Nethereum.
  - `Manifest/` — Mantaray manifest reading/writing and path resolution.
  - `Services/` — higher-level orchestration (`ChunkService`, `FeedService`) exposed via `I…Service` interfaces.
  - `Extensions/`, `JsonConverters/`, `TypeConverters/`, `Exceptions/` — supporting code.
- **`src/SwarmSdk.Client`** (`SwarmSdk.Client`) — Client to talk to a Bee node. `SwarmClient` / `ISwarmClient` is the public entry point; `Clients/Bee/BeeGeneratedClient.cs` and `Clients/Beehive/BeehiveGeneratedClient.cs` are **NSwag-generated** wrappers over the node's HTTP API. Also `Models/` (request/response DTOs like `FileResponse`), `Stores/` (chunk stores backed by the node), and `Tools/`.
- **`src/SwarmSdk.Wasm`** — WebAssembly host exposing SDK utilities to JS. `IsAotCompatible=true`, not packable, not part of the public NuGet surface. Keep it AOT-safe.
- **`test/SwarmSdk.UnitTest`** — xUnit + Moq unit tests of `SwarmSdk`, mirroring its folder layout.
- **`test/SwarmSdk.Client.UnitTest`** — xUnit + Moq unit tests of `SwarmSdk.Client`, mirroring its folder layout, with `ISwarmClient` mocked where the node would be.

A test project mirrors the library it tests and references only that library, so logic worth unit-testing belongs in the lowest library that can hold it: prefer placing pure, testable logic in `SwarmSdk` core (where it is covered by `SwarmSdk.UnitTest`) and keeping `SwarmSdk.Client` a thin wrapper over it. `SwarmSdk.Client.UnitTest` covers the client-side logic that cannot live in core, such as the mapping of the node answers in `SwarmClientChunkStore`.

Key cross-cutting points:

- **Root namespace is `Etherna.SwarmSdk` for every project**, so `src/SwarmSdk/Stores/Foo.cs` is `namespace Etherna.SwarmSdk.Stores`, and `src/SwarmSdk.Client/Stores/Bar.cs` is `namespace Etherna.SwarmSdk.Stores` too (the project's `.Client` suffix is dropped, not part of the namespace). The namespace mirrors the folder path *under* the root namespace.
- **`ConfigureAwait(false)` is required** on every awaited call in library code (`SwarmSdk`, `SwarmSdk.Client`) — these are libraries with no synchronization context to preserve.
- **Generated clients are not hand-maintained.** `Clients/Bee/BeeGeneratedClient.cs` and `Clients/Beehive/BeehiveGeneratedClient.cs` are produced by NSwag from the Bee OpenAPI spec; prefer regenerating over editing, and keep manual post-generation fixes minimal and documented. Public ergonomics belong in `SwarmClient`, not in the generated layer. Regeneration lives in `tools/` (see `tools/README.md`): bundle the multi-file spec with `npx @redocly/cli bundle ./bee-openapi/Swarm.yaml -o bee-openapi.yaml` (use `redocly bundle`, **never** a dereferencing tool — that drops `components/schemas` and makes NSwag emit hundreds of anonymous `ResponseN` DTOs), then run `nswag run bee-api.nswag` and `nswag run beehive-api.nswag`. To bump the API version, copy the upstream `*.yaml` from `ethersphere/bee/openapi` into `tools/original-open-api/` and merge. **After regenerating, re-apply the Native AOT post-generation fixes** documented in [docs/NativeAot.md](docs/NativeAot.md) (the source-gen serialization swaps and the `*.Aot.cs` companion contexts) — they are required for the AOT publish to stay clean.
- **Injectable time for delays/timeouts.** Code that schedules work over time takes a `TimeProvider` (defaulting to `TimeProvider.System`) so tests can drive a `FakeTimeProvider` instead of the wall clock. See `ReplicaResolverChunkStore` and its test.

## Issue tracker

Bugs and features are tracked in Jira project **SSDK** (https://etherna.atlassian.net/projects/SSDK). Branch names follow `feature/SSDK-<id>-<slug>` / `improve/SSDK-<id>-<slug>` / `fix/SSDK-<id>-<slug>` — match this when creating branches.

# Coding Style

## General Principles

- Keep commits clean: only include changes strictly necessary for the task at hand.
- Keep the documentation wiki aligned: user-facing docs live in the sibling `swarm-sdk-dotnet.wiki` repo (published at https://github.com/Etherna/swarm-sdk-dotnet/wiki, checked out next to this one). Every substantial change to the public API, configuration, features, or architecture must be reported there in the same work line (the wiki branch mirrors the code branch: `dev`, `hotfix/x.y.z`, `release/x.y.z`). See its own `AGENTS.md` for its conventions.
- Never reference AI agents or assistants in commits or code — no agent names, no `Co-Authored-By` agent trailers, no "generated/assisted by" notes. Commit messages and code must read as the team's own work.
- Exceptions to these conventions are accepted when strictly necessary or when they significantly improve code quality. Justify with a comment where needed.
- All elements (usings, properties, methods, fields, enum members, etc.) are always alphabetically ordered within their respective sections.
- Primary constructors are preferred everywhere the constructor is a simple parameter assignment.
- Keep code clean: remove unused variables, dead code, and redundant imports.
- Every source file starts with the standard LGPL copyright header (see any existing file).

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
- When a call's or constructor's arguments are wrapped one per line, keep the **first** argument on the opening-parenthesis line and align the rest under it — don't drop the first argument onto its own line. Skip this only when the first argument is itself long enough to warrant its own line.
  ```csharp
  new(id: id,
      amount: null,
      depth: PostageBatch.MinDepth,
      utilization: 0);
  ```
  This applies to invocation argument lists; multi-line method/constructor *parameter declarations* keep the first parameter on its own line, as elsewhere in the codebase.
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
