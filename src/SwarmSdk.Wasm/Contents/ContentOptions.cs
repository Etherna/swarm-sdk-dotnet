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

using Etherna.SwarmSdk.Models;
using System;

namespace Etherna.SwarmSdk.Wasm.Contents
{
    /// <summary>
    /// How a content is chunked, as the exports receive it from JavaScript.
    /// </summary>
    /// <param name="RedundancyLevel">The redundancy level of the chunks.</param>
    /// <param name="Encrypt">True to encrypt the chunks.</param>
    /// <param name="CompactLevel">The keys tried per chunk to fill the postage buckets evenly, 0 for none.</param>
    internal readonly record struct ContentOptions(
        RedundancyLevel RedundancyLevel,
        bool Encrypt,
        ushort CompactLevel)
    {
        // Static builders.
        /// <summary>
        /// Read the options: the redundancy level as the number of <see cref="Models.RedundancyLevel"/>, from 0
        /// none to 4 paranoid, and the compaction level in [0, 65535].
        /// </summary>
        public static ContentOptions Parse(int redundancyLevel, bool encrypt, int compactLevel)
        {
            if (!Enum.IsDefined((RedundancyLevel)redundancyLevel))
                throw new ArgumentOutOfRangeException(nameof(redundancyLevel), "Unknown redundancy level");
            if (compactLevel is < ushort.MinValue or > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(compactLevel), "Compaction level out of [0, 65535]");

            return new ContentOptions((RedundancyLevel)redundancyLevel, encrypt, (ushort)compactLevel);
        }
    }
}
