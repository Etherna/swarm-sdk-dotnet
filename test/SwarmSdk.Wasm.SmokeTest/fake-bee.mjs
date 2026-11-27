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

// A bee node for the tests: it stores the chunks uploaded to it and serves them by address, records every
// request, and answers the other endpoints with fixed JSON in the shape of the bee OpenAPI. Chunk and SOC
// addresses are computed with the SDK under test, whose hashing the smoke test checks against bee's values.

import http from 'node:http';

export const fakeBatch = {
    batchID: 'ab'.repeat(32),
    utilization: 3,
    utilizationRatio: 0.75,
    usable: true,
    label: 'smoke',
    depth: 20,
    amount: '100000000',
    bucketDepth: 16,
    blockNumber: 42,
    immutableFlag: false,
    exists: true,
    batchTTL: 86400,
};

export const fakeTag = {
    uid: 7,
    address: '00'.repeat(32),
    startedAt: '2026-11-27T10:00:00Z',
    split: 10,
    seen: 1,
    stored: 9,
    sent: 8,
    synced: 7,
};

export const fakeTxHash = '0x' + 'cd'.repeat(32);

export async function startFakeBee(sdk) {
    const chunks = new Map();
    const requests = [];
    const pins = new Set();

    const json = (response, status, body) => {
        response.writeHead(status, { 'content-type': 'application/json' });
        response.end(JSON.stringify(body));
    };
    const notFound = response => json(response, 404, { code: 404, message: 'Not Found' });

    const route = (request, body, response) => {
        const url = new URL(request.url, 'http://localhost');
        const parts = url.pathname.split('/').filter(p => p);
        const [root, a, b, c] = parts;
        const method = request.method;

        // Chunks.
        if (method === 'POST' && root === 'chunks' && !a) {
            const address = sdk.content.chunkAddress(body);
            chunks.set(address, body);
            return json(response, 201, { reference: address });
        }
        if (method === 'POST' && root === 'soc') {
            const signature = Buffer.from(url.searchParams.get('sig'), 'hex');
            const payload = Buffer.concat([Buffer.from(b, 'hex'), signature, body]);
            const address = sdk.soc.address(b, a);
            chunks.set(address, new Uint8Array(payload));
            return json(response, 201, { reference: address });
        }
        if (method === 'GET' && root === 'chunks' && a) {
            const chunk = chunks.get(a);
            if (!chunk)
                return notFound(response);
            response.writeHead(200, { 'content-type': 'binary/octet-stream' });
            return response.end(chunk);
        }

        // Node.
        if (method === 'GET' && root === 'health')
            return json(response, 200, { status: 'ok', version: '2.8.3', apiVersion: '9.0.1' });
        if (method === 'GET' && root === 'readiness')
            return json(response, 200, { status: 'ready', version: '2.8.3', apiVersion: '9.0.1' });
        if (method === 'GET' && root === 'node')
            return json(response, 200, { beeMode: 'full', chequebookEnabled: true, swapEnabled: true });
        if (method === 'GET' && root === 'addresses')
            return json(response, 200, {
                overlay: '11'.repeat(32),
                underlay: ['/ip4/127.0.0.1/tcp/1634/p2p/16Uiu2HAm'],
                ethereum: '0x' + '22'.repeat(20),
                chain_address: '0x' + '22'.repeat(20),
                publicKey: '03' + '33'.repeat(32),
                pssPublicKey: '03' + '44'.repeat(32),
            });
        if (method === 'GET' && root === 'chainstate')
            return json(response, 200, {
                chainTip: 1000, block: 999, totalAmount: '5000000', currentPrice: '24000', minimumValidityBlocks: 17280,
            });

        // Postage.
        if (method === 'GET' && root === 'stamps' && !a)
            return json(response, 200, { stamps: [fakeBatch] });
        if (method === 'GET' && root === 'stamps' && a && b === 'buckets')
            return json(response, 200, {
                depth: 20, bucketDepth: 16, bucketUpperBound: 16,
                buckets: [{ bucketID: 0, collisions: 3 }, { bucketID: 1, collisions: 0 }],
            });
        if (method === 'GET' && root === 'stamps' && a)
            return json(response, 200, fakeBatch);
        if (method === 'PATCH' && root === 'stamps' && (a === 'topup' || a === 'dilute'))
            return json(response, 202, { batchID: b, txHash: fakeTxHash });
        if (method === 'PATCH' && root === 'stamps' && a)
            return json(response, 200, { message: 'OK', code: 200 });
        if (method === 'POST' && root === 'stamps')
            return json(response, 201, { batchID: fakeBatch.batchID, txHash: fakeTxHash });
        if (method === 'GET' && root === 'batches')
            return json(response, 200, {
                batches: [{
                    batchID: fakeBatch.batchID, value: '100000000', start: 42, owner: '0x' + '22'.repeat(20),
                    depth: 20, bucketDepth: 16, immutable: false, batchTTL: 86400,
                }],
            });

        // Tags.
        if (method === 'POST' && root === 'tags')
            return json(response, 201, fakeTag);
        if (method === 'GET' && root === 'tags' && a)
            return json(response, 200, fakeTag);
        if (method === 'GET' && root === 'tags')
            return json(response, 200, { tags: [fakeTag] });
        if (method === 'PATCH' && root === 'tags')
            return json(response, 200, { message: 'OK', code: 200 });
        if (method === 'DELETE' && root === 'tags') {
            response.writeHead(204);
            return response.end();
        }

        // Pins and stewardship.
        if (method === 'GET' && root === 'pins' && a === 'check') {
            const reference = url.searchParams.get('ref');
            const lines = (reference ? [reference] : [...pins])
                .map(r => JSON.stringify({ reference: r, total: 10, missing: 1, invalid: 0 }));
            response.writeHead(200, { 'content-type': 'application/x-ndjson' });
            return response.end(lines.join('\n') + '\n');
        }
        if (method === 'POST' && root === 'pins') {
            const created = !pins.has(a);
            pins.add(a);
            return json(response, created ? 201 : 200, { message: 'OK', code: created ? 201 : 200 });
        }
        if (method === 'DELETE' && root === 'pins') {
            pins.delete(a);
            return json(response, 200, { message: 'OK', code: 200 });
        }
        if (method === 'GET' && root === 'pins' && a)
            return pins.has(a) ? json(response, 200, { reference: a }) : notFound(response);
        if (method === 'GET' && root === 'pins')
            return json(response, 200, { references: [...pins] });
        if (method === 'GET' && root === 'stewardship')
            return json(response, 200, { isRetrievable: chunks.has(a) });
        if (method === 'PUT' && root === 'stewardship')
            return json(response, 200, { message: 'OK', code: 200 });

        return notFound(response);
    };

    const server = http.createServer((request, response) => {
        const parts = [];
        request.on('data', part => parts.push(part));
        request.on('end', () => {
            const body = new Uint8Array(Buffer.concat(parts));
            requests.push({ method: request.method, url: request.url, headers: request.headers, body });
            try {
                route(request, body, response);
            } catch (error) {
                json(response, 500, { code: 500, message: String(error) });
            }
        });
    });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));

    return {
        url: `http://127.0.0.1:${server.address().port}/`,
        chunks,
        requests,
        close() {
            //fetch keeps its connections alive: close them, or the process waits for their timeout
            server.close();
            server.closeAllConnections();
        },
    };
}
