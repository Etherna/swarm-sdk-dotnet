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

// A sample page of the JavaScript API: it hashes a file, as a bee node does on its bzz endpoint.

import { loadSwarmSdk } from './swarm-sdk.js';

const sdk = await loadSwarmSdk();
const { reference } = await sdk.content.hashFile(
    new TextEncoder().encode('Bzz over the world!'),
    { contentType: 'text/plain', fileName: 'hello.txt' });

console.log(reference);
document.getElementById('out').textContent = reference;
