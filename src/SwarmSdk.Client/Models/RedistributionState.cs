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

namespace Etherna.SwarmSdk.Models
{
    public sealed class RedistributionState(
        bool isFrozen,
        bool isFullySynced,
        bool isHealthy,
        bool enabled,
        bool hasCommittedThisRound,
        bool hasRevealedThisRound,
        int round,
        int lastWonRound,
        int lastPlayedRound,
        int lastFrozenRound,
        int block,
        BzzValue reward,
        XDaiValue fees)
    {
        // Properties.
        public bool IsFrozen { get; } = isFrozen;
        public bool IsFullySynced { get; } = isFullySynced;
        public bool IsHealthy { get; } = isHealthy;

        /// <summary>Whether the node will commit in new redistribution rounds. Sampling still runs while disabled.</summary>
        public bool Enabled { get; } = enabled;

        /// <summary>Whether the node has already committed in the current round. Operators can shut down without freeze risk when this is false.</summary>
        public bool HasCommittedThisRound { get; } = hasCommittedThisRound;

        /// <summary>Whether the node has revealed in the current round. After a commit, the node must reveal before it is safe to shut down.</summary>
        public bool HasRevealedThisRound { get; } = hasRevealedThisRound;

        public int Round { get; } = round;
        public int LastWonRound { get; } = lastWonRound;
        public int LastPlayedRound { get; } = lastPlayedRound;
        public int LastFrozenRound { get; } = lastFrozenRound;
        public int Block { get; } = block;
        public BzzValue Reward { get; } = reward;
        public XDaiValue Fees { get; } = fees;
    }
}
