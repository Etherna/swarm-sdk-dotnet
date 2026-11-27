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

// Runs every export of the published SwarmSdk.Wasm on Node, against the values a bee node computes.
// Usage: node smoke.mjs <publish directory>/wwwroot/_framework

import assert from 'node:assert/strict';
import http from 'node:http';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const frameworkDir = process.argv[2];
if (!frameworkDir) {
    console.error('Usage: node smoke.mjs <publish directory>/wwwroot/_framework');
    process.exit(2);
}

//the .NET runtime probes the streaming of request bodies with new Request(''), which Node rejects without a base URL
const NodeRequest = globalThis.Request;
globalThis.Request = class extends NodeRequest {
    constructor(input, init) {
        super(input === '' ? 'http://localhost/' : input, init);
    }
};

const { dotnet } = await import(pathToFileURL(path.resolve(frameworkDir, 'dotnet.js')).href);
const runtime = await dotnet.create();
const { ContentExports, SocExports, StamperExports, UploadExports } =
    (await runtime.getAssemblyExports('SwarmSdk.Wasm.dll')).Etherna.SwarmSdk.Wasm;

const encoder = new TextEncoder();
const hello = encoder.encode('Bzz over the world!');
const hex = bytes => Buffer.from(bytes).toString('hex');
//content exports return the JSON of the reference and of the postage it takes
const referenceOf = async json => JSON.parse(await json).reference;

// Contents.
assert.equal(
    await referenceOf(ContentExports.HashFileAsync(hello, 'text/plain', 'hello.txt', 0, false, 0, null, null, null)),
    '973e91247d90079d5f537fe49e2deac142ab0d4b0f10d34bc8fd9a637498d31d');

//three levels of chunks, with redundancy writing parities and root replicas; the data chunks repeat each other
const data = new Uint8Array(3 * 1024 * 1024 + 17).map((_, i) => (i * 31) & 255);
for (const [redundancyLevel, reference] of [
    [0, '8a0bb5d37242baeb262cac30da506c07008a50deb1e48f2cc02770eedfec736e'],
    [1, '45060bc4d35fc45b1f421f05b2415beefadbccbf31121129bd23b868ace99d2f'],
    [4, 'f81b3da2e416485dbf53b09d6ee277b76f8b6153f71ba06e666dda3592dc2a1b']])
{
    const chunks = [];
    const result = JSON.parse(await ContentExports.HashBytesAsync(data, redundancyLevel, false, 0, null, null,
        (address, payload, stamp) => chunks.push([address, payload, stamp])));
    assert.equal(result.reference, reference);
    assert.equal(result.missedOptimisticHashing, 0);
    assert.equal(chunks.length, ContentExports.CountContentChunks(data.length, redundancyLevel, false, 0));
    assert.ok(chunks.some(([address, payload]) => address === reference && payload instanceof Uint8Array));
    assert.ok(chunks.every(([, , stamp]) => stamp === null));
}

const encrypted = await referenceOf(ContentExports.HashBytesAsync(hello, 0, true, 0, null, null, null));
assert.match(encrypted, /^[0-9a-f]{128}$/);

//compaction derives the chunk keys from the content and the buckets: the same content gives the same reference
const compacted = await referenceOf(ContentExports.HashBytesAsync(data, 0, false, 16, null, null, null));
assert.match(compacted, /^[0-9a-f]{128}$/);
assert.equal(await referenceOf(ContentExports.HashBytesAsync(data, 0, false, 16, null, null, null)), compacted);
await assert.rejects(ContentExports.HashBytesAsync(hello, 0, false, 65536, null, null, null));

const indexContent = encoder.encode('<h1>hi</h1>');
const binContent = new Uint8Array(10000).fill(7);
const contents = new Uint8Array(indexContent.length + binContent.length);
contents.set(indexContent);
contents.set(binContent, indexContent.length);
assert.equal(
    await referenceOf(ContentExports.HashDirectoryAsync(
        ['index.html', 'img/a.bin'], contents, [indexContent.length, binContent.length], null, null, 0, false, 0,
        null, null, null)),
    '19e6d6b4161edb7dd266f3329e82a99fa15c975804b913fc0087a93ac0327e46');
await assert.rejects(ContentExports.HashDirectoryAsync(
    ['index.html'], contents, [contents.length + 1], null, null, 0, false, 0, null, null, null));
await assert.rejects(ContentExports.HashBytesAsync(hello, 5, false, 0, null, null, null));

// Keys, single owner chunks and feeds.
const privateKey = '0x' + '11'.repeat(32);
const owner = SocExports.GetAddressFromPrivateKey(privateKey);
assert.equal(owner, '0x19E7E376E7C213B7E7e7e46cc70A5dD086DAff2A');

const topic = SocExports.BuildFeedTopic('my-topic');
assert.equal(topic, '0ce8095306af56dbf740006772ab425b05b0dbebbbf585cd640c645459865ced');

const identifier = SocExports.BuildSequenceFeedIdentifier(topic, 3);
assert.equal(identifier, '26b77d3c697a423f3df622451246159c45ad4fdf339c70fd10df5fe67eaa56a7');
assert.equal(
    SocExports.BuildSocAddress(identifier, owner),
    '017613eb8b11ad97a368b70de2fbe3f112967653aacc7d20e6cf386a4b38386a');
assert.equal(
    SocExports.BuildEpochFeedIdentifier(topic, 1700000000, 10),
    '3765a40b11c5f5162391e6692111fb76414630f072d45558cd39cc41784832bc');

const soc = SocExports.BuildSignedSoc(identifier, hello, privateKey);
assert.equal(soc.length, 32 + 65 + 8 + hello.length);
assert.equal(Buffer.from(soc.subarray(0, 32)).toString('hex'), identifier);
assert.deepEqual(soc.subarray(32 + 65 + 8), hello);
assert.throws(() => SocExports.BuildSignedSoc(identifier, new Uint8Array(4097), privateKey));

// Postage stamps.
//a stamp is batch id, bucket and slot in it, timestamp and signature; the bucket is the first 2 bytes of the address
const batchId = 'ab'.repeat(32);
const checkStamp = (stamp, address, slot) => {
    assert.equal(stamp.length, 113);
    assert.equal(hex(stamp.subarray(0, 32)), batchId);
    const view = new DataView(stamp.buffer, stamp.byteOffset, stamp.length);
    assert.equal(view.getUint32(32), parseInt(address.slice(0, 4), 16));
    if (slot !== undefined)
        assert.equal(view.getUint32(36), slot);
    assert.ok(Math.abs(Number(view.getBigUint64(40) / 1000000n) - Date.now()) < 60000);
    assert.ok([27, 28].includes(stamp[112]));
};
const stamper = StamperExports.CreateStamper(batchId, 20, false, privateKey, null);
//the reference of a content of one chunk is the address of the chunk
const helloAddress = await referenceOf(ContentExports.HashBytesAsync(hello, 0, false, 0, null, null, null));
const helloBucket = helloAddress.slice(0, 4);
const firstStamp = StamperExports.StampChunk(stamper, helloAddress);
checkStamp(firstStamp, helloAddress);
const firstSlot = new DataView(firstStamp.buffer, firstStamp.byteOffset).getUint32(36);
//the slots of a bucket are numbered from 0, as bee does
assert.equal(firstSlot, 0);
checkStamp(StamperExports.StampChunk(stamper, helloAddress), helloAddress, firstSlot);
checkStamp(StamperExports.StampChunk(stamper, helloBucket + '00'.repeat(30)), helloBucket, firstSlot + 1);

const buckets = StamperExports.GetStamperBuckets(stamper);
assert.equal(buckets.length, 65536 * 4);
assert.equal(new DataView(buckets.buffer, buckets.byteOffset).getUint32(parseInt(helloBucket, 16) * 4, true), 2);
assert.equal(StamperExports.GetStamperRequiredBatchDepth(stamper), 17);

//a stamper resumed from the buckets goes on from the slots they hold
const resumed = StamperExports.CreateStamper(batchId, 20, false, privateKey, buckets);
checkStamp(StamperExports.StampChunk(resumed, helloBucket + 'ff'.repeat(30)), helloBucket, firstSlot + 2);
StamperExports.DisposeStamper(resumed);
assert.throws(() => StamperExports.StampChunk(resumed, helloAddress));
assert.throws(() => StamperExports.CreateStamper(batchId, 20, false, privateKey, new Uint8Array(4)));

//a write with a stamper hands every chunk to the callback with its stamp
const stampedChunks = [];
assert.equal(
    await referenceOf(ContentExports.HashBytesAsync(data, 1, false, 0, stamper, null,
        (address, payload, stamp) => stampedChunks.push([address, stamp]))),
    '45060bc4d35fc45b1f421f05b2415beefadbccbf31121129bd23b868ace99d2f');
assert.equal(stampedChunks.length, ContentExports.CountContentChunks(data.length, 1, false, 0));
for (const [address, stamp] of stampedChunks)
    checkStamp(stamp, address);

// Uploads, to a node that records the requests.
const requests = [];
const node = http.createServer((request, response) => {
    const body = [];
    request.on('data', part => body.push(part));
    request.on('end', () => {
        requests.push({ method: request.method, url: request.url, headers: request.headers, body: Buffer.concat(body) });
        response.writeHead(201, { 'content-type': 'application/json' });
        response.end(JSON.stringify({ reference: '00'.repeat(32) }));
    });
});
await new Promise(resolve => node.listen(0, '127.0.0.1', resolve));
const beeUrl = `http://127.0.0.1:${node.address().port}/`;

try {
    //the node stamps with its batch: every chunk goes to the chunk endpoint, every root replica to the SOC one
    assert.equal(
        await referenceOf(UploadExports.UploadBytesAsync(beeUrl, data, 1, false, 0, batchId, null, null, null)),
        '45060bc4d35fc45b1f421f05b2415beefadbccbf31121129bd23b868ace99d2f');
    assert.equal(requests.length, ContentExports.CountContentChunks(data.length, 1, false, 0));
    assert.ok(requests.every(r => r.method === 'POST' && r.headers['swarm-postage-batch-id'] === batchId));
    assert.equal(requests.filter(r => r.url.startsWith('/soc/')).length, 2);
    assert.equal(requests.filter(r => r.url === '/chunks').length, requests.length - 2);

    //the chunks come stamped
    requests.length = 0;
    assert.equal(
        await referenceOf(UploadExports.UploadFileAsync(
            beeUrl, hello, 'text/plain', 'hello.txt', 0, false, 0, null, stamper, null, null)),
        '973e91247d90079d5f537fe49e2deac142ab0d4b0f10d34bc8fd9a637498d31d');
    assert.ok(requests.length > 0);
    for (const r of requests) {
        assert.equal(r.headers['swarm-postage-batch-id'], undefined);
        assert.match(r.headers['swarm-postage-stamp'], /^[0-9a-f]{226}$/);
    }

    requests.length = 0;
    assert.equal(
        await referenceOf(UploadExports.UploadDirectoryAsync(beeUrl, ['index.html', 'img/a.bin'], contents,
            [indexContent.length, binContent.length], null, null, 0, false, 0, batchId, null, null, null)),
        '19e6d6b4161edb7dd266f3329e82a99fa15c975804b913fc0087a93ac0327e46');
    assert.ok(requests.length > 0);

    requests.length = 0;
    const helloChunk = new Uint8Array(8 + hello.length);
    new DataView(helloChunk.buffer).setBigUint64(0, BigInt(hello.length), true);
    helloChunk.set(hello, 8);
    assert.equal(ContentExports.GetChunkAddress(helloChunk), helloAddress);
    assert.equal(await UploadExports.UploadChunkAsync(beeUrl, helloChunk, null, stamper, null), helloAddress);
    assert.equal(requests[0].url, '/chunks');
    assert.deepEqual(new Uint8Array(requests[0].body), helloChunk);
    checkStamp(Buffer.from(requests[0].headers['swarm-postage-stamp'], 'hex'), helloAddress);

    //a feed update is a signed single owner chunk, uploaded at its owner and identifier
    requests.length = 0;
    assert.equal(
        await UploadExports.UploadSocAsync(beeUrl, soc, batchId, null, null, false, null),
        '017613eb8b11ad97a368b70de2fbe3f112967653aacc7d20e6cf386a4b38386a');
    const socUrl = new URL(requests[0].url, beeUrl);
    assert.equal(socUrl.pathname.toLowerCase(), `/soc/${owner.slice(2).toLowerCase()}/${identifier}`);
    assert.equal(socUrl.searchParams.get('sig'), hex(soc.subarray(32, 32 + 65)));
    assert.deepEqual(new Uint8Array(requests[0].body), soc.subarray(32 + 65));

    //exactly one postage
    await assert.rejects(UploadExports.UploadBytesAsync(beeUrl, hello, 0, false, 0, null, null, null, null));
    await assert.rejects(UploadExports.UploadBytesAsync(beeUrl, hello, 0, false, 0, batchId, stamper, null, null));
} finally {
    //fetch keeps its connections alive: close them, or the process waits for their timeout
    node.close();
    node.closeAllConnections();
    StamperExports.DisposeStamper(stamper);
}

console.log('SwarmSdk.Wasm smoke test passed');

//the runtime keeps a timer of the request timeouts pending after the requests end, and it would hold the process
process.exit(0);
