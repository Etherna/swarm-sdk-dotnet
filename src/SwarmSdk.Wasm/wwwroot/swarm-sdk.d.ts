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

/** A hex string: an address, a reference, a key, a topic or an identifier. */
export type Hex = string;
/** An amount of PLUR (BZZ) or wei (xDAI): a bigint, or a decimal string. */
export type Amount = bigint | string;

export type RedundancyLevel = 'none' | 'medium' | 'strong' | 'insane' | 'paranoid' | 0 | 1 | 2 | 3 | 4;
export type RedundancyStrategy = 'none' | 'data' | 'prox' | 'race' | 0 | 1 | 2 | 3;
export type FeedType = 'sequence' | 'epoch';

/** A chunk as the chunking hands it over: address, span and data (or the full SOC), and stamp if stamped. */
export interface Chunk {
    address: Hex;
    payload: Uint8Array;
    stamp: Uint8Array | null;
}

export interface ContentOptions {
    /** Default none. */
    redundancyLevel?: RedundancyLevel;
    encrypt?: boolean;
    /** Keys tried per chunk to fill the postage buckets evenly, 0 (default) for none. */
    compactLevel?: number;
    /** Stamp the chunks client side, and compact against the buckets of its batch. */
    stamper?: Stamper;
    /** Chunks hashed (and uploaded) at once. */
    chunkConcurrency?: number;
}

export interface HashOptions extends ContentOptions {
    /** Receives every chunk, to store or upload it elsewhere. */
    onChunk?: (chunk: Chunk) => void;
}

/** Exactly one of batchId (the node stamps) and stamper (stamped here). */
export type Postage = { batchId: Hex; stamper?: undefined } | { stamper: Stamper; batchId?: undefined };

export type UploadOptions = ContentOptions & Postage & {
    /** A tag of the node, counting the chunks of the upload. */
    tag?: number | bigint;
};

export interface FileOptions {
    contentType?: string;
    fileName?: string;
}

export interface DirectoryOptions {
    indexDocument?: string;
    errorDocument?: string;
}

export interface DirectoryFile {
    path: string;
    data: Uint8Array;
}

/** A chunked content, and the postage it takes in the buckets of its stamper. */
export interface ContentResult {
    reference: Hex;
    missedOptimisticHashing: number;
    requiredBatchDepth: number;
    requiredBatchByteSize: number;
    consumedSize: number;
    remainingBatchSize: number;
}

export interface DownloadOptions {
    /** The level the root was replicated with, to look for its replicas. Default paranoid. */
    redundancyLevel?: RedundancyLevel;
    /** Default data. */
    redundancyStrategy?: RedundancyStrategy;
    /** Default true. */
    redundancyStrategyFallback?: boolean;
}

export interface ResolveOptions extends DownloadOptions {
    /** Index and error documents, redirects to directories. Default true. */
    browserResolution?: boolean;
}

export interface Resource {
    reference: Hex;
    contentType: string | null;
    fileName: string | null;
    metadata: Record<string, string>;
    isFromErrorDocument: boolean;
}

export interface Epoch {
    /** Unix time in seconds. */
    start: number | bigint;
    level: number;
}

export interface FeedUpdate {
    /** Sequence index, or epoch start. */
    index: bigint;
    epochLevel: number | null;
    address: Hex;
    /** Null on a published update. */
    payload: Uint8Array | null;
    /** The root of the content the update wraps, inline: read it with downloadFeedContent. Null on a published update. */
    wrappedChunkAddress: Hex | null;
    wrappedChunkPayload: Uint8Array | null;
    payloadVersion: 'V1' | 'V2' | null;
    timestamp: number | null;
}

export type FeedPublishOptions = Postage & {
    /** The data is the span and data of a chunk to wrap, as the root chunk of a larger content. */
    dataIsChunk?: boolean;
    tag?: number | bigint;
    deferred?: boolean;
    pin?: boolean;
};

export interface Health {
    isStatusOk: boolean;
    version: string;
    apiVersion: string;
}

export interface NodeInfo {
    beeMode: 'Light' | 'Full' | 'UltraLight' | 'Dev' | string;
    chequebookEnabled: boolean;
    swapEnabled: boolean;
}

export interface NodeAddresses {
    overlay: Hex;
    underlay: string[];
    ethereum: Hex;
    chainAddress: Hex;
    publicKey: Hex;
    pssPublicKey: Hex;
}

export interface ChainState {
    block: number;
    chainTip: number;
    /** PLUR per chunk per block. */
    currentPrice: bigint;
    totalAmount: bigint;
    minimumValidityBlocks: number;
}

export interface PostageBatch {
    id: Hex;
    /** PLUR per chunk. */
    amount: bigint | null;
    blockNumber: number;
    depth: number | null;
    exists: boolean;
    isImmutable: boolean;
    isUsable: boolean;
    label: string;
    ttlSeconds: number;
    /** Chunks in the fullest bucket. */
    utilization: number;
}

export interface PostageBuckets {
    depth: number;
    bucketDepth: number;
    bucketUpperBound: number;
    /** Chunks per bucket. */
    collisions: number[];
}

export interface Tag {
    id: number;
    address: Hex;
    startedAt: string;
    split: number;
    seen: number;
    stored: number;
    sent: number;
    synced: number;
}

export interface PinStatus {
    reference: Hex;
    creationTime: string | null;
    processed: boolean | null;
    succeeded: boolean;
    missingChunks: Hex[] | null;
    pinnedChunks: number | null;
}

export interface PinCheck {
    reference: Hex;
    total: number;
    missing: number;
    invalid: number;
}

export declare function loadSwarmSdk(options?: { frameworkUrl?: string | URL }): Promise<SwarmSdk>;

export declare class SwarmSdk {
    readonly content: {
        countChunks(dataLength: number, options?: ContentOptions): number;
        chunkAddress(payload: Uint8Array): Hex;
        hashBytes(data: Uint8Array, options?: HashOptions): Promise<ContentResult>;
        hashDirectory(files: DirectoryFile[], options?: HashOptions & DirectoryOptions): Promise<ContentResult>;
        hashFile(data: Uint8Array, options?: HashOptions & FileOptions): Promise<ContentResult>;
    };
    readonly feeds: {
        epochIdentifier(topic: Hex, epochStart: number | bigint, epochLevel: number): Hex;
        hashManifest(owner: Hex, topic: Hex, options?: { type?: FeedType; compactLevel?: number; stamper?: Stamper; onChunk?: (chunk: Chunk) => void }): Promise<Hex>;
        sequenceIdentifier(topic: Hex, index: number | bigint): Hex;
        topic(name: string): Hex;
    };
    readonly postage: {
        amountForTtl(chainPrice: Amount, ttlSeconds: number): bigint;
        maxBucketCollisions(depth: number): number;
        price(amount: Amount, depth: number): bigint;
        requiredDepth(collisions: number): number;
        ttlSeconds(amount: Amount, chainPrice: Amount): number;
    };
    readonly soc: {
        address(identifier: Hex, owner: Hex): Hex;
        ownerOf(privateKey: Hex): Hex;
        sign(identifier: Hex, data: Uint8Array, privateKey: Hex): Uint8Array;
    };
    bee(url: string | URL): BeeNode;
    createStamper(options: { batchId: Hex; depth: number; immutable?: boolean; privateKey: Hex; buckets?: Uint8Array | null }): Stamper;
}

export declare class Stamper implements Disposable {
    readonly id: number;
    /** 65536 little endian uint32 counters: save them to resume the batch later. */
    readonly buckets: Uint8Array;
    readonly requiredBatchDepth: number;
    stamp(chunkAddress: Hex): Uint8Array;
    dispose(): void;
    [Symbol.dispose](): void;
}

export declare class BeeNode {
    readonly url: string;
    uploadBytes(data: Uint8Array, options: UploadOptions): Promise<ContentResult>;
    uploadChunk(payload: Uint8Array, options: Postage & { tag?: number | bigint }): Promise<Hex>;
    uploadDirectory(files: DirectoryFile[], options: UploadOptions & DirectoryOptions): Promise<ContentResult>;
    uploadFile(data: Uint8Array, options: UploadOptions & FileOptions): Promise<ContentResult>;
    uploadSoc(payload: Uint8Array, options: FeedPublishOptions): Promise<Hex>;
    downloadBytes(reference: Hex, options?: DownloadOptions): Promise<Uint8Array>;
    downloadChunk(address: Hex): Promise<Uint8Array>;
    /** The content of a feed update, read from the root chunk it wraps inline. */
    downloadFeedContent(update: FeedUpdate, options?: DownloadOptions): Promise<Uint8Array>;
    downloadFile(address: string, options?: ResolveOptions): Promise<Resource & { data: Uint8Array }>;
    resolve(address: string, options?: ResolveOptions): Promise<Resource>;
    addresses(): Promise<NodeAddresses>;
    chainState(): Promise<ChainState>;
    health(): Promise<Health>;
    info(): Promise<NodeInfo>;
    isReady(): Promise<boolean>;
    isRetrievable(reference: Hex, options?: { redundancyLevel?: RedundancyLevel }): Promise<boolean>;
    reupload(reference: Hex, batchId: Hex, options?: { redundancyLevel?: RedundancyLevel }): Promise<void>;
    readonly feeds: {
        decodeManifest(reference: Hex): Promise<{ owner: Hex; topic: Hex; type: 'Sequence' | 'Epoch' } | null>;
        findEpochUpdate(owner: Hex, topic: Hex, at: number | bigint, options?: { knownNearEpoch?: Epoch }): Promise<FeedUpdate | null>;
        findLastSequenceUpdate(owner: Hex, topic: Hex, options?: { knownNearIndex?: number | bigint }): Promise<FeedUpdate | null>;
        updateEpoch(topic: Hex, data: Uint8Array, privateKey: Hex, options: FeedPublishOptions & { timestamp?: number | bigint; knownNearEpoch?: Epoch }): Promise<FeedUpdate>;
        updateSequence(topic: Hex, data: Uint8Array, privateKey: Hex, options: FeedPublishOptions & { knownNearIndex?: number | bigint }): Promise<FeedUpdate>;
        uploadManifest(owner: Hex, topic: Hex, options: Postage & { type?: FeedType; compactLevel?: number }): Promise<Hex>;
    };
    readonly pins: {
        /** False if the node can't find the content. */
        add(reference: Hex, options?: { redundancyLevel?: RedundancyLevel }): Promise<boolean>;
        check(reference: Hex): Promise<PinCheck>;
        checkAll(): Promise<PinCheck[]>;
        list(): Promise<Hex[]>;
        remove(reference: Hex): Promise<void>;
        status(reference: Hex): Promise<PinStatus>;
    };
    readonly postage: {
        buckets(batchId: Hex): Promise<PostageBuckets>;
        buy(options: { amount: Amount; depth: number; label?: string; immutable?: boolean; gasLimit?: number | bigint; gasPrice?: Amount }): Promise<{ batchId: Hex; txHash: Hex }>;
        dilute(batchId: Hex, depth: number, options?: { gasPrice?: Amount; gasLimit?: number | bigint }): Promise<Hex>;
        /** error is the reason the node gives when the batch isn't usable. */
        get(batchId: Hex): Promise<PostageBatch & { error: string | null }>;
        global(batchId?: Hex | null): Promise<(PostageBatch & { owner: Hex })[]>;
        list(): Promise<(PostageBatch & { error: string | null })[]>;
        setLabel(batchId: Hex, label: string): Promise<void>;
        topUp(batchId: Hex, amount: Amount, options?: { gasPrice?: Amount; gasLimit?: number | bigint }): Promise<Hex>;
    };
    readonly tags: {
        create(address?: Hex | null): Promise<Tag>;
        get(id: number | bigint): Promise<Tag>;
        list(options?: { offset?: number; limit?: number }): Promise<Tag[]>;
        remove(id: number | bigint): Promise<void>;
        update(id: number | bigint, address?: Hex | null): Promise<void>;
    };
}
