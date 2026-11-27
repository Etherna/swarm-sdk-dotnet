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

namespace Etherna.SwarmSdk.Wasm.Nodes
{
    /// <summary>
    /// What the exports share to talk to a bee node: the exports take the URL of the node on every call, and
    /// postage, values and enumerations as JavaScript passes them.
    /// </summary>
    internal static class BeeNode
    {
        // Methods.
        public static SwarmClient BuildClient(string beeUrl) => new(new Uri(beeUrl));

        /// <summary>
        /// Read a redundancy level from the number of <see cref="RedundancyLevel"/>, from 0 none to 4 paranoid.
        /// </summary>
        public static RedundancyLevel ReadRedundancyLevel(int redundancyLevel) =>
            Enum.IsDefined((RedundancyLevel)redundancyLevel)
                ? (RedundancyLevel)redundancyLevel
                : throw new ArgumentOutOfRangeException(nameof(redundancyLevel), "Unknown redundancy level");

        /// <summary>
        /// Read a redundancy strategy from the number of <see cref="RedundancyStrategy"/>: 0 none, 1 data,
        /// 2 prox, 3 race.
        /// </summary>
        public static RedundancyStrategy ReadRedundancyStrategy(int redundancyStrategy) =>
            Enum.IsDefined((RedundancyStrategy)redundancyStrategy)
                ? (RedundancyStrategy)redundancyStrategy
                : throw new ArgumentOutOfRangeException(nameof(redundancyStrategy), "Unknown redundancy strategy");

        public static XDaiValue? ReadXDai(string? weiValue) =>
            weiValue is null ? null : XDaiValue.FromWeiString(weiValue);
    }
}
