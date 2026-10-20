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

using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Manifest;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Services;
using Etherna.SwarmSdk.Stores;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Chunks
{
    public class ChunkTraverserTest
    {
        // Internal classes.
        public record TraverseFromDataChunkTestElement(
            IChunkStore ChunkStore,
            SwarmReference RootReference,
            IEnumerable<SwarmReference> ExpectedFoundReferences,
            IEnumerable<SwarmReference> ExpectedNotFoundReferences);

        public record TraverseFromMantarayManifestRootTestElement(
            IChunkStore ChunkStore,
            SwarmReference RootReference,
            IEnumerable<SwarmReference> ExpectedFoundReferences,
            IEnumerable<SwarmReference> ExpectedNotFoundReferences);

        public record TraverseFromMantarayNodeChunkTestElement(
            IChunkStore ChunkStore,
            SwarmReference RootReference,
            IEnumerable<SwarmReference> ExpectedFoundReferences,
            IEnumerable<SwarmReference> ExpectedNotFoundReferences);

        public record TraverseTestElement(
            IChunkStore ChunkStore,
            SwarmReference RootReference,
            IEnumerable<SwarmReference> ExpectedFoundReferences,
            IEnumerable<SwarmReference> ExpectedNotFoundReferences);
        
        // Data.
        public static IEnumerable<object[]> TraverseFromDataChunkTests
        {
            get
            {
                var tests = new List<TraverseFromDataChunkTestElement>();
                
                //find all chunks (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    tests.Add(new TraverseFromDataChunkTestElement(
                        chunkStore,
                        "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                        [
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c"
                        ],
                        []));
                }
                
                //missing traversing root (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove root
                    var deleteTask = chunkStore.RemoveAsync("3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromDataChunkTestElement(
                        chunkStore,
                        "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                        [],
                        ["3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466"]));
                }
                
                //missing leaf data (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileB" leaf data
                    var deleteTask = chunkStore.RemoveAsync("4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromDataChunkTestElement(
                        chunkStore,
                        "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                        [
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c"
                        ],
                        ["4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb"]));
                }
                
                //find all chunks (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    tests.Add(new TraverseFromDataChunkTestElement(
                        chunkStore,
                        "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                        [
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"
                        ],
                        []));
                }
                
                //missing traversing root (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove root
                    var deleteTask = chunkStore.RemoveAsync("88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromDataChunkTestElement(
                        chunkStore,
                        "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                        [
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"
                        ],
                        []));
                }
                
                //missing leaf data (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileB" leaf data
                    var deleteTask = chunkStore.RemoveAsync("4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromDataChunkTestElement(
                        chunkStore,
                        "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                        [
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"
                        ],
                        []));
                }
                
                //missing leaf data and parities (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileB" leaf data
                    var deleteTask = Task.WhenAll(
                        chunkStore.RemoveAsync("4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb"),
                        chunkStore.RemoveAsync("3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849"),
                        chunkStore.RemoveAsync("d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85"),
                        chunkStore.RemoveAsync("e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"));
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromDataChunkTestElement(
                        chunkStore,
                        "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                        [
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c"
                        ],
                        [
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"
                        ]));
                }

                //single owner chunk root, wrapping a stored data chunk that isn't entered (no redundancy)
                {
                    var chunkStoreInitTask = BuildSocChunkStoreAsync();
                    chunkStoreInitTask.Wait();
                    var (chunkStore, socReference) = chunkStoreInitTask.Result;
                    
                    tests.Add(new TraverseFromDataChunkTestElement(
                        chunkStore,
                        socReference,
                        [socReference],
                        []));
                }

                return tests.Select(t => new object[] { t });
            }
        }
        
        public static IEnumerable<object[]> TraverseFromMantarayManifestRootTests
        {
            get
            {
                var tests = new List<TraverseFromMantarayManifestRootTestElement>();
                
                //find all chunks (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                        [
                            "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                            "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                            "b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1",
                            "72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1",
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "1396c4610b0a3417e54bdb8ef646c2e089a9949bd82814c27b0420c8148c3d43",
                            "28634e6ed6ff1f72936052f2ef6b806606d00ec04a97c97b84d13200fd6ddd00",
                            "b3a110c515642b16ecf50c024a80b42a7dacc9b99be8880334ac01160686c27c",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3"
                        ],
                        []));
                }
                
                //missing manifest root (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove root
                    var deleteTask = chunkStore.RemoveAsync("46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc");
                    deleteTask.Wait();
                
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                        [],
                        ["46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc"]));
                }
                
                //missing manifest fork node (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "A" fork node
                    var deleteTask = chunkStore.RemoveAsync("b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1");
                    deleteTask.Wait();
                
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                        [
                            "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                            "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                            "fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1",
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "1396c4610b0a3417e54bdb8ef646c2e089a9949bd82814c27b0420c8148c3d43",
                            "28634e6ed6ff1f72936052f2ef6b806606d00ec04a97c97b84d13200fd6ddd00",
                            "b3a110c515642b16ecf50c024a80b42a7dacc9b99be8880334ac01160686c27c",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3"
                        ],
                        [ "b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1" ]));
                }
                
                //missing data intermediate (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileA" intermediate data
                    var deleteTask = chunkStore.RemoveAsync("72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e");
                    deleteTask.Wait();
                
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                        [
                            "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                            "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                            "b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1",
                            "fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1",
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "1396c4610b0a3417e54bdb8ef646c2e089a9949bd82814c27b0420c8148c3d43",
                            "28634e6ed6ff1f72936052f2ef6b806606d00ec04a97c97b84d13200fd6ddd00",
                            "b3a110c515642b16ecf50c024a80b42a7dacc9b99be8880334ac01160686c27c",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3"
                        ],
                        ["72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e"]));
                }
                
                //missing data leaf (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileA" leaf data
                    var deleteTask = chunkStore.RemoveAsync("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                        [
                            "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                            "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                            "b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1",
                            "72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1",
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "1396c4610b0a3417e54bdb8ef646c2e089a9949bd82814c27b0420c8148c3d43",
                            "28634e6ed6ff1f72936052f2ef6b806606d00ec04a97c97b84d13200fd6ddd00",
                            "b3a110c515642b16ecf50c024a80b42a7dacc9b99be8880334ac01160686c27c",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3"
                        ],
                        ["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]));
                }
                
                //find all chunks (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                        [
                            "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96",
                            "205dc00928f90e89a8c3f364d1fd69c90430839e3f9d956f5b86cc89dc2e4fce",
                            "e6aaf074e67db4c47e57f2f327a36dace092f02f043a1abb1132d1a949289e1c",
                            "53e72e4edb645c7c8ba801cf60725a99eb84a53ed751c7e0adb4ef5609a39aa2",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3",
                            "956c91e0ead14d164523430e202b12cfc114c0f54e4dfc3f547d1fb9ab96e26e",
                            "edb064acefbcd3a5bfade1882382bd600682635f5f731ba96924ec36162e5d7c",
                            "9fb3251a21778c9efc64e9243e1e08e65924dd2afe5547c90c4ae2775159a95d"
                        ],
                        []));
                }
                
                //missing manifest root (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove root
                    var deleteTask = chunkStore.RemoveAsync("910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64");
                    deleteTask.Wait();
                
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                        [
                            "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96",
                            "205dc00928f90e89a8c3f364d1fd69c90430839e3f9d956f5b86cc89dc2e4fce",
                            "e6aaf074e67db4c47e57f2f327a36dace092f02f043a1abb1132d1a949289e1c",
                            "53e72e4edb645c7c8ba801cf60725a99eb84a53ed751c7e0adb4ef5609a39aa2",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3",
                            "956c91e0ead14d164523430e202b12cfc114c0f54e4dfc3f547d1fb9ab96e26e",
                            "edb064acefbcd3a5bfade1882382bd600682635f5f731ba96924ec36162e5d7c",
                            "9fb3251a21778c9efc64e9243e1e08e65924dd2afe5547c90c4ae2775159a95d"
                        ],
                        []));
                }
                
                //missing manifest fork node (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "A" fork node
                    var deleteTask = chunkStore.RemoveAsync("2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f");
                    deleteTask.Wait();
                
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                        [
                            "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96",
                            "205dc00928f90e89a8c3f364d1fd69c90430839e3f9d956f5b86cc89dc2e4fce",
                            "e6aaf074e67db4c47e57f2f327a36dace092f02f043a1abb1132d1a949289e1c",
                            "53e72e4edb645c7c8ba801cf60725a99eb84a53ed751c7e0adb4ef5609a39aa2",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3",
                            "956c91e0ead14d164523430e202b12cfc114c0f54e4dfc3f547d1fb9ab96e26e",
                            "edb064acefbcd3a5bfade1882382bd600682635f5f731ba96924ec36162e5d7c",
                            "9fb3251a21778c9efc64e9243e1e08e65924dd2afe5547c90c4ae2775159a95d"
                        ],
                        []));
                }
                
                //missing data intermediate (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileA" intermediate data
                    var deleteTask = chunkStore.RemoveAsync("9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c");
                    deleteTask.Wait();
                
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                        [
                            "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96",
                            "205dc00928f90e89a8c3f364d1fd69c90430839e3f9d956f5b86cc89dc2e4fce",
                            "e6aaf074e67db4c47e57f2f327a36dace092f02f043a1abb1132d1a949289e1c",
                            "53e72e4edb645c7c8ba801cf60725a99eb84a53ed751c7e0adb4ef5609a39aa2",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3",
                            "956c91e0ead14d164523430e202b12cfc114c0f54e4dfc3f547d1fb9ab96e26e",
                            "edb064acefbcd3a5bfade1882382bd600682635f5f731ba96924ec36162e5d7c",
                            "9fb3251a21778c9efc64e9243e1e08e65924dd2afe5547c90c4ae2775159a95d"
                        ],
                        []));
                }
                
                //missing data leaf (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileA" leaf data
                    var deleteTask = chunkStore.RemoveAsync("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayManifestRootTestElement(
                        chunkStore,
                        "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                        [
                            "910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64",
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96",
                            "205dc00928f90e89a8c3f364d1fd69c90430839e3f9d956f5b86cc89dc2e4fce",
                            "e6aaf074e67db4c47e57f2f327a36dace092f02f043a1abb1132d1a949289e1c",
                            "53e72e4edb645c7c8ba801cf60725a99eb84a53ed751c7e0adb4ef5609a39aa2",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3",
                            "956c91e0ead14d164523430e202b12cfc114c0f54e4dfc3f547d1fb9ab96e26e",
                            "edb064acefbcd3a5bfade1882382bd600682635f5f731ba96924ec36162e5d7c",
                            "9fb3251a21778c9efc64e9243e1e08e65924dd2afe5547c90c4ae2775159a95d"
                        ],
                        []));
                }

                return tests.Select(t => new object[] { t });
            }
        }
        
        public static IEnumerable<object[]> TraverseFromMantarayNodeChunkTests
        {
            get
            {
                var tests = new List<TraverseFromMantarayNodeChunkTestElement>();
                
                //find all chunks (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                        [
                            "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                            "b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1",
                            "72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1",
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c"
                        ],
                        []));
                }
                
                //missing traversing root (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove root
                    var deleteTask = chunkStore.RemoveAsync("df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                        [],
                        ["df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db"]));
                }
                
                //missing manifest internal node (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "A" fork node
                    var deleteTask = chunkStore.RemoveAsync("b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                        [
                            "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                            "fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1",
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c"
                        ],
                        ["b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1"]));
                }
                
                //missing data intermediate (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileA" intermediate data
                    var deleteTask = chunkStore.RemoveAsync("72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                        [
                            "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                            "b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1",
                            "fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1",
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c"
                        ],
                        ["72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e"]));
                }
                
                //missing data leaf (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileA" leaf data
                    var deleteTask = chunkStore.RemoveAsync("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                        [
                            "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                            "b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1",
                            "72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1",
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c"
                        ],
                        ["45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf"]));
                }
                
                //find all chunks (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                        [
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"
                        ],
                        []));
                }
                
                //missing traversing root (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove root
                    var deleteTask = chunkStore.RemoveAsync("bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                        [
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"
                        ],
                        []));
                }
                
                //missing manifest internal node (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "A" fork node
                    var deleteTask = chunkStore.RemoveAsync("2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                        [
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"
                        ],
                        []));
                }
                
                //missing data intermediate (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileA" intermediate data
                    var deleteTask = chunkStore.RemoveAsync("9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                        [
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"
                        ],
                        []));
                }
                
                //missing data leaf (Medium redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.Medium);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //remove "fileA" leaf data
                    var deleteTask = chunkStore.RemoveAsync("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf");
                    deleteTask.Wait();
                    
                    tests.Add(new TraverseFromMantarayNodeChunkTestElement(
                        chunkStore,
                        "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                        [
                            "bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8",
                            "2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f",
                            "9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca",
                            "5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4",
                            "d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470",
                            "7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4",
                            "88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849",
                            "d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85",
                            "e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96"
                        ],
                        []));
                }

                return tests.Select(t => new object[] { t });
            }
        }
        
        public static IEnumerable<object[]> TraverseTests
        {
            get
            {
                var tests = new List<TraverseTestElement>();
                
                //manifest root (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    tests.Add(new TraverseTestElement(
                        chunkStore,
                        "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                        [
                            "46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc",
                            "df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db",
                            "b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1",
                            "72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e",
                            "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac",
                            "45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf",
                            "fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1",
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c",
                            "1396c4610b0a3417e54bdb8ef646c2e089a9949bd82814c27b0420c8148c3d43",
                            "28634e6ed6ff1f72936052f2ef6b806606d00ec04a97c97b84d13200fd6ddd00",
                            "b3a110c515642b16ecf50c024a80b42a7dacc9b99be8880334ac01160686c27c",
                            "4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185",
                            "670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20",
                            "caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420",
                            "b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3"
                        ],
                        []));
                }
                
                //multi-chunk data root (no redundancy)
                {
                    var chunkStoreInitTask = BuildChunkStoreAsync(RedundancyLevel.None);
                    chunkStoreInitTask.Wait();
                    var chunkStore = chunkStoreInitTask.Result;
                    
                    //"fileB" data intermediate, not a manifest
                    tests.Add(new TraverseTestElement(
                        chunkStore,
                        "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                        [
                            "3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466",
                            "185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651",
                            "4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb",
                            "405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c"
                        ],
                        []));
                }
                
                //single-chunk data root, shorter than a manifest node header (no redundancy)
                {
                    var chunkStore = new MemoryChunkStore();
                    var data = new byte[EncryptionKey256.KeySize + MantarayNodeBase.VersionHashSize - 1];
                    new Random(0).NextBytes(data);
                    
                    var writeTask = new ChunkService().WriteDataChunksAsync(chunkStore, data);
                    writeTask.Wait();
                    var rootReference = writeTask.Result;
                    
                    tests.Add(new TraverseTestElement(
                        chunkStore,
                        rootReference,
                        [rootReference],
                        []));
                }

                //single owner chunk root, wrapping a stored data chunk that isn't entered (no redundancy)
                {
                    var chunkStoreInitTask = BuildSocChunkStoreAsync();
                    chunkStoreInitTask.Wait();
                    var (chunkStore, socReference) = chunkStoreInitTask.Result;
                    
                    tests.Add(new TraverseTestElement(
                        chunkStore,
                        socReference,
                        [socReference],
                        []));
                }

                return tests.Select(t => new object[] { t });
            }
        }
        
        // Tests.
        [Theory, MemberData(nameof(TraverseTests))]
        public async Task Traverse(TraverseTestElement test)
        {
            var chunkTraverser = new ChunkTraverser(test.ChunkStore);
            List<SwarmReference> foundChunkReferences = [];
            List<SwarmReference> invalidFoundChunkReferences = [];
            List<SwarmReference> notFoundChunkReferences = [];
        
            await chunkTraverser.TraverseAsync(
                test.RootReference,
                (_, r) => { foundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                (_, r) => { invalidFoundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                r => { notFoundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                RedundancyLevel.Paranoid,
                RedundancyStrategy.Data,
                true,
                true);
        
            Assert.Equal(test.ExpectedFoundReferences, foundChunkReferences);
            Assert.Equal(test.ExpectedNotFoundReferences, notFoundChunkReferences);
            Assert.Empty(invalidFoundChunkReferences);
        }
        
        [Theory, MemberData(nameof(TraverseFromDataChunkTests))]
        public async Task TraverseFromDataChunk(TraverseFromDataChunkTestElement test)
        {
            var chunkTraverser = new ChunkTraverser(test.ChunkStore);
            List<SwarmReference> foundChunkReferences = [];
            List<SwarmReference> notFoundChunkReferences = [];
        
            await chunkTraverser.TraverseFromDataChunkAsync(
                test.RootReference,
                (_, r) => { foundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                r => { notFoundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                RedundancyLevel.Paranoid,
                RedundancyStrategy.Data,
                true,
                true);
        
            Assert.Equal(test.ExpectedFoundReferences, foundChunkReferences);
            Assert.Equal(test.ExpectedNotFoundReferences, notFoundChunkReferences);
        }
        
        [Theory, MemberData(nameof(TraverseFromMantarayManifestRootTests))]
        public async Task TraverseFromMantarayManifestRoot(TraverseFromMantarayManifestRootTestElement test)
        {
            var chunkTraverser = new ChunkTraverser(test.ChunkStore);
            List<SwarmReference> foundChunkReferences = [];
            List<SwarmReference> invalidFoundChunkReferences = [];
            List<SwarmReference> notFoundChunkReferences = [];

            await chunkTraverser.TraverseFromMantarayManifestRootAsync(
                test.RootReference,
                (_, r) => { foundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                (_, r) => { invalidFoundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                r => { notFoundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                RedundancyLevel.Paranoid,
                RedundancyStrategy.Data,
                true,
                true);

            Assert.Equal(test.ExpectedFoundReferences, foundChunkReferences);
            Assert.Equal(test.ExpectedNotFoundReferences, notFoundChunkReferences);
            Assert.Empty(invalidFoundChunkReferences);
        }
        
        [Theory, MemberData(nameof(TraverseFromMantarayNodeChunkTests))]
        public async Task TraverseFromMantarayNodeChunk(TraverseFromMantarayNodeChunkTestElement test)
        {
            var chunkTraverser = new ChunkTraverser(test.ChunkStore);
            List<SwarmReference> foundChunkReferences = [];
            List<SwarmReference> invalidFoundChunkReferences = [];
            List<SwarmReference> notFoundChunkReferences = [];
        
            await chunkTraverser.TraverseFromMantarayNodeChunkAsync(
                test.RootReference,
                NodeType.Edge,
                (_, r) => { foundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                (_, r) => { invalidFoundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                r => { notFoundChunkReferences.Add(r.Reference); return Task.CompletedTask; },
                RedundancyLevel.Paranoid,
                RedundancyStrategy.Data,
                true,
                true);
        
            Assert.Equal(test.ExpectedFoundReferences, foundChunkReferences);
            Assert.Equal(test.ExpectedNotFoundReferences, notFoundChunkReferences);
            Assert.Empty(invalidFoundChunkReferences);
        }
        
        // Helpers.
        private static async Task<IChunkStore> BuildChunkStoreAsync(
            RedundancyLevel redundancyLevel)
        {
            var chunkStore = new MemoryChunkStore();
            var postageStamper = new FakePostageStamper();
            
            /* Build dir manifest structure as:
             * /
             * |-- fileA (8kB)
             * |-- fileB (12kB)
             * |-- myDir
             *     |-- fileA (8kB)
             *     |-- fileC (16kB)
             *
             * Chunks without redundancy:
             * 46be4f99d106518c74fe02262aa4d738117eee47f9bb6bceea523346762372cc (root)
             * |-- df08922becf846a73648dd793d2b4e8c7d8e14d812ae953b169c3948cc7208db ("file" fork)
             *     |-- b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1 ("A" fork)
             *         |-- 72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e (data intermediate)
             *             |-- ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac (data leaf)
             *             |-- 45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf (data leaf)
             *     |-- fd85afac03d0fce2ecd169c0f40d2f4afddd265f03a5f9938d1a7fedd6e670c1 ("B" fork)
             *         |-- 3abeb3b5c9690c1d453c20a1d492664a1f5336c2cb359cce04a37df996f45466 (data intermediate)
             *             |-- 185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651 (data leaf)
             *             |-- 4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb (data leaf)
             *             |-- 405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c (data leaf)
             * |-- 1396c4610b0a3417e54bdb8ef646c2e089a9949bd82814c27b0420c8148c3d43 ("myDir/file" fork)
             *     |-- b4dbbcabd06b975c982b1165526ca86d645957bdb4ad185c7762349bc3d05db1 ("A" fork)
             *         |-- 72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e (data intermediate)
             *             |-- ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac (data leaf)
             *             |-- 45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf (data leaf)
             *     |-- 28634e6ed6ff1f72936052f2ef6b806606d00ec04a97c97b84d13200fd6ddd00 ("C" fork)
             *         |-- b3a110c515642b16ecf50c024a80b42a7dacc9b99be8880334ac01160686c27c (data intermediate)
             *             |-- 4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185 (data leaf)
             *             |-- 670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20 (data leaf)
             *             |-- caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420 (data leaf)
             *             |-- b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3 (data leaf)
             *
             * Chunks with Medium redundancy:
             * 910c2034a133ce27c2f84b77b28026ed4d0b848c713cb4f0485eae9e2f891b64 (root)
             * |-- bd345335c1eaaf9281b6a9c5f114795d4423f727fa7a06fe52c6dbce2d67c6d8 ("file" fork)
             *     |-- 2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f ("A" fork)
             *         |-- 9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c (data intermediate)
             *             |-- ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac (data leaf)
             *             |-- 45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf (data leaf)
             *             |-- d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca (parity)
             *             |-- 5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4 (parity)
             *             |-- d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470 (parity)
             *         |-- 6a761b787d46e16083cb2cbe7e6e3eaffabf481b02efe9299771eab315c5b48a (soc root replica)
             *         |-- 8aa01edc40a0f5b085eab79c658a66f57502276e0c3e1d0658f6c04afb25f97d (soc root replica)
             *     |-- 280f32251893d054c48e7a883ab26b02f41ec5f85d5c53971e38504579891462 (soc root replica)
             *     |-- f3833297c2cf487ec19b3b07179e95eb4404f8b44443dc57fff4e3c7b2f44f21 (soc root replica)
             *     |-- 7c5de76a3f1b7904871d7dad114f6bc752bd9c51aa600859303725a5aa5c7ab4 ("B" fork)
             *         |-- 88bdfd38ff0ab6d55068565d949cceebdd71e774fa71f6e86c4ddd3d34fe79f3 (data intermediate)
             *             |-- 185ca0c92f3a1760c64206da9ce63f66c81094b467713701c5150c8115eb4651 (data leaf)
             *             |-- 4a36ba2c1185612926e7f73e1eca905839263b3c4c0f21e6c6c8e731110ecdcb (data leaf)
             *             |-- 405b276d2bdd8257a4864947a5d2bf8b3908bba72250a0d92a9127eb417f106c (data leaf)
             *             |-- 3fc39bea2da534472cfa98cac2da77e89c0d73f5f330f600359759599e5ef849 (parity)
             *             |-- d30f46b1425d3d5f73ebb0db968082ee830408d6707b2db8fec11287ebfafe85 (parity)
             *             |-- e3d95dc11f5e76bfa6d04c8dc1964bac2035f0cb6899bed9ea1abda220da4a96 (parity)
             *         |-- 56546c8a6f15184578680c753a35d940e5d7ed2d01e82cbee5dc7a2aad509424 (soc root replica)
             *         |-- ddb8c57d9ff08a6f8ac41049105364a7764831304f8988af12a519cec578381a (soc root replica)
             *     |-- 2ddf84e795af629b0d77d97fc75275ab2684d5721ab7c52bf68885d1a4e7a2f8 (soc root replica)
             *     |-- 92f1d21e80ea4ea159d84d917cab3c5aa32950d9a6154d78b8cb5f5685f87a34 (soc root replica)
             * |-- 77cd477e4e51da3cf9331c875931990384cc5a2a601dfc73021d90b089a6b199 (soc root replica)
             * |-- df75e10186a52ab935b01bc57b2616272035394fcae67008f4165b98cde000d4 (soc root replica)
             * |-- 205dc00928f90e89a8c3f364d1fd69c90430839e3f9d956f5b86cc89dc2e4fce ("myDir/file" fork)
             *     |-- 2a1b9564d2d4f190b895d951d3bb422a091578eb26e4f872bae326cff126e53f ("A" fork)
             *         |-- 9775f55d03f9237402e4b2359c5230f3eb9fd473436d032824e893a294ea888c (data intermediate)
             *             |-- ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac (data leaf)
             *             |-- 45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf (data leaf)
             *             |-- d0e0454d8708dfb36791f5633709ee6ebd34c5c5a8e8f9c292a2131846b903ca (parity)
             *             |-- 5c2c287f169e2179acbb664e701d57df8da3e06c128b06ec5155e2520b30b7d4 (parity)
             *             |-- d12b77a0803942032dd87b8635b6f969c4ddff6725406aa38396697e4c319470 (parity)
             *         |-- 6a761b787d46e16083cb2cbe7e6e3eaffabf481b02efe9299771eab315c5b48a (soc root replica)
             *         |-- 8aa01edc40a0f5b085eab79c658a66f57502276e0c3e1d0658f6c04afb25f97d (soc root replica)
             *     |-- 280f32251893d054c48e7a883ab26b02f41ec5f85d5c53971e38504579891462 (soc root replica)
             *     |-- f3833297c2cf487ec19b3b07179e95eb4404f8b44443dc57fff4e3c7b2f44f21 (soc root replica)
             *     |-- e6aaf074e67db4c47e57f2f327a36dace092f02f043a1abb1132d1a949289e1c ("C" fork)
             *         |-- 53e72e4edb645c7c8ba801cf60725a99eb84a53ed751c7e0adb4ef5609a39aa2 (data intermediate)
             *             |-- 4890e9a8a9656096fdfc57ba7423778efc44c374f0a5812a7ef7cad395fe7185 (data leaf)
             *             |-- 670371ff59c155195ac366e8f398b226fc97ee092edf1b57e0eea8fe7beafd20 (data leaf)
             *             |-- caf0f6a6ba3b3cd0c242f046e6c41c59ac3e31ef7a2c4e74427b6c8a6929c420 (data leaf)
             *             |-- b3881d42550e27238c5bb2f2ea63aa96a3a287a9f4cabd065df5f7bc432effb3 (data leaf)
             *             |-- 956c91e0ead14d164523430e202b12cfc114c0f54e4dfc3f547d1fb9ab96e26e (parity)
             *             |-- edb064acefbcd3a5bfade1882382bd600682635f5f731ba96924ec36162e5d7c (parity)
             *             |-- 9fb3251a21778c9efc64e9243e1e08e65924dd2afe5547c90c4ae2775159a95d (parity)
             *         |-- 057a1c4a4156e8556fa0262156342e44a685c805adf15adbbe841649c0fc92d9 (soc root replica)
             *         |-- ccbde3fd2fb2d485fd6c09674d438a2f48db752680df208d79c837bfe2fc510e (soc root replica)
             *     |-- ad6542cf666fb157f2741b90d5ee65292dcd3d26478ce18a2736c722aae78f53 (soc root replica)
             *     |-- 086416767669cd2807f7de883edadb202b0c3ed682c32a6ff2ee81d29c70b3fb (soc root replica)
             * |-- c0c935b81ce3927474d0193397b4b0957f01f336d01c2be44fb0fd223620a982 (soc root replica)
             * |-- 649434a5da772405968625b0c22cef5ece59a66e5a08b0b342c5f113f9fe9a81 (soc root replica)
             * ff647fabcd17ad4908f8d5cfb0d215d67ccab6639b62f612239566b42be4dc10 (soc root replica)
             * 2b0824fe874962f44bf2c04cf0e83735e522ebc1ca7b1c9b10d07efd04b04f12 (soc root replica)
             */
            
            // Create files.
            var fileRand = new Random(0);

            var fileAData = new byte[8 * 1024];
            var fileBData = new byte[12 * 1024];
            var fileCData = new byte[16 * 1024];
            
            fileRand.NextBytes(fileAData);
            fileRand.NextBytes(fileBData);
            fileRand.NextBytes(fileCData);
            
            // Build manifest.
            var dirManifest = new WritableMantarayManifest(
                chunkStore,
                postageStamper,
                redundancyLevel,
                false,
                0,
                null);

            var fileAHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                postageStamper,
                redundancyLevel,
                false,
                0,
                null);
            var fileAEntryMetadata = new Dictionary<string, string>
            {
                [ManifestEntry.ContentTypeKey] = "application/octet-stream",
                [ManifestEntry.FilenameKey] = "fileA"
            };
            var fileAHashingResult = await fileAHasherPipeline.HashDataAsync(fileAData).ConfigureAwait(false);
            dirManifest.Add(
                "fileA",
                ManifestEntry.NewFile(
                    fileAHashingResult.Hash,
                    fileAEntryMetadata));
            dirManifest.Add(
                "myDir/fileA",
                ManifestEntry.NewFile(
                    fileAHashingResult.Hash,
                    fileAEntryMetadata));

            var fileBHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                postageStamper,
                redundancyLevel,
                false,
                0,
                null);
            var fileBEntryMetadata = new Dictionary<string, string>
            {
                [ManifestEntry.ContentTypeKey] = "application/octet-stream",
                [ManifestEntry.FilenameKey] = "fileB"
            };
            var fileBHashingResult = await fileBHasherPipeline.HashDataAsync(fileBData).ConfigureAwait(false);
            dirManifest.Add(
                "fileB",
                ManifestEntry.NewFile(
                    fileBHashingResult.Hash,
                    fileBEntryMetadata));

            var fileCHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                postageStamper,
                redundancyLevel,
                false,
                0,
                null);
            var fileCEntryMetadata = new Dictionary<string, string>
            {
                [ManifestEntry.ContentTypeKey] = "application/octet-stream",
                [ManifestEntry.FilenameKey] = "fileC"
            };
            var fileCHashingResult = await fileCHasherPipeline.HashDataAsync(fileCData).ConfigureAwait(false);
            dirManifest.Add(
                "myDir/fileC",
                ManifestEntry.NewFile(
                    fileCHashingResult.Hash,
                    fileCEntryMetadata));

            // Build manifest chunks.
            await dirManifest.GetReferenceAsync(new Hasher()).ConfigureAwait(false);

            return chunkStore;
        }
        
        private static async Task<(IChunkStore ChunkStore, SwarmReference SocReference)> BuildSocChunkStoreAsync()
        {
            var chunkStore = new MemoryChunkStore();
            var hasher = new Hasher();
            
            // Store a data chunk, and a signed single owner chunk wrapping it.
            var innerData = new byte[42];
            new Random(0).NextBytes(innerData);
            var innerReference = await new ChunkService().WriteDataChunksAsync(chunkStore, innerData).ConfigureAwait(false);
            var innerChunk = (SwarmCac)await chunkStore.GetAsync(innerReference.Hash).ConfigureAwait(false);
            
            var privateKey = new EthPrivateKey("0x4f3edf983ac636a65a842ce7c78d9aa706d3b113bce9c46f30d7d21715b23b1d");
            var soc = new SwarmSoc(
                Enumerable.Range(0, SwarmSocIdentifier.IdentifierSize).Select(i => (byte)i).ToArray(),
                privateKey.Address,
                innerChunk);
            soc.SignWithPrivateKey(privateKey, hasher);
            await chunkStore.AddAsync(soc).ConfigureAwait(false);

            return (chunkStore, soc.Hash);
        }
    }
}