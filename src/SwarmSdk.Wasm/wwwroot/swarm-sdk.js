// Copyright 2021-present Etherna SA
// This file is part of SwarmSDK.
//
// SwarmSDK is free software: you can redistribute it and/or modify it under the terms of the
// GNU Lesser General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
//
// SwarmSDK is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License along with SwarmSDK.
// If not, see <https://www.gnu.org/licenses/>.

// The JavaScript API of SwarmSDK: it loads the .NET runtime of the WebAssembly host, and wraps its raw exports
// (positional arguments, numbered enumerations and stampers, JSON results) in classes and option objects.
// Types are in swarm-sdk.d.ts.

const redundancyLevels = { none: 0, medium: 1, strong: 2, insane: 3, paranoid: 4 };
const redundancyStrategies = { none: 0, data: 1, prox: 2, race: 3 };

/**
 * Load the WebAssembly host and return the SDK.
 * @param {{ frameworkUrl?: string | URL }} [options] Where the _framework directory of the published host is,
 * by default next to this module.
 */
export async function loadSwarmSdk(options = {}) {
    installNodeShims();
    const frameworkUrl = new URL(options.frameworkUrl ?? './_framework/', import.meta.url);
    const { dotnet } = await import(new URL('dotnet.js', ensureDirectory(frameworkUrl)).href);
    const runtime = await dotnet.create();
    const exports = (await runtime.getAssemblyExports('SwarmSdk.Wasm.dll')).Etherna.SwarmSdk.Wasm;
    return new SwarmSdk(exports);
}

/** The SDK: offline operations, stampers, and the bee nodes to talk to. */
export class SwarmSdk {
    #x;

    constructor(exports) {
        this.#x = exports;
        this.content = new Contents(exports);
        this.feeds = new Feeds(exports);
        this.postage = new PostageMath(exports);
        this.soc = new SingleOwnerChunks(exports);
    }

    /** A bee node, by its API URL. */
    bee(url) {
        return new BeeNode(this.#x, String(url));
    }

    /** A stamper signing the chunks of a postage batch client side. Dispose it when done. */
    createStamper({ batchId, depth, immutable = false, privateKey, buckets = null }) {
        const id = this.#x.StamperExports.CreateStamper(batchId, depth, immutable, privateKey, buckets);
        return new Stamper(this.#x, id);
    }
}

/** Client side postage stamping of a batch. */
export class Stamper {
    #x;
    #disposed = false;

    constructor(exports, id) {
        this.#x = exports;
        this.id = id;
    }

    /** The bucket counters of the batch: save them, and give them back to createStamper to resume. */
    get buckets() {
        return this.#x.StamperExports.GetStamperBuckets(this.#live());
    }

    /** The depth the batch needs to hold the chunks stamped so far. */
    get requiredBatchDepth() {
        return this.#x.StamperExports.GetStamperRequiredBatchDepth(this.#live());
    }

    /** Stamp a chunk by its hex address: 113 bytes, the value of the swarm-postage-stamp header. */
    stamp(chunkAddress) {
        return this.#x.StamperExports.StampChunk(this.#live(), chunkAddress);
    }

    dispose() {
        if (this.#disposed)
            return;
        this.#x.StamperExports.DisposeStamper(this.id);
        this.#disposed = true;
    }

    [Symbol.dispose]() {
        this.dispose();
    }

    #live() {
        if (this.#disposed)
            throw new Error('The stamper is disposed');
        return this.id;
    }
}

/** Offline chunking and hashing. */
class Contents {
    #x;

    constructor(exports) {
        this.#x = exports;
    }

    /** The chunks an upload of a content of the given length writes. */
    countChunks(dataLength, options = {}) {
        const o = contentOptions(options);
        return this.#x.ContentExports.CountContentChunks(dataLength, o.redundancyLevel, o.encrypt, o.compactLevel);
    }

    /** The hex address of a content addressed chunk from its span and data. */
    chunkAddress(payload) {
        return this.#x.ContentExports.GetChunkAddress(payload);
    }

    async hashBytes(data, options = {}) {
        const o = contentOptions(options);
        return contentResult(await this.#x.ContentExports.HashBytesAsync(
            data, o.redundancyLevel, o.encrypt, o.compactLevel, o.stamper, o.chunkConcurrency, o.onChunk));
    }

    async hashDirectory(files, options = {}) {
        const o = contentOptions(options);
        const d = directory(files);
        return contentResult(await this.#x.ContentExports.HashDirectoryAsync(
            d.paths, d.contents, d.lengths, options.indexDocument ?? null, options.errorDocument ?? null,
            o.redundancyLevel, o.encrypt, o.compactLevel, o.stamper, o.chunkConcurrency, o.onChunk));
    }

    async hashFile(data, options = {}) {
        const o = contentOptions(options);
        return contentResult(await this.#x.ContentExports.HashFileAsync(
            data, options.contentType ?? 'application/octet-stream', options.fileName ?? null,
            o.redundancyLevel, o.encrypt, o.compactLevel, o.stamper, o.chunkConcurrency, o.onChunk));
    }
}

/** Offline feed identifiers, topics and manifests. */
class Feeds {
    #x;

    constructor(exports) {
        this.#x = exports;
    }

    epochIdentifier(topic, epochStart, epochLevel) {
        return this.#x.SocExports.BuildEpochFeedIdentifier(topic, Number(epochStart), epochLevel);
    }

    /** Build the manifest of a feed without uploading it, and return its hex reference. */
    async hashManifest(owner, topic, options = {}) {
        const o = contentOptions(options);
        return await this.#x.FeedExports.HashFeedManifestAsync(
            owner, topic, options.type ?? 'sequence', o.compactLevel, o.stamper, o.onChunk);
    }

    sequenceIdentifier(topic, index) {
        return this.#x.SocExports.BuildSequenceFeedIdentifier(topic, Number(index));
    }

    /** The topic of a feed from its name. */
    topic(name) {
        return this.#x.SocExports.BuildFeedTopic(name);
    }
}

/** Offline arithmetic of postage batches. Amounts are PLUR as bigint or decimal strings. */
class PostageMath {
    #x;

    constructor(exports) {
        this.#x = exports;
    }

    amountForTtl(chainPrice, ttlSeconds) {
        return BigInt(this.#x.PostageExports.CalculateAmount(String(chainPrice), Number(ttlSeconds)));
    }

    maxBucketCollisions(depth) {
        return this.#x.PostageExports.GetMaxBucketCollisions(depth);
    }

    price(amount, depth) {
        return BigInt(this.#x.PostageExports.CalculatePrice(String(amount), depth));
    }

    requiredDepth(collisions) {
        return this.#x.PostageExports.GetRequiredBatchDepth(collisions);
    }

    ttlSeconds(amount, chainPrice) {
        return this.#x.PostageExports.CalculateTtl(String(amount), String(chainPrice));
    }
}

/** Keys and single owner chunks. */
class SingleOwnerChunks {
    #x;

    constructor(exports) {
        this.#x = exports;
    }

    address(identifier, owner) {
        return this.#x.SocExports.BuildSocAddress(identifier, owner);
    }

    ownerOf(privateKey) {
        return this.#x.SocExports.GetAddressFromPrivateKey(privateKey);
    }

    /** Build and sign a single owner chunk wrapping up to 4096 bytes: identifier, signature, span and data. */
    sign(identifier, data, privateKey) {
        return this.#x.SocExports.BuildSignedSoc(identifier, data, privateKey);
    }
}

/** A bee node: contents are chunked, verified and stamped here, the node only stores and serves chunks. */
export class BeeNode {
    #x;

    constructor(exports, url) {
        this.#x = exports;
        this.url = url;
        this.feeds = new NodeFeeds(exports, url);
        this.pins = new Pins(exports, url);
        this.postage = new PostageBatches(exports, url);
        this.tags = new Tags(exports, url);
    }

    // Uploads.
    async uploadBytes(data, options) {
        const o = uploadOptions(options);
        return contentResult(await this.#x.UploadExports.UploadBytesAsync(
            this.url, data, o.redundancyLevel, o.encrypt, o.compactLevel, o.batchId, o.stamper,
            o.chunkConcurrency, o.tag));
    }

    async uploadChunk(payload, options) {
        const o = uploadOptions(options);
        return await this.#x.UploadExports.UploadChunkAsync(this.url, payload, o.batchId, o.stamper, o.tag);
    }

    async uploadDirectory(files, options) {
        const o = uploadOptions(options);
        const d = directory(files);
        return contentResult(await this.#x.UploadExports.UploadDirectoryAsync(
            this.url, d.paths, d.contents, d.lengths, options.indexDocument ?? null, options.errorDocument ?? null,
            o.redundancyLevel, o.encrypt, o.compactLevel, o.batchId, o.stamper, o.chunkConcurrency, o.tag));
    }

    async uploadFile(data, options) {
        const o = uploadOptions(options);
        return contentResult(await this.#x.UploadExports.UploadFileAsync(
            this.url, data, options.contentType ?? 'application/octet-stream', options.fileName ?? null,
            o.redundancyLevel, o.encrypt, o.compactLevel, o.batchId, o.stamper, o.chunkConcurrency, o.tag));
    }

    /** Upload a signed single owner chunk, as soc.sign returns it. */
    async uploadSoc(payload, options) {
        const o = uploadOptions(options);
        return await this.#x.UploadExports.UploadSocAsync(
            this.url, payload, o.batchId, o.stamper, o.tag, options.deferred ?? false, options.pin ?? null);
    }

    // Downloads.
    async downloadBytes(reference, options = {}) {
        const o = downloadOptions(options);
        return await this.#x.DownloadExports.DownloadBytesAsync(
            this.url, reference, o.redundancyLevel, o.redundancyStrategy, o.fallback);
    }

    /** Download the content of a feed update, from the root chunk it wraps. */
    async downloadFeedContent(update, options = {}) {
        const o = downloadOptions(options);
        return await this.#x.DownloadExports.DownloadChunkContentAsync(
            this.url, update.wrappedChunkPayload, o.redundancyStrategy, o.fallback);
    }

    async downloadChunk(address) {
        return await this.#x.DownloadExports.DownloadChunkAsync(this.url, address);
    }

    /** Download the resource at reference/path, with its content type, file name and metadata. */
    async downloadFile(address, options = {}) {
        const resource = await this.resolve(address, options);
        const data = await this.downloadBytes(resource.reference, options);
        return { ...resource, data };
    }

    /** Resolve the resource at reference/path in its manifest, without downloading it. */
    async resolve(address, options = {}) {
        const o = downloadOptions(options);
        const resource = JSON.parse(await this.#x.DownloadExports.ResolveResourceAsync(
            this.url, address, options.browserResolution ?? true, o.redundancyLevel, o.redundancyStrategy,
            o.fallback));
        return {
            reference: resource.reference,
            contentType: resource.metadata['Content-Type'] ?? null,
            fileName: resource.metadata['Filename'] ?? null,
            metadata: resource.metadata,
            isFromErrorDocument: resource.isFromErrorDoc,
        };
    }

    // Node state.
    async chainState() {
        const state = JSON.parse(await this.#x.NodeExports.GetChainStateAsync(this.url));
        return { ...state, currentPrice: BigInt(state.currentPrice), totalAmount: BigInt(state.totalAmount) };
    }

    async health() {
        return JSON.parse(await this.#x.NodeExports.GetHealthAsync(this.url));
    }

    async addresses() {
        return JSON.parse(await this.#x.NodeExports.GetNodeAddressesAsync(this.url));
    }

    async info() {
        return JSON.parse(await this.#x.NodeExports.GetNodeInfoAsync(this.url));
    }

    async isReady() {
        return await this.#x.NodeExports.GetReadinessAsync(this.url);
    }

    // Stewardship.
    async isRetrievable(reference, options = {}) {
        return await this.#x.PinExports.IsContentRetrievableAsync(
            this.url, reference, optionalLevel(options.redundancyLevel));
    }

    async reupload(reference, batchId, options = {}) {
        await this.#x.PinExports.ReuploadContentAsync(
            this.url, reference, batchId, optionalLevel(options.redundancyLevel));
    }
}

/** Feeds of a node, looked up and verified here. */
class NodeFeeds {
    #x;
    #url;

    constructor(exports, url) {
        this.#x = exports;
        this.#url = url;
    }

    /** The owner and topic a feed manifest points to, or null. */
    async decodeManifest(reference) {
        return parseOrNull(await this.#x.FeedExports.DecodeFeedManifestAsync(this.#url, reference));
    }

    /** The update of an epoch feed valid at a unix time in seconds, or null. */
    async findEpochUpdate(owner, topic, at, options = {}) {
        return feedUpdate(parseOrNull(await this.#x.FeedExports.FindEpochFeedUpdateAsync(
            this.#url, owner, topic, Number(at), optionalNumber(options.knownNearEpoch?.start),
            options.knownNearEpoch?.level ?? null)));
    }

    /** The last update of a sequence feed, or null. */
    async findLastSequenceUpdate(owner, topic, options = {}) {
        return feedUpdate(parseOrNull(await this.#x.FeedExports.FindLastSequenceFeedUpdateAsync(
            this.#url, owner, topic, optionalNumber(options.knownNearIndex))));
    }

    /**
     * Publish the next update of an epoch feed, signed by the private key of its owner. It wraps the data, up to
     * 4096 bytes, or with dataIsChunk the span and data of a chunk, as the root of a larger content.
     */
    async updateEpoch(topic, data, privateKey, options) {
        const o = uploadOptions(options);
        return feedUpdate(JSON.parse(await this.#x.FeedExports.UpdateEpochFeedAsync(
            this.#url, topic, data, options.dataIsChunk ?? false, privateKey, optionalNumber(options.timestamp),
            optionalNumber(options.knownNearEpoch?.start), options.knownNearEpoch?.level ?? null,
            o.batchId, o.stamper, o.tag, options.deferred ?? false, options.pin ?? null)));
    }

    /** Publish the next update of a sequence feed, signed by the private key of its owner. Data as updateEpoch. */
    async updateSequence(topic, data, privateKey, options) {
        const o = uploadOptions(options);
        return feedUpdate(JSON.parse(await this.#x.FeedExports.UpdateSequenceFeedAsync(
            this.#url, topic, data, options.dataIsChunk ?? false, privateKey, optionalNumber(options.knownNearIndex),
            o.batchId, o.stamper, o.tag, options.deferred ?? false, options.pin ?? null)));
    }

    /** Upload the manifest of a feed, and return its hex reference. */
    async uploadManifest(owner, topic, options) {
        const o = uploadOptions(options);
        return await this.#x.FeedExports.UploadFeedManifestAsync(
            this.#url, owner, topic, options.type ?? 'sequence', o.compactLevel, o.batchId, o.stamper);
    }
}

/** Pins of a node. */
class Pins {
    #x;
    #url;

    constructor(exports, url) {
        this.#x = exports;
        this.#url = url;
    }

    async add(reference, options = {}) {
        return await this.#x.PinExports.CreatePinAsync(this.#url, reference, optionalLevel(options.redundancyLevel));
    }

    async check(reference) {
        return JSON.parse(await this.#x.PinExports.CheckPinAsync(this.#url, reference));
    }

    async checkAll() {
        return JSON.parse(await this.#x.PinExports.CheckAllPinsAsync(this.#url));
    }

    async list() {
        return JSON.parse(await this.#x.PinExports.GetAllPinsAsync(this.#url));
    }

    async remove(reference) {
        await this.#x.PinExports.DeletePinAsync(this.#url, reference);
    }

    async status(reference) {
        return JSON.parse(await this.#x.PinExports.GetPinStatusAsync(this.#url, reference));
    }
}

/** Postage batches of a node and of the chain. Amounts are PLUR, gas prices wei, as bigint or strings. */
class PostageBatches {
    #x;
    #url;

    constructor(exports, url) {
        this.#x = exports;
        this.#url = url;
    }

    async buckets(batchId) {
        return JSON.parse(await this.#x.PostageExports.GetPostageBatchBucketsAsync(this.#url, batchId));
    }

    async buy({ amount, depth, label = null, immutable = null, gasLimit = null, gasPrice = null }) {
        return JSON.parse(await this.#x.PostageExports.BuyPostageBatchAsync(
            this.#url, String(amount), depth, label, immutable, optionalNumber(gasLimit), optionalString(gasPrice)));
    }

    async dilute(batchId, depth, { gasPrice = null, gasLimit = null } = {}) {
        return await this.#x.PostageExports.DilutePostageBatchAsync(
            this.#url, batchId, depth, optionalString(gasPrice), optionalNumber(gasLimit));
    }

    async get(batchId) {
        return ownedBatch(JSON.parse(await this.#x.PostageExports.GetPostageBatchAsync(this.#url, batchId)));
    }

    /** The valid batches of the chain, with their owners, or the one with the given id. */
    async global(batchId = null) {
        return JSON.parse(await this.#x.PostageExports.GetGlobalValidPostageBatchesAsync(this.#url, batchId))
            .map(b => ({ ...postageBatch(b.postageBatch), owner: b.owner }));
    }

    async list() {
        return JSON.parse(await this.#x.PostageExports.GetOwnedPostageBatchesAsync(this.#url)).map(ownedBatch);
    }

    async setLabel(batchId, label) {
        await this.#x.PostageExports.UpdatePostageBatchLabelAsync(this.#url, batchId, label);
    }

    async topUp(batchId, amount, { gasPrice = null, gasLimit = null } = {}) {
        return await this.#x.PostageExports.TopUpPostageBatchAsync(
            this.#url, batchId, String(amount), optionalString(gasPrice), optionalNumber(gasLimit));
    }
}

/** Upload tags of a node. */
class Tags {
    #x;
    #url;

    constructor(exports, url) {
        this.#x = exports;
        this.#url = url;
    }

    async create(address = null) {
        return JSON.parse(await this.#x.TagExports.CreateTagAsync(this.#url, address));
    }

    async get(id) {
        return JSON.parse(await this.#x.TagExports.GetTagAsync(this.#url, Number(id)));
    }

    async list({ offset = null, limit = null } = {}) {
        return JSON.parse(await this.#x.TagExports.ListTagsAsync(this.#url, offset, limit));
    }

    async remove(id) {
        await this.#x.TagExports.DeleteTagAsync(this.#url, Number(id));
    }

    async update(id, address = null) {
        await this.#x.TagExports.UpdateTagAsync(this.#url, Number(id), address);
    }
}

// Helpers.
function contentOptions(options) {
    return {
        redundancyLevel: level(options.redundancyLevel ?? 'none', redundancyLevels, 'redundancy level'),
        encrypt: options.encrypt ?? false,
        compactLevel: options.compactLevel ?? 0,
        stamper: options.stamper?.id ?? null,
        chunkConcurrency: options.chunkConcurrency ?? null,
        onChunk: options.onChunk
            ? (address, payload, stamp) => options.onChunk({ address, payload, stamp })
            : null,
    };
}

function contentResult(json) {
    return JSON.parse(json);
}

function directory(files) {
    const paths = files.map(f => f.path);
    const lengths = files.map(f => f.data.length);
    const contents = new Uint8Array(lengths.reduce((a, b) => a + b, 0));
    let offset = 0;
    for (const file of files) {
        contents.set(file.data, offset);
        offset += file.data.length;
    }
    return { paths, contents, lengths };
}

function downloadOptions(options) {
    return {
        redundancyLevel: level(options.redundancyLevel ?? 'paranoid', redundancyLevels, 'redundancy level'),
        redundancyStrategy: level(options.redundancyStrategy ?? 'data', redundancyStrategies, 'redundancy strategy'),
        fallback: options.redundancyStrategyFallback ?? true,
    };
}

function ensureDirectory(url) {
    return url.href.endsWith('/') ? url : new URL(url.href + '/');
}

function feedUpdate(update) {
    if (update === null)
        return null;
    return {
        index: BigInt(update.index),
        epochLevel: update.epochLevel,
        address: update.address,
        payload: fromBase64(update.payload),
        wrappedChunkAddress: update.wrappedChunkAddress,
        wrappedChunkPayload: fromBase64(update.wrappedChunkPayload),
        payloadVersion: update.payloadVersion,
        timestamp: update.timestamp,
    };
}

function fromBase64(value) {
    if (value === null || value === undefined)
        return null;
    const binary = atob(value);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++)
        bytes[i] = binary.charCodeAt(i);
    return bytes;
}

/**
 * In Node the .NET runtime probes the streaming of request bodies with new Request(''), which Node rejects
 * without a base URL: give it one, for that probe only.
 */
function installNodeShims() {
    if (typeof process === 'undefined' || !process.versions?.node || typeof Request === 'undefined')
        return;
    try {
        new Request('');
    } catch {
        const NodeRequest = globalThis.Request;
        globalThis.Request = class extends NodeRequest {
            constructor(input, init) {
                super(input === '' ? 'http://localhost/' : input, init);
            }
        };
    }
}

function level(value, names, what) {
    if (typeof value === 'number')
        return value;
    const number = names[value];
    if (number === undefined)
        throw new RangeError(`Unknown ${what}: ${value}`);
    return number;
}

function optionalLevel(value) {
    return value === undefined || value === null ? null : level(value, redundancyLevels, 'redundancy level');
}

function optionalNumber(value) {
    return value === undefined || value === null ? null : Number(value);
}

function optionalString(value) {
    return value === undefined || value === null ? null : String(value);
}

function ownedBatch(response) {
    return { ...postageBatch(response.postageBatch), error: response.error };
}

function parseOrNull(json) {
    return json === null || json === undefined ? null : JSON.parse(json);
}

function postageBatch(batch) {
    const { ttl, amount, ...rest } = batch;
    return { ...rest, amount: amount === null ? null : BigInt(amount), ttlSeconds: timeSpanSeconds(ttl) };
}

/** Seconds of a .NET TimeSpan in its constant format, [-][d.]hh:mm:ss[.fffffff]. */
function timeSpanSeconds(value) {
    const match = /^(-)?(?:(\d+)\.)?(\d+):(\d+):(\d+)(?:\.(\d+))?$/.exec(value);
    if (!match)
        throw new Error(`Unexpected duration ${value}`);
    const [, sign, days = '0', hours, minutes, seconds] = match;
    const total = ((Number(days) * 24 + Number(hours)) * 60 + Number(minutes)) * 60 + Number(seconds);
    return sign ? -total : total;
}

function uploadOptions(options) {
    if (!options || (options.batchId === undefined) === (options.stamper === undefined))
        throw new TypeError('Give either a batchId or a stamper');
    return {
        ...contentOptions(options),
        batchId: options.batchId ?? null,
        tag: optionalNumber(options.tag),
    };
}
