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

using Etherna.SwarmSdk.Services;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using Hasher = Etherna.SwarmSdk.Hashing.Hasher;

#pragma warning disable CA1303
Console.WriteLine("Main, don't remove!");
#pragma warning restore CA1303

[SuppressMessage("Design", "CA1052:Static holder types should be Static or NotInheritable")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("Design", "CA1050:Declare types in namespaces")]
#pragma warning disable CA1515
public partial class SwarmSdkWasmUtil
{
    [JSExport]
    internal static async Task<string> GetHashStringAsync(
        byte[] data,
        string fileContentType,
        string fileName)
    {
        var calculatorService = new ChunkService();
        var result = await calculatorService.UploadSingleFileAsync(
            data,
            fileContentType,
            fileName,
            new Hasher()).ConfigureAwait(false);
        return result.Reference.Hash.ToString();
    }
}
#pragma warning restore CA1515
