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

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Etherna.SwarmSdk.Exceptions
{
    /// <summary>
    /// A <see cref="SwarmClient"/> call that the node did not complete within the bound of its operation class
    /// (<see cref="SwarmClientTimeouts"/>).
    /// </summary>
    [SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
    public sealed class SwarmClientTimeoutException(
        string operation,
        TimeSpan timeout,
        Exception? innerException)
        : TimeoutException(
            string.Format(CultureInfo.InvariantCulture, "{0} did not complete within {1:0.###} s", operation, timeout.TotalSeconds),
            innerException)
    {
        // Properties.
        /// <summary>The call that timed out, as <c>SwarmClient.&lt;method&gt;</c>.</summary>
        public string Operation { get; } = operation;

        /// <summary>The bound the call did not meet.</summary>
        public TimeSpan Timeout { get; } = timeout;
    }
}
