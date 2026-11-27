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

// Runs the JavaScript API of the published SwarmSdk.Wasm (wwwroot/swarm-sdk.js) on Node, end to end against a
// fake bee node: uploads read back through verified downloads, feeds published and looked up, and the postage,
// tag, pin and node endpoints.
// Usage: node sdk.mjs <publish directory>/wwwroot

import assert from 'node:assert/strict';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { fakeBatch, fakeTag, fakeTxHash, startFakeBee } from './fake-bee.mjs';

const wwwroot = process.argv[2];
if (!wwwroot) {
    console.error('Usage: node sdk.mjs <publish directory>/wwwroot');
    process.exit(2);
}

const { loadSwarmSdk } = await import(pathToFileURL(path.resolve(wwwroot, 'swarm-sdk.js')).href);
const sdk = await loadSwarmSdk();
const fake = await startFakeBee(sdk);
const bee = sdk.bee(fake.url);

const encoder = new TextEncoder();
const hello = encoder.encode('Bzz over the world!');
const data = new Uint8Array(3 * 1024 * 1024 + 17).map((_, i) => (i * 31) & 255);
//data whose chunks don't repeat each other, for the round trips: the SDK can't read back yet a content that
//repeats a chunk, so every 4096 bytes start with their own index
const unique = new Uint8Array(3 * 1024 * 1024 + 17).map((_, i) => (i * 31) & 255);
for (let i = 0; i < unique.length; i += 4096)
    new DataView(unique.buffer).setUint32(i, i / 4096);
const batchId = fakeBatch.batchID;
const privateKey = '0x' + '11'.repeat(32);
const owner = sdk.soc.ownerOf(privateKey);

try {
    // Contents: hashing returns the reference and the postage it takes.
    const hashed = await sdk.content.hashBytes(data, { redundancyLevel: 'medium' });
    assert.equal(hashed.reference, '45060bc4d35fc45b1f421f05b2415beefadbccbf31121129bd23b868ace99d2f');
    assert.equal(hashed.requiredBatchDepth, 17);
    assert.equal(hashed.requiredBatchByteSize, 2 ** 17 * 4096);
    assert.equal(hashed.remainingBatchSize, hashed.requiredBatchByteSize - hashed.consumedSize);
    assert.throws(() => sdk.content.countChunks(1, { redundancyLevel: 'extreme' }), RangeError);

    // Uploads read back through verified downloads.
    const bytes = await bee.uploadBytes(unique, { redundancyLevel: 'medium', batchId });
    assert.equal(bytes.reference, (await sdk.content.hashBytes(unique, { redundancyLevel: 'medium' })).reference);
    assert.deepEqual(await bee.downloadBytes(bytes.reference, { redundancyLevel: 'medium' }), unique);

    //the root is missing: it comes from its replicas
    const root = fake.chunks.get(bytes.reference);
    fake.chunks.delete(bytes.reference);
    assert.deepEqual(await bee.downloadBytes(bytes.reference, { redundancyLevel: 'medium' }), unique);
    fake.chunks.set(bytes.reference, root);

    //a node serving another chunk at an address is caught
    const [someAddress, someChunk] = [...fake.chunks].find(([address]) => address !== bytes.reference);
    const tampered = someChunk.slice();
    tampered[tampered.length - 1] ^= 1;
    fake.chunks.set(someAddress, tampered);
    await assert.rejects(bee.downloadChunk(someAddress));
    fake.chunks.set(someAddress, someChunk);
    assert.equal(sdk.content.chunkAddress(await bee.downloadChunk(someAddress)), someAddress);

    const encrypted = await bee.uploadBytes(hello, { encrypt: true, batchId });
    assert.match(encrypted.reference, /^[0-9a-f]{128}$/);
    assert.deepEqual(await bee.downloadBytes(encrypted.reference), hello);

    const file = await bee.uploadFile(hello, { contentType: 'text/plain', fileName: 'hello.txt', batchId, tag: 7 });
    assert.equal(file.reference, '973e91247d90079d5f537fe49e2deac142ab0d4b0f10d34bc8fd9a637498d31d');
    assert.ok(fake.requests.some(r => r.headers['swarm-tag'] === '7'));
    const downloaded = await bee.downloadFile(file.reference);
    assert.equal(downloaded.contentType, 'text/plain');
    assert.equal(downloaded.fileName, 'hello.txt');
    assert.deepEqual(downloaded.data, hello);

    const index = encoder.encode('<h1>hi</h1>');
    const bin = unique.subarray(0, 10000);
    const files = [{ path: 'index.html', data: index }, { path: 'img/a.bin', data: bin }];
    const site = await bee.uploadDirectory(files, { batchId });
    assert.equal(site.reference, (await sdk.content.hashDirectory(files)).reference);
    assert.deepEqual((await bee.downloadFile(`${site.reference}/img/a.bin`)).data, bin);
    const home = await bee.downloadFile(site.reference);
    assert.equal(home.contentType, 'text/html');
    assert.deepEqual(home.data, index);
    await assert.rejects(bee.resolve(`${site.reference}/missing`));

    //uploads stamped here, with compaction against the buckets of the batch
    const stamper = sdk.createStamper({ batchId, depth: 20, privateKey });
    try {
        fake.requests.length = 0;
        const compacted = await bee.uploadBytes(unique, { compactLevel: 8, stamper });
        assert.match(compacted.reference, /^[0-9a-f]{128}$/);
        assert.ok(fake.requests.every(r => /^[0-9a-f]{226}$/.test(r.headers['swarm-postage-stamp'])));
        assert.deepEqual(await bee.downloadBytes(compacted.reference), unique);
        assert.equal(stamper.requiredBatchDepth, compacted.requiredBatchDepth);
        await assert.rejects(bee.uploadBytes(hello, { batchId, stamper }), TypeError);
    } finally {
        stamper.dispose();
    }
    assert.throws(() => stamper.stamp(someAddress), /disposed/);

    // Feeds: published, signed here, and looked up from verified chunks.
    const topic = sdk.feeds.topic('smoke-feed');
    assert.equal(await bee.feeds.findLastSequenceUpdate(owner, topic), null);
    const first = await bee.feeds.updateSequence(topic, encoder.encode('first'), privateKey, { batchId });
    assert.equal(first.index, 0n);
    const second = await bee.feeds.updateSequence(topic, encoder.encode('second'), privateKey, { batchId });
    assert.equal(second.index, 1n);
    assert.equal(second.address, sdk.soc.address(sdk.feeds.sequenceIdentifier(topic, 1), owner));
    const last = await bee.feeds.findLastSequenceUpdate(owner, topic);
    assert.equal(last.index, 1n);
    assert.equal(last.payloadVersion, 'V2');
    assert.deepEqual(last.wrappedChunkPayload.subarray(8), encoder.encode('second'));
    assert.deepEqual(await bee.downloadFeedContent(last), encoder.encode('second'));

    //a feed wrapping the root of a larger content reads the content from the root
    const big = await bee.uploadBytes(unique, { batchId });
    const bigRoot = await bee.downloadChunk(big.reference);
    await bee.feeds.updateSequence(topic, bigRoot, privateKey, { batchId, dataIsChunk: true });
    const bigUpdate = await bee.feeds.findLastSequenceUpdate(owner, topic, { knownNearIndex: 1 });
    assert.equal(bigUpdate.index, 2n);
    assert.equal(bigUpdate.wrappedChunkAddress, big.reference);
    assert.deepEqual(await bee.downloadFeedContent(bigUpdate), unique);

    const epochTopic = sdk.feeds.topic('smoke-epoch');
    await bee.feeds.updateEpoch(epochTopic, encoder.encode('then'), privateKey, { batchId, timestamp: 1700000000 });
    const epochUpdate = await bee.feeds.findEpochUpdate(owner, epochTopic, 1700000100);
    assert.equal(epochUpdate.timestamp, 1700000000);
    assert.deepEqual(epochUpdate.wrappedChunkPayload.subarray(8), encoder.encode('then'));
    assert.equal(await bee.feeds.findEpochUpdate(owner, epochTopic, 1600000000), null);

    const manifest = await bee.feeds.uploadManifest(owner, topic, { batchId });
    assert.equal(manifest, await sdk.feeds.hashManifest(owner, topic));
    assert.deepEqual(await bee.feeds.decodeManifest(manifest), { owner, topic, type: 'Sequence' });
    assert.equal(await bee.feeds.decodeManifest(file.reference), null);

    // Postage.
    const chainState = await bee.chainState();
    assert.equal(chainState.currentPrice, 24000n);
    const amount = sdk.postage.amountForTtl(chainState.currentPrice, 86400);
    assert.equal(sdk.postage.ttlSeconds(amount, chainState.currentPrice), 86400);
    assert.equal(sdk.postage.price(amount, 20), amount * 2n ** 20n);
    assert.equal(sdk.postage.maxBucketCollisions(20), 16);
    assert.equal(sdk.postage.requiredDepth(16), 20);

    assert.deepEqual(await bee.postage.buy({ amount, depth: 20, label: 'smoke' }), { batchId, txHash: fakeTxHash });
    const batch = await bee.postage.get(batchId);
    assert.equal(batch.id, batchId);
    assert.equal(batch.depth, 20);
    assert.equal(batch.amount, 100000000n);
    assert.equal(batch.ttlSeconds, fakeBatch.batchTTL);
    assert.equal((await bee.postage.list()).length, 1);
    assert.equal((await bee.postage.global())[0].owner, '0x' + '22'.repeat(20));
    assert.equal((await bee.postage.buckets(batchId)).depth, 20);
    assert.equal(await bee.postage.topUp(batchId, 1000n), fakeTxHash);
    assert.equal(await bee.postage.dilute(batchId, 21), fakeTxHash);
    await bee.postage.setLabel(batchId, 'renamed');

    // Tags.
    const tag = await bee.tags.create();
    assert.equal(tag.id, fakeTag.uid);
    assert.equal((await bee.tags.get(7)).synced, fakeTag.synced);
    assert.equal((await bee.tags.list({ limit: 10 })).length, 1);
    await bee.tags.update(7, file.reference);
    await bee.tags.remove(7);

    // Pins and stewardship.
    assert.equal(await bee.pins.add(file.reference), true);
    assert.deepEqual(await bee.pins.list(), [file.reference]);
    assert.equal((await bee.pins.check(file.reference)).missing, 1);
    assert.equal((await bee.pins.checkAll()).length, 1);
    await bee.pins.remove(file.reference);
    assert.equal(await bee.isRetrievable(file.reference), true);
    await bee.reupload(file.reference, batchId);

    // Node.
    assert.equal((await bee.health()).version, '2.8.3');
    assert.equal(await bee.isReady(), true);
    assert.equal((await bee.info()).beeMode, 'Full');
    assert.equal((await bee.addresses()).overlay, '11'.repeat(32));
} finally {
    fake.close();
}

console.log('SwarmSdk.Wasm JavaScript API test passed');

//the runtime keeps a timer of the request timeouts pending after the requests end, and it would hold the process
process.exit(0);
