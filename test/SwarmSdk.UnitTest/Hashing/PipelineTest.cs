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

using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.SwarmSdk.Hashing
{
    public class PipelineTest
    {
        // Internal classes.
        public record ProduceCorrectRootReplicasWithRedundancyTestElement(
            int InputDataSize,
            RedundancyLevel RedundancyLevel,
            IEnumerable<SwarmHash> ExpectedReplicaHashes);
        
        // Data.
        public static IEnumerable<object[]> ProduceCorrectRootReplicasWithRedundancyTests
        {
            get
            {
                var tests = new List<ProduceCorrectRootReplicasWithRedundancyTestElement>
                {
                    // Redundancy None, 0B
                    new(0, RedundancyLevel.None, []),
                    
                    // Redundancy None, 1B
                    new(1, RedundancyLevel.None, []),
                    
                    // Redundancy None, maxChunkData -1
                    new(4095, RedundancyLevel.None, []),
                    
                    // Redundancy None, maxChunkData
                    new(4096, RedundancyLevel.None, []),
                    
                    // Redundancy None, maxChunkData +1
                    new(4097, RedundancyLevel.None, []),
                    
                    // Redundancy None, 128*maxChunkData -1
                    new(524287, RedundancyLevel.None, []),
                    
                    // Redundancy None, 128*maxChunkData
                    new(524288, RedundancyLevel.None, []),
                    
                    // Redundancy None, 128*maxChunkData +1
                    new(524289, RedundancyLevel.None, []),
                    
                    // Redundancy None, 1MB
                    new(1048576, RedundancyLevel.None, []),
                    
                    // Redundancy Medium, 0B
                    new(0, RedundancyLevel.Medium,
                    [
                        "43699f9b93dee932f52cff46d9d62c926fbbcbfa78e9c0572a9e6b20187561ae",
                        "874c30b727f81d7b6d47a786070aba29226b0f8cd0ff4963db6836c9bbc36e8e"
                    ]),
                    
                    // Redundancy Medium, 1B
                    new(1, RedundancyLevel.Medium,
                    [
                        "243bcf22a106f1a1135cb4627db02d8b98b3e5318c2dfbbb975fb421470846a6",
                        "f51098e724b882b2bf09b36023b31ce42c95466afdf401b958e754e38a5350ea"
                    ]),
                    
                    // Redundancy Medium, maxChunkData -1
                    new(4095, RedundancyLevel.Medium,
                    [
                        "28a7a6bf4e520004042866d29ccfa2e5162be144f91d7d67a3ebecca8995a246",
                        "8204076fdac8501127fa66aab8069ec56706b27cfa0d4bfcbf3d26868bc05603"
                    ]),
                    
                    // Redundancy Medium, maxChunkData
                    new(4096, RedundancyLevel.Medium, 
                    [
                        "7a4570d7f499ea02868b9bb33faea9b0661664cb3870edde45255588f5942483",
                        "b817419b37b74c84772d975d5be034a6d1fcf147e43720bad6e1a88ebc37845d"
                    ]),
                    
                    // Redundancy Medium, maxChunkData +1
                    new(4097, RedundancyLevel.Medium, 
                    [
                        "6e7c5acd043ea9090ab440a1da7628871ca47e61c1a1d7cefbe4fd94874759c4",
                        "904f173d51c491d59fedc5a38aaa1a90ae26afe84925700e920b1bee13b18ecf"
                    ]),
                    
                    // Redundancy Medium, 128*maxChunkData -1
                    new(524287, RedundancyLevel.Medium, 
                    [
                        "36364ba813ec29f1cc14b997008232b9bf3d511c87af72275a97334b6fd01c47",
                        "8d99cf115c212cf3d1f33ffc62e2fc952620fcbadac3faf34529e177ac63c172"
                    ]),
                    
                    // Redundancy Medium, 128*maxChunkData
                    new(524288, RedundancyLevel.Medium, 
                    [
                        "16155c8d629e277ca5ca2222a7d51be33282a3403cafe8d944ae1a57b2ab42a8",
                        "88a13ecdf8a7f097b3a60bf1e881189803131476d3a7ba68110213e0600b9886"
                    ]),
                    
                    // Redundancy Medium, 128*maxChunkData +1
                    new(524289, RedundancyLevel.Medium, 
                    [
                        "10026977da3563e98057c5e49fa97b73a6cca355ed6f5f6b26179ac5cbacc27e",
                        "cce25fd0bf01c47cc14a84a066921baa2ba9fcd92285b5efb5f4ef1f7def9f0e"
                    ]),
                    
                    // Redundancy Medium, 1MB
                    new(1048576, RedundancyLevel.Medium, 
                    [
                        "5f6e64c34304e1d5bdbc2ae538b8a61fee3c92869444d100fe382a2a8bdd9cdd",
                        "c309d663a1bd0e307d246dd65f1131fa08377bf5b53210238154ad79258b42b8"
                    ]),
                    
                    // Redundancy Strong, 0B
                    new(0, RedundancyLevel.Strong,
                    [
                        "3829fb0146574db31c503819de1af192cdf4e412f99e9624894e68f4929dfdfe",
                        "43699f9b93dee932f52cff46d9d62c926fbbcbfa78e9c0572a9e6b20187561ae",
                        "874c30b727f81d7b6d47a786070aba29226b0f8cd0ff4963db6836c9bbc36e8e",
                        "fd756165e55097c0ed6340732d38edf14c43a3d7adf111bfbbcb1195c1b025b5"
                    ]),
                    
                    // Redundancy Strong, 1B
                    new(1, RedundancyLevel.Strong,
                    [
                        "243bcf22a106f1a1135cb4627db02d8b98b3e5318c2dfbbb975fb421470846a6",
                        "5c668ccb3c5d270c7b5058f1f429f7aad265836194fdd01d93bb1a0096a572f4",
                        "bc32fa5f1d87ebaba6144032dcf15214af8970ba2253e593f54e123f171e021b",
                        "f51098e724b882b2bf09b36023b31ce42c95466afdf401b958e754e38a5350ea"
                    ]),
                    
                    // Redundancy Strong, maxChunkData -1
                    new(4095, RedundancyLevel.Strong,
                    [
                        "28a7a6bf4e520004042866d29ccfa2e5162be144f91d7d67a3ebecca8995a246",
                        "5c2022fdadace0d10f91618b67cb758afc7d1505e57cceeef412ac57a09da2a2",
                        "8204076fdac8501127fa66aab8069ec56706b27cfa0d4bfcbf3d26868bc05603",
                        "e6b327b806cbc3f1af41299ae5fcf4608bd2824cc9a2030945152f7ecb854b8b"
                    ]),
                    
                    // Redundancy Strong, maxChunkData
                    new(4096, RedundancyLevel.Strong,
                    [
                        "3073699f9f89e1744c8f77e4f1080fb3e648bc73f791e8e43f45792ac4daa5c2",
                        "7a4570d7f499ea02868b9bb33faea9b0661664cb3870edde45255588f5942483",
                        "b817419b37b74c84772d975d5be034a6d1fcf147e43720bad6e1a88ebc37845d",
                        "e100473152cc3460b832b116ca83d011627ef8949771eaab54bc571474bd4620"
                    ]),
                    
                    // Redundancy Strong, maxChunkData +1
                    new(4097, RedundancyLevel.Strong,
                    [
                        "3f1006333b4071b9128af2906375df36767ccc19b8755ad06dd602b16e146cb8",
                        "7407fea500f21e6e6df373eacdc32e6cc14ef7c21282d90ba05fe88d291068a0",
                        "a5406726ae8d99f3a6cb1829e7ae61a4907adc321765e3d66b6e07053c393753",
                        "d1a75d245c983523094992e693df1b03d13ae7a875245e6c5ea0aadb6f2c7077"
                    ]),
                    
                    // Redundancy Strong, 128*maxChunkData -1
                    new(524287, RedundancyLevel.Strong,
                    [
                        "2c3cbf13048f1dd1e639dbe77d1469cd1c8863b7775453a78e5a00a553fd58c6",
                        "4d0376b8997a6f085ae383ace3fd7ee041164d9a32fc2e633368c3977eabcf1a",
                        "91180c07a5f6320608c52d19a7a741071d03586149db74c797cc6d3d25afb1c5",
                        "f4eda25091605c25e9b64b8904a2ec0e573bfd43e50e594fe5b1479d564de947"
                    ]),
                    
                    // Redundancy Strong, 128*maxChunkData
                    new(524288, RedundancyLevel.Strong,
                    [
                        "041fb0ab4b1fa1527acd4cbbaa5a2ebebe9141e54cfe8202c56b8b2b00fee5a3",
                        "7a41dd7f669bc47de7dadd3b0bac0ac07dcb47f45d3528195e9e25574c8744b9",
                        "ac5fbe3e4087cb62b8ef892c4dbeda05f96f9740f148a0191eef6291d20ddfa3",
                        "c34c277a4018835343bf478341a83d43489c0c320365eebf52abd64a948396f7"
                    ]),
                    
                    // Redundancy Strong, 128*maxChunkData +1
                    new(524289, RedundancyLevel.Strong,
                    [
                        "2d9cbb9017aa2f99f55b5ac66f1049a4fae74146cfa85135d5485d36253baec7",
                        "56f93df1415e4706c77cdd24a1034513007b0b2ec2a4e3c23b5bdc18abc8ad8d",
                        "8089c86d7277558d3a6afb13d7cc170c3c01f824b045c544806920a51f5cf6ce",
                        "e33d989eac94884d3235784124dcc762f0485a62acb718ecfd4e15b2c9065d6e"
                    ]),
                    
                    // Redundancy Strong, 1MB
                    new(1048576, RedundancyLevel.Strong,
                    [
                        "3938ac1cfbcc9f59e4d2fb920fd4d373e2a1534969578f5e5e0473a79ca12d56",
                        "7b12ac5ccb42e7b141322ea64ae2860bfbe838533be39f06ae266d99be2283e8",
                        "ad03f3f7e59756e4d786ffe88c65d280f222ff75116195e143e53290ea8a24f0",
                        "d7de107467be995ae237f0b569d57d9a3f7d5d14d03c78375bf45fef3734852b"
                    ]),
                    
                    // Redundancy Insane, 0B
                    new(0, RedundancyLevel.Insane,
                    [
                        "874c30b727f81d7b6d47a786070aba29226b0f8cd0ff4963db6836c9bbc36e8e",
                        "fd756165e55097c0ed6340732d38edf14c43a3d7adf111bfbbcb1195c1b025b5",
                        "3829fb0146574db31c503819de1af192cdf4e412f99e9624894e68f4929dfdfe",
                        "b21a88e7857fad79046bfc73bd2ca4b3ae32ca81e430fd4e5fe92ee04bf584b3",
                        "61489001ff79af2fb15576f6f4f30809daaaf70826cab8b4fa7f1b8302814e47",
                        "0906a2146d58a2f1964e2d50488688d6c0c4446adcff083e0294dae5bd4fb076",
                        "c8dd7fde34cdae709b17fce163055653e94d5d4e8bd980646f52d125fbeb9e42",
                        "43699f9b93dee932f52cff46d9d62c926fbbcbfa78e9c0572a9e6b20187561ae"
                    ]),
                    
                    // Redundancy Insane, 1B
                    new(1, RedundancyLevel.Insane,
                    [
                        "d135f09a5ccbec8841bc3292c0e2ad6e0b036fb2ab1356757382a8ff48507c21",
                        "bc32fa5f1d87ebaba6144032dcf15214af8970ba2253e593f54e123f171e021b",
                        "9d66e5020d3f29015e06a9b0d99936c5d9157b98fc75db9e53f1af14f44ca2d4",
                        "f51098e724b882b2bf09b36023b31ce42c95466afdf401b958e754e38a5350ea",
                        "5c668ccb3c5d270c7b5058f1f429f7aad265836194fdd01d93bb1a0096a572f4",
                        "1eefacd16cca2948781b525c5059cdf8580ad81e4d0913f79075feb7878a4d1c",
                        "6fe43c8d76bd29c0c7bab674c40fda0b4c6402f8d075af54b53787f6bf604229",
                        "243bcf22a106f1a1135cb4627db02d8b98b3e5318c2dfbbb975fb421470846a6"
                    ]),
                    
                    // Redundancy Insane, maxChunkData -1
                    new(4095, RedundancyLevel.Insane,
                    [
                        "1f998b58cd4f213c33bc6995777ce1d8bd0bd5b542c983ac138870ed120e6bdb",
                        "e6b327b806cbc3f1af41299ae5fcf4608bd2824cc9a2030945152f7ecb854b8b",
                        "d43c23ff8ad771d132974c0dcc40229c7e30ab089bd5d8933b5737c4a4967a23",
                        "b2b63011b33bbda1ea83638c5dba1e7f0de77f2e16661d06483982310535e1e7",
                        "5c2022fdadace0d10f91618b67cb758afc7d1505e57cceeef412ac57a09da2a2",
                        "8204076fdac8501127fa66aab8069ec56706b27cfa0d4bfcbf3d26868bc05603",
                        "28a7a6bf4e520004042866d29ccfa2e5162be144f91d7d67a3ebecca8995a246",
                        "7c5689b53c33bb38f9ac4ac4f3e91fa1dd7fd84606add0788c2eeb06fbcaddd9"
                    ]),
                    
                    // Redundancy Insane, maxChunkData
                    new(4096, RedundancyLevel.Insane,
                    [
                        "b817419b37b74c84772d975d5be034a6d1fcf147e43720bad6e1a88ebc37845d",
                        "00e38da09da4f402e2fcbf1bf5dc1a30e36f42d5217132a84dae9a7744dfac4f",
                        "e100473152cc3460b832b116ca83d011627ef8949771eaab54bc571474bd4620",
                        "7a4570d7f499ea02868b9bb33faea9b0661664cb3870edde45255588f5942483",
                        "4d01157e1652a75c3d4b6011f38611e3f6833fa51b70a9e0162b78dfd233b77f",
                        "3073699f9f89e1744c8f77e4f1080fb3e648bc73f791e8e43f45792ac4daa5c2",
                        "d137ec13b4e6eeffb7be8c65c89a914368015dba7a5a9fb9eb83176a19d3c213",
                        "9a15ca88274be80f315a1799c30a50730537fbaa0241745fab21a957251091a0"
                    ]),
                    
                    // Redundancy Insane, maxChunkData +1
                    new(4097, RedundancyLevel.Insane,
                    [
                        "cdb14047ad8c667522a37014a90793ecd9b007a9de96ec9156f4f68b82c91ed5",
                        "9b83c165c82c3b3b5b8937409ae0a842d3ca281cdbb2d7f6650a37310e297b43",
                        "fd24d4aac7a196c13141d7e4f7034080f3e7f4cb3e72f64c366105384453cccf",
                        "62afd86a736520c3720214da0afe5202088a739479e576893f6f6683c013f2cd",
                        "515f14833590f70db395ed3a93313a1a87bcd38e9ed437a9551303d1edee6059",
                        "28b9a9e5f24ba7bd15362d5af64e2bef03f54f849e819f05bd7f02745abbc79f",
                        "0b82bd2ef1a8c29ae69a704c67e27df38b0ff7d7c283b91661f193690a43e0da",
                        "a24114a998c1c2217bfcd39ef9f06eb89a907b3638969e7cfb4613728c85bdb7"
                    ]),
                    
                    // Redundancy Insane, 128*maxChunkData -1
                    new(524287, RedundancyLevel.Insane,
                    [
                        "acd6ecdb05000fcecb3e90eb89c5a8e1333b7f59a2b8410984fdcb44c8c03d54",
                        "1d24ff1ae2551dbc5679075200215ad06d4ae7c4b70fe3d2be2201a4d0f0e8e9",
                        "87ca9128d449a5fd5d7f01c100c35ac32d1d547bac4ec5e42916c472cf01b7f6",
                        "cea0fe84ae50056bd0f775f72b1d9ea28732f685ba03df1fefc1ac2d4f87304c",
                        "6a10dc693378f64f7147162dc5cb62213d5cf16ec69082b0809de212e18da319",
                        "ef02096594d78e72aab8726487352e342c19b81e8921bdf24c75fa4f93e6656b",
                        "4836885c89c1ed8aa02e1230de16f2f62880e49faae94b336985963490df4740",
                        "32cbf18cc7d8558f7877962090c4389d40810a37dc04aa06933d549bef288a6f"
                    ]),
                    
                    // Redundancy Insane, 128*maxChunkData
                    new(524288, RedundancyLevel.Insane,
                    [
                        "fa4d4da0ef8e339c5508f5b963ad97a31001ee76ce00b9dfbfb09ed52189d8d2",
                        "75c5a95b10f0cf42a7fb31e2e264ca5523dc3917f26b8ea21fec905eda65e360",
                        "c4962cd84390bd9fc9b704ef259646754e65c6629af2e4b20e3368d50f7986a9",
                        "1a7684cf30a909a8abf4e992d880f4ff171739b2b75e2b44f696796f1b8a5fb5",
                        "30b6e059d55b4a7bf4c53bc4c498eee896611fff0855e642b8c85c54bf6d1fb6",
                        "a7884f44b3bd85da34b5ec91944d2e006e15b2df0459fae8ca76383827250dea",
                        "808b689a03432f637714e1bd4687ddc813cb8c3b0a9db6e9cb739c820a76b32e",
                        "5f6e6b565793ad1d2933edeebea05792adb2b9ec30a55432b3a24eb01bc7f67b"
                    ]),
                    
                    // Redundancy Insane, 128*maxChunkData +1
                    new(524289, RedundancyLevel.Insane,
                    [
                        "0539e16fdfa9727220b9d9c388e1c88aeb8db63a64b4443f3e4304595dc2b79c",
                        "aa7ab3291962d5679c4cd7c1aac5affc6190e1bf0a51d73c123d8e216883f827",
                        "f7d8e654b3c023850bb9c15e0bf0df4bb7ad7cdaa2a092ff2a19e303958888b0",
                        "d30faafd3dcfe15d96b2f7baf6477dfbffc2c1cf42c0a0ff0125b09d40467936",
                        "36265bce0875b2c2e19a19e0c9991406aae741694087653e6738e8017f62b818",
                        "6264ce8b1f9327314406a97d14cd85e8072437b5ba2a2e6615e9503292a8a852",
                        "5f860f6f834cdfeb524fb803f19967bedd0499ec5f8d1570768d9a83dfaaed13",
                        "9969f5d900fe5fd7ab5c84d31f672e77a3ebb36ad8c48389cd9e85c132349645"
                    ]),
                    
                    // Redundancy Insane, 1MB
                    new(1048576, RedundancyLevel.Insane,
                    [
                        "ae57e50d56a497b62056435c73adaacf231e73b2eac883cd557d2e3a199e3671",
                        "ecd81c55ae3ecb9213a3a067a2dd0e110f72c64e41c3511fc348426e051988ba",
                        "6c56b2e27a90999c1095010d89b90800900f5c15488c27c970702d183a883448",
                        "c52fe593701134033434de45f88227c323f7ff784d717fbd4fbbc73974c52256",
                        "292133fe10e7a4a1402d33bebab7ed003917196e383690fd776478a1e3453257",
                        "082a167588c8b89655c503b455738dbd8990833e99d96160eb7a01047df647c0",
                        "55b3adf5757eeae8253b70db8d211f42476afa5c867f9de71c939bdf3726c198",
                        "8baa174ca1e3210b216b3127f17f12b83fbd4b4b8b8cc7253b6a3ed491880fd9"
                    ]),
                    
                    // Redundancy Paranoid, 0B
                    new(0, RedundancyLevel.Paranoid,
                    [
                        "ecd7d6ae16115b6caf218e7d98a0ee91a965d23d4f51cb98d4b8fa5d4e6fa0e6",
                        "874c30b727f81d7b6d47a786070aba29226b0f8cd0ff4963db6836c9bbc36e8e",
                        "59fa5a993b8cb80dfcf2bd4cf98fcc6f61f8b1ee5c7259c897af4c006a16bddc",
                        "90ac131277179aa8294096d3d03ac80aeb509be80cb221500bbcb4f22091fae2",
                        "b21a88e7857fad79046bfc73bd2ca4b3ae32ca81e430fd4e5fe92ee04bf584b3",
                        "43699f9b93dee932f52cff46d9d62c926fbbcbfa78e9c0572a9e6b20187561ae",
                        "292bf72ea651d8526f2bb6582f5351bccceebbbb8fd0cc67dcfed09221b9dcce",
                        "fd756165e55097c0ed6340732d38edf14c43a3d7adf111bfbbcb1195c1b025b5",
                        "3829fb0146574db31c503819de1af192cdf4e412f99e9624894e68f4929dfdfe",
                        "61489001ff79af2fb15576f6f4f30809daaaf70826cab8b4fa7f1b8302814e47",
                        "0906a2146d58a2f1964e2d50488688d6c0c4446adcff083e0294dae5bd4fb076",
                        "afb458abb7daabd459cba446d3abd9d57821f3987965dd8781cb9c11e41df1e7",
                        "d069e87c821ed269b986cbb29ade4b87f28179eb9bbaceec850624d56fac7bb2",
                        "15bdcdd951db96ea035c4fabff2fb538cdb3a243e4fe44ce2b424fba7ea6ac5f",
                        "c8dd7fde34cdae709b17fce163055653e94d5d4e8bd980646f52d125fbeb9e42",
                        "71e7a775e280be92a87c83f48430e641b1ceca41cb6b21d983f4634d4ebb2ac3"
                    ]),
                    
                    // Redundancy Paranoid, 1B
                    new(1, RedundancyLevel.Paranoid,
                    [
                        "0c9588d488827ccf218b2201ea7ac4f3588e5fee4a4cce2c17d4eabb4f79fc80",
                        "4e741c1c5b734881449c096d64ccbc44493a217d4d5ee4269f9060957abaa65a",
                        "f51098e724b882b2bf09b36023b31ce42c95466afdf401b958e754e38a5350ea",
                        "243bcf22a106f1a1135cb4627db02d8b98b3e5318c2dfbbb975fb421470846a6",
                        "5c668ccb3c5d270c7b5058f1f429f7aad265836194fdd01d93bb1a0096a572f4",
                        "d135f09a5ccbec8841bc3292c0e2ad6e0b036fb2ab1356757382a8ff48507c21",
                        "86a7989ed8dae46ee14397198a76119e6c6e15179a3f60607e433a4c695fdd7e",
                        "e4c72632bb00ecd474982add4510a2672e4f3f11dbfd7b6fe7cf94c8466c2470",
                        "1eefacd16cca2948781b525c5059cdf8580ad81e4d0913f79075feb7878a4d1c",
                        "ae66c010f8af2a04f4dfb9de370d3e8b0c7390eb0535c5c49cbdba4e1d8c824b",
                        "3249bed6e55d71b3c0593510364a9696ffe2a414c23762a218b55efa5d44f58b",
                        "71468e3d4fe483529b10d3a4966f6ef8f5c281bb3de6325a6f1715626824a12f",
                        "9d66e5020d3f29015e06a9b0d99936c5d9157b98fc75db9e53f1af14f44ca2d4",
                        "bc32fa5f1d87ebaba6144032dcf15214af8970ba2253e593f54e123f171e021b",
                        "6fe43c8d76bd29c0c7bab674c40fda0b4c6402f8d075af54b53787f6bf604229",
                        "cb9fa58d8842cdb3b835771c7d4e5a2636b5e99de31684e8375f006e0e847a6f"
                    ]),
                    
                    // Redundancy Paranoid, maxChunkData -1
                    new(4095, RedundancyLevel.Paranoid,
                    [
                        "5c2022fdadace0d10f91618b67cb758afc7d1505e57cceeef412ac57a09da2a2",
                        "f60e0db37a639ff4ead6fcf9d90011fa626aa47ba28b9955c8294c52cc345692",
                        "28a7a6bf4e520004042866d29ccfa2e5162be144f91d7d67a3ebecca8995a246",
                        "6c097f9db4d2474fd357b4a7cbc0af4ca0ac299100511b5bdb1dc10f2f597e80",
                        "98dad46c57fa80a04467601e601d825baa33050fb6d49ee310f9a13243349924",
                        "0b28fd925694f02fe3aa7546d4480fee8d78bb3129808895a297c77f6e033203",
                        "8204076fdac8501127fa66aab8069ec56706b27cfa0d4bfcbf3d26868bc05603",
                        "4a50d690520e793d8900f1944dcf0c4c9f6202f15d4d8ac6aff470e7a35e6561",
                        "e6b327b806cbc3f1af41299ae5fcf4608bd2824cc9a2030945152f7ecb854b8b",
                        "3405e89e2315ecb387dcab5dabbefe171ad7fd6d5b66b5c1ffe3c17e437c151b",
                        "7c5689b53c33bb38f9ac4ac4f3e91fa1dd7fd84606add0788c2eeb06fbcaddd9",
                        "1f998b58cd4f213c33bc6995777ce1d8bd0bd5b542c983ac138870ed120e6bdb",
                        "cd5c0b2b0acd229ff5f9a8d4df25e3a6bf3938ae96b6b16b2e1ee8707c6ff272",
                        "abed4e850f4eb6a2afbcba03894e810b05e0ab2bc6b9e7ae18f46b76988a1863",
                        "d43c23ff8ad771d132974c0dcc40229c7e30ab089bd5d8933b5737c4a4967a23",
                        "b2b63011b33bbda1ea83638c5dba1e7f0de77f2e16661d06483982310535e1e7"
                    ]),
                    
                    // Redundancy Paranoid, maxChunkData
                    new(4096, RedundancyLevel.Paranoid,
                    [
                        "d137ec13b4e6eeffb7be8c65c89a914368015dba7a5a9fb9eb83176a19d3c213",
                        "52c7228ab7a9b3063eea22e338b86fa5f28c20b79a2cd20672c6f808567e0ce4",
                        "6784daddb2d5aad5b5d0d090576354db49966120d0e8ae549b09db3518292cdb",
                        "88dab80d07c15eda6f57af1623c16e420c54e9fdc9b864feefd60f4ec25c8c3e",
                        "4d01157e1652a75c3d4b6011f38611e3f6833fa51b70a9e0162b78dfd233b77f",
                        "fb728cc5cf6d5e41b923fcca0e76136b0e155569a2ddcadae2e5f437caa61f17",
                        "e100473152cc3460b832b116ca83d011627ef8949771eaab54bc571474bd4620",
                        "9a15ca88274be80f315a1799c30a50730537fbaa0241745fab21a957251091a0",
                        "b817419b37b74c84772d975d5be034a6d1fcf147e43720bad6e1a88ebc37845d",
                        "00e38da09da4f402e2fcbf1bf5dc1a30e36f42d5217132a84dae9a7744dfac4f",
                        "7a4570d7f499ea02868b9bb33faea9b0661664cb3870edde45255588f5942483",
                        "c02a8f7f8bb427dfb4f38c910cb7814b355af4c578e8b23c8c7d1968ad81e39e",
                        "1144ab88c08c0285bef0d61c16d02b23313dc317b6874d60a6a2026a3798f325",
                        "3073699f9f89e1744c8f77e4f1080fb3e648bc73f791e8e43f45792ac4daa5c2",
                        "a808b6d7e206456924165bf0eb516f6f819369bebf57a65e3baf59fd01b4ea23",
                        "28f875a6be1591a4cd580534717e76b4bb127670543a53bb5b1de0f09d49e9a5"
                    ]),
                    
                    // Redundancy Paranoid, maxChunkData +1
                    new(4097, RedundancyLevel.Paranoid,
                    [
                        "11d68e7c6331b112e4b2386fe101ef5029fbb04c10f3050ca57092bb3e9582ca",
                        "7dc57bbdc9f35e9d6b3743596ec576decf11bf4ff8561c5923e19932ce9c2a98",
                        "8aa7f15e126bddb671af6fd258d4110f1112b623240201fbf1f845228389f943",
                        "fe910b6b035ee876898e89c73340ebf2789eedd183dfebe00f4086a26fe6ff4b",
                        "36525152732510e3feefcf9dd27f34d664d4c8413f51c8365a4742808d508e73",
                        "c8fd0c89df603081cfea138aa070b612a3f0c0fdd1f379e2a7c2ab45dc39491e",
                        "639e148ac84ac4a4dc3cb998982166d8955001f2546044c1ccc2531475d5dcf6",
                        "b0f2e33d7af50b84b715cece98fad3f59c29731844b0b727b5aad7d8f57930f2",
                        "dc4ce00d2083108468e066ec7b8415fcd1e5b2797d1a6bbc745b12fb9f22034f",
                        "274b7b0bfa729fd7954afd690ca9cc88579459cc006435e822b525b966b61554",
                        "9d77299fff99f1328d75309bdaf9dcf9f06b81960e884101559658baa75d2733",
                        "ec51f8caaf0635fde025cf3c2e119762cca981c673ada565feaa8439e3cdc431",
                        "46b3051dc96a472c27f29db3127100cdeb8830396b25500fa32c338ad1a2ab76",
                        "5cb074accf40b3ad2338482fd26feb36bfb8c4b16794518916963b39710741d4",
                        "02bf43681add6b34bfedaea4239b4d8aeb31bc9a21aaf471263e42bfed9032cc",
                        "a547e1d47a9b5727c32c9b619be53d1f8c7b469059738d4ac46991f02c3d335c"
                    ]),
                    
                    // Redundancy Paranoid, 128*maxChunkData -1
                    new(524287, RedundancyLevel.Paranoid,
                    [
                        "a61f22a56ef60aba84feaf9c78c5ce31cf1cb18e12024ae7813779e825f4bd85",
                        "2a675b5cbb3d67f2e39751767f0b5dbb5dc2829675f258dbc8104183b3a8eb96",
                        "1767415f06cb6dc37c8896b87689505feb04eac5f3f24ebc9fd7332ade032047",
                        "042ecda803bc3c0999ba6c7a5dc782987673ef0bc77a8e6d952edbed2b6345c1",
                        "3915e90ef9c6130a2e1cfa5096bdb2550c4376f2b41abb6e85f2d80c05760f32",
                        "499230833c159383f090fb5d8c5d75792ab57db62bcb07ba5c1ee82e149a12c7",
                        "566fa4fd1f898bc67bce473a4ca641d4b139ec867e048c3e510f2c36c3f4976c",
                        "993c105bf1783542d455b597a0cd8b71a526170f494023be7aaea5409df996bf",
                        "d7570112db097c4ed06d501173ca06fa2c33eee84f57a66fd9ef7c8326907ba4",
                        "c33795104fb7cc6dc3cdb617d7aae450c910806be9b741d690cded8ee2791cc5",
                        "777fe4fd3b4097003a81ea7da7673c8eea5dbbdc47763220962c4c9678b679af",
                        "bbdb945c2fa73823a2b41345a194f2886676dca3cf46c642f899510a084e195a",
                        "8abdb044f154233008ba4b6fa2ffd032c6507e335b9460fd0a99391754b646a0",
                        "64d274790ced26355e8bf049a97d978e6a8488d6ccb6668d6936ecac99c73b79",
                        "e95123c9866d60ae8e0b3d43e7ef074227e96984a49238eb11abb24dd4216708",
                        "f0dc07ed7c8b99a16bd058542a030054743940451ab3f0eea1a5dee3e9498a96"
                    ]),
                    
                    // Redundancy Paranoid, 128*maxChunkData
                    new(524288, RedundancyLevel.Paranoid,
                    [
                        "40a26a4cfe813d9c33872706226a50fd9c3b35f1c5e696c8033f5caf1432aeb8",
                        "0cca456dca1328c5c1bb98e3e29428debfb6a437eff4e59319334f3fee8ece0c",
                        "30f0a50be458d35a52e3c61ea13cd0c1753eb1f961545423756b093ecd81a991",
                        "d4ca26b2d909acfa4b3216f699230913516830ba7da9b6f9cdbf0b51b0769ab7",
                        "133bb78b75a42f3cc1ce16b7f44608e11f00becd40ace0ed8945bcf1db9b5086",
                        "cd9c4243e678c6122ae8a785a0d77cddbef360694931c70470a78649cfc6abc0",
                        "b419ebf918c57ea158732ac1789609e54ec72be39f1b39f9eb0741850e4cab35",
                        "57b8ed1fc34725b6268dbc2281378e24d34f3b41521f47b0257c830ba745a742",
                        "2a1de023ce9507ac106ad34779e0322dfe087f8d7505db728864501b867f93f5",
                        "7281b8911c1ea3705c7380ba533d9c63f0ecc47328cf5efc929e778a864bf0dc",
                        "f224799fdda83f338989ef0ca44116bc814e4b29871387deed96a0c853d282b4",
                        "984e1c70b8248c469dd14ada30f173df60465451f0322d82474426e2b1283744",
                        "6fa3e29b2a2a9edf728bd4866b12f1d0145da0d4282c2192c70f42732c865c72",
                        "8c5db0e7bb73768421e78e27d15769ac9804855d56b4b94b9cd93dcac8c4b10a",
                        "a32e51889297e2afd7e538ed557ad8b4740aaa937bcd1efd57d80feffb23fc76",
                        "ea735cc8e98dc8202d30cd0ea28fec5b56965d892ef151f404aed9798bf87c08"
                    ]),
                    
                    // Redundancy Paranoid, 128*maxChunkData +1
                    new(524289, RedundancyLevel.Paranoid,
                    [
                        "63b7faf09439f0e203d4caf42407b83c4c2a0f223258574eeeb7b69ebf0c2466",
                        "37029c0364d2e24b62b6c81f2cc6b560703c4001c57f8ccf4d6a8d6725bef725",
                        "9440fd8dfcbee81c6171f0b3de2424b446837b2eeebb360bdf4d10be26b2059a",
                        "7c18a97a0d82bc1693e0b5dffe99118d2252280ac28db807a0eeae7c9b17a8dc",
                        "b622caa8188dfc26a61179f0ce8ea49552a2c3875f099d917cb9273aad03c5fb",
                        "8ae09c086196c7c89873e993d1fd7ecc417fe142970daf1f9a144d35a83e8f84",
                        "d261ab71d1b547bff6d93014eb4f088d5ad4823bf1f4968df2991535d5497b22",
                        "55a577d438e3886010ec57e378a536b2d9e601010d856e98cf733998f8cb0636",
                        "f29dd48db495b1fe357e31aa9fe673fd084ff58de876e5c9c28b3c0e99e29a32",
                        "0ecf18a1ccdfbaa6648c25f446ee3823078ef8455b009336044ebe90d482b411",
                        "eb40b7ee160d219af28fa779b3907fb41e752509ec0e40614a5cbd8232836a81",
                        "21493105a2bdc6264d8da36d9e59799c2a8ee4917964b7dbdc06a6655879567f",
                        "4132ca28a7bb5755e7ac11a619ca8e6c3e2e62d642674fa515536557065deddd",
                        "1ee9bcbc5da2c2bf252183ec8a3789e41004bb290ce39354dbcf0fbdc36c8d4f",
                        "a221bc76e8b970807171b6ead0ddd304db8db17365361736e1f7f9c0ef8ee421",
                        "c92888271ea66c9d8308ec9b30361ad89c81f448273b92dd3a0743ad16b4129d"
                    ]),
                    
                    // Redundancy Paranoid, 1MB
                    new(1048576, RedundancyLevel.Paranoid,
                    [
                        "9937705d45206150d9983e064e2d238653ba7d7aa0c720f7e7a58d88d999fe0f",
                        "b73de89e9f8f6dc628b3a8f166935a8bcc1cd36b24ee3f9f7b23716715e34e34",
                        "c0ddb25c3aa523f20e06458efbcad833c9e0711d5c4d1d3772a190dc1df771f2",
                        "0d6fb270b7d669cca5c1546f73f22b61537666e3835dc89b61324f6465851383",
                        "ea6234cecac5ee823dbea37b2cde6d7764114501d2f996ed77755d5970c1e0b6",
                        "86b6d40d169aa3ad6dfc4dde33e4ad1aefe13ab5f068bfa0eda15755d8dbf5be",
                        "2e0e05bd436afee28d0c058a573e5b1b35dcc921d8bc9eb1dd253a5053981945",
                        "75a557a6de42277b3c3ed288194faf339980698a69f663f9812bf03b71babf0c",
                        "5949f251fd16a18fbd5be6ba7f5402e21dda884d5710a1c4d0c42162ea4d7c8e",
                        "1defa84ac6d0cdfe0301075410eab9b2c94c7b99443a519c578054d9ad70407b",
                        "d1da00787c0f9918c254e2bca95900f1ab9e24ffdaff8438b83eb2d5bdf3610f",
                        "a9a5af17cc9530a63966c74c4a42eda0ab59d8807f6f353a03a47f016d526a1c",
                        "f277960e7023bce119187803539cb10f867cce719943f854104f01288089c2d6",
                        "6f82331237a0a729adda8bc85d561a1172b3eb6cb5758434c91724db2d2f7fc2",
                        "35a21f35dd9c5b018a0a290d73e6256bc7a37c7d99590c58f68545b7c48860c9",
                        "4a69c073d340c779948573a9c7c176a94c20adc3b179ab2e04528a90706a4ccb"
                    ])
                };

                return tests.Select(t => new object[] { t });
            }
        }
        
        // Tests.
        [Fact]
        public async Task CancelledRunLeavesNoChunkWriteInFlight()
        {
            // Setup.
            var data = new byte[SwarmCac.DataSize * 64];
            new Random(0).NextBytes(data);

            //a store that cancels the run at its first write, and takes its time to end each one
            using var cancellationTokenSource = new CancellationTokenSource();
            var startedWrites = 0;
            var endedWrites = 0;
            var chunkStoreMock = new Mock<IChunkStore>();
            chunkStoreMock
                .Setup(s => s.AddAsync(It.IsAny<SwarmChunk>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmChunk, CancellationToken>(async (_, _) =>
                {
                    Interlocked.Increment(ref startedWrites);
                    await cancellationTokenSource.CancelAsync();
                    await Task.Delay(50);
                    Interlocked.Increment(ref endedWrites);
                    return true;
                });
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStoreMock.Object,
                new FakePostageStamper(),
                RedundancyLevel.None,
                false,
                0,
                4);

            // Run.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pipeline.HashDataAsync(data, cancellationTokenSource.Token));

            // Assert.
            Assert.Equal(startedWrites, endedWrites);
        }

        [Fact]
        public async Task CancelledRunStopsFeedingChunksAndThrowsTheCancellation()
        {
            // Setup.
            var data = new byte[SwarmCac.DataSize * 64];
            new Random(0).NextBytes(data);

            //a store that cancels the run at its third write
            using var cancellationTokenSource = new CancellationTokenSource();
            var writes = 0;
            var chunkStoreMock = new Mock<IChunkStore>();
            chunkStoreMock
                .Setup(s => s.AddAsync(It.IsAny<SwarmChunk>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmChunk, CancellationToken>(async (_, _) =>
                {
                    if (Interlocked.Increment(ref writes) == 3)
                        await cancellationTokenSource.CancelAsync();
                    return true;
                });
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStoreMock.Object,
                new FakePostageStamper(),
                RedundancyLevel.None,
                false,
                0,
                1);

            // Run.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pipeline.HashDataAsync(data, cancellationTokenSource.Token));

            // Assert.
            //the write that cancels is the last one: no chunk starts once the cancellation is requested
            Assert.Equal(3, writes);
        }

        [Fact]
        public async Task EveryChunkWriteReceivesTheTokenOfTheRun()
        {
            // Setup.
            var data = new byte[SwarmCac.DataSize * 3];
            new Random(0).NextBytes(data);

            using var cancellationTokenSource = new CancellationTokenSource();
            var writes = new ConcurrentQueue<(SwarmChunk Chunk, CancellationToken Token)>();
            var chunkStoreMock = new Mock<IChunkStore>();
            chunkStoreMock
                .Setup(s => s.AddAsync(It.IsAny<SwarmChunk>(), It.IsAny<CancellationToken>()))
                .Returns<SwarmChunk, CancellationToken>((chunk, cancellationToken) =>
                {
                    writes.Enqueue((chunk, cancellationToken));
                    return Task.FromResult(true);
                });
            using var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStoreMock.Object,
                new FakePostageStamper(),
                RedundancyLevel.Medium,
                false,
                0,
                null);

            // Run.
            await pipeline.HashDataAsync(data, cancellationTokenSource.Token);

            // Assert.
            //the data chunks with their parities and their root, and the replicas of the root
            Assert.True(writes.Count(w => w.Chunk is SwarmCac) > 4);
            Assert.Equal(2, writes.Count(w => w.Chunk is SwarmSoc));
            Assert.All(writes, w => Assert.Equal(cancellationTokenSource.Token, w.Token));
        }

        [Theory]
        [InlineData(0, "b34ca8c22b9e982354f9c7f50b470d66db428d880c8a904d5fe4ec9713171526")]         // 0B
        [InlineData(1, "73d7beee42b30991aff05c6f693e6708120aaa513e78e7c0798b5ace1687e4d3")]         // 1B
        [InlineData(31, "3ccd1c9dab870180849f8709b3c0da081d0ddf03606b1091086141e10eb97d63")]        // hashSize -1
        [InlineData(32, "8565c6c2f3f185ad7a1db712f24c267d8abdfbcb8f495a399a00a806d5885f02")]        // hashSize
        [InlineData(33, "3617d23a0b51a5cc6efac60fddb3b34aea1cb9b6775dd9e05b08ca04260b0d70")]        // hashSize +1
        [InlineData(4095, "499adde5b6ddb224a95c4917691a032cc2a3af0b40bef9b03bb23d684c7336b0")]      // maxChunkData -1
        [InlineData(4096, "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac")]      // maxChunkData
        [InlineData(4097, "43163aa5554dc0ba611a5f573c249be9258f50ede812a860f1b6c5245de0781e")]      // maxChunkData +1
        [InlineData(8191, "0716bfe0b1575d710fa1abe814a391cae8f745ab047a6e67ec311e6d3c4062e2")]      // 2*maxChunkData -1
        [InlineData(8192, "72e71b431b78633506acd4198d023ac535f0612dbd9a0aaf6d8b1ba496f4637e")]      // 2*maxChunkData
        [InlineData(8193, "c3641af1c230df638aaf0ec27f3ee8481b5be39d122d68a6fc4acae2d2c0de32")]      // 2*maxChunkData +1
        [InlineData(524287, "1e394e0c4c1883024b3ba72cbc58b603ae3fc0e61108bfdaeefc35e8a15cb228")]    // 128*maxChunkData -1
        [InlineData(524288, "2729f8f847f610297aad7206e627d48550d10c729c8cbb6754e276f5e8598255")]    // 128*maxChunkData
        [InlineData(524289, "7a40111280e319e96b98816bca8258f4f55fd304cb6b7dbc0fb837d5bc223e34")]    // 128*maxChunkData +1
        [InlineData(1048575, "970cff0963f24d525045227779a8592afef35ad6cca4be31fdb4b0ca4ed76cb5")]   // 1MB -1
        [InlineData(1048576, "e529cf3f25310dcda26591ec39e91e355f741ba590c43af6176f596fce245a0e")]   // 1MB
        [InlineData(1048577, "abcb683950626cfffffa1520c10895e5829e26144fa2448f831065d4a612d481")]   // 1MB +1
        public async Task ProduceCorrectPlainHash(int inputDataSize, SwarmHash expectedHash)
        {
            var data = new byte[inputDataSize];
            new Random(0).NextBytes(data);
            var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                new FakeChunkStore(),
                new FakePostageStamper(),
                RedundancyLevel.None,
                false,
                0,
                null);

            var result = await pipeline.HashDataAsync(data);
            
            Assert.Equal(expectedHash, result.Hash);
        }

        [Theory]
        [InlineData(0, RedundancyLevel.Medium, "b34ca8c22b9e982354f9c7f50b470d66db428d880c8a904d5fe4ec9713171526")]         // 0B
        [InlineData(1, RedundancyLevel.Medium, "73d7beee42b30991aff05c6f693e6708120aaa513e78e7c0798b5ace1687e4d3")]         // 1B
        [InlineData(4095, RedundancyLevel.Medium, "499adde5b6ddb224a95c4917691a032cc2a3af0b40bef9b03bb23d684c7336b0")]      // maxChunkData -1
        [InlineData(4096, RedundancyLevel.Medium, "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac")]      // maxChunkData
        [InlineData(4097, RedundancyLevel.Medium, "58516e6f6097013e0b83710da69a0e64bd9e9068b00ac691b1216165edafd67b")]      // maxChunkData +1
        [InlineData(524287, RedundancyLevel.Medium, "97aee0f4a4daa560d5543a21d69e2879b36662ac6c912de31a38cec70c0ecf88")]    // 128*maxChunkData -1
        [InlineData(524288, RedundancyLevel.Medium, "0f63b12d9025eaadb62cba152d6e842a5623dca8098a3f055c5f2780d49d3764")]    // 128*maxChunkData
        [InlineData(524289, RedundancyLevel.Medium, "8445d42250f4d8b9b59196abac87fa953f6725fa1e57bb6457dd776944cd1cb1")]    // 128*maxChunkData +1
        [InlineData(1048576, RedundancyLevel.Medium, "9d79e140507997b2ec5bfbe0ade128e393d9b39d68770f9733bc84e0a9ce7640")]   // 1MB
        [InlineData(0, RedundancyLevel.Strong, "b34ca8c22b9e982354f9c7f50b470d66db428d880c8a904d5fe4ec9713171526")]         // 0B
        [InlineData(1, RedundancyLevel.Strong, "73d7beee42b30991aff05c6f693e6708120aaa513e78e7c0798b5ace1687e4d3")]         // 1B
        [InlineData(4095, RedundancyLevel.Strong, "499adde5b6ddb224a95c4917691a032cc2a3af0b40bef9b03bb23d684c7336b0")]      // maxChunkData -1
        [InlineData(4096, RedundancyLevel.Strong, "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac")]      // maxChunkData
        [InlineData(4097, RedundancyLevel.Strong, "f2066bfd9cb636c28b55b0c701620741d7f33b36b549a75ed60e7a2794fc867f")]      // maxChunkData +1
        [InlineData(524287, RedundancyLevel.Strong, "2894b9498c75213790f4d22babd673cfed4eb31535412ddc832d3ce302140bb4")]    // 128*maxChunkData -1
        [InlineData(524288, RedundancyLevel.Strong, "1f183f3948ccf4b9308e48b1fdbb3920f30a151724296c4b8152165aee4d456c")]    // 128*maxChunkData
        [InlineData(524289, RedundancyLevel.Strong, "e4f8480ab43917254c71d1996dc001d527bf506b79666cfa81aefd9bb5fd5b26")]    // 128*maxChunkData +1
        [InlineData(1048576, RedundancyLevel.Strong, "9cf1327f4e94b6b68cee753a94d2e54942e2f9be5e7c6135f44721a4995a4c4f")]   // 1MB
        [InlineData(0, RedundancyLevel.Insane, "b34ca8c22b9e982354f9c7f50b470d66db428d880c8a904d5fe4ec9713171526")]         // 0B
        [InlineData(1, RedundancyLevel.Insane, "73d7beee42b30991aff05c6f693e6708120aaa513e78e7c0798b5ace1687e4d3")]         // 1B
        [InlineData(4095, RedundancyLevel.Insane, "499adde5b6ddb224a95c4917691a032cc2a3af0b40bef9b03bb23d684c7336b0")]      // maxChunkData -1
        [InlineData(4096, RedundancyLevel.Insane, "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac")]      // maxChunkData
        [InlineData(4097, RedundancyLevel.Insane, "67c99efc517fdf17bf9f24ef2a099ecafa1c7fe4d2d6e38670320ae0c10a6295")]      // maxChunkData +1
        [InlineData(524287, RedundancyLevel.Insane, "57c8337e8af2260cbc5af41ea0436c8fa09e4dbaaeb1a4057c82493c36594303")]    // 128*maxChunkData -1
        [InlineData(524288, RedundancyLevel.Insane, "1bd5abc2342da39c7c79391ddb761416bdd802ae06a8378a8b8cf3f160690de4")]    // 128*maxChunkData
        [InlineData(524289, RedundancyLevel.Insane, "f2ea43a1adff86c8425e1a72e1841acabd8b4f2d03b8336e4a4a531473457f21")]    // 128*maxChunkData +1
        [InlineData(1048576, RedundancyLevel.Insane, "bd64e96421d2b5752e94716a23881c97311f887f58e1cce78e4e0d0b02f856cb")]   // 1MB
        [InlineData(0, RedundancyLevel.Paranoid, "b34ca8c22b9e982354f9c7f50b470d66db428d880c8a904d5fe4ec9713171526")]       // 0B
        [InlineData(1, RedundancyLevel.Paranoid, "73d7beee42b30991aff05c6f693e6708120aaa513e78e7c0798b5ace1687e4d3")]       // 1B
        [InlineData(4095, RedundancyLevel.Paranoid, "499adde5b6ddb224a95c4917691a032cc2a3af0b40bef9b03bb23d684c7336b0")]    // maxChunkData -1
        [InlineData(4096, RedundancyLevel.Paranoid, "ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac")]    // maxChunkData
        [InlineData(4097, RedundancyLevel.Paranoid, "377e2af38f513669c357c63ea8fc7283d71d96f3b5cf20de7552f2d52e8455c9")]    // maxChunkData +1
        [InlineData(524287, RedundancyLevel.Paranoid, "2efde7e159748dc37457e4810da9540e4e29114156b306c8d09fd2f3ae8a91c7")]  // 128*maxChunkData -1
        [InlineData(524288, RedundancyLevel.Paranoid, "a94067ab887fca3c0ddd6c337308bf74f7d029ced597fe206b85f4149abc6d65")]  // 128*maxChunkData
        [InlineData(524289, RedundancyLevel.Paranoid, "ff60fcb938d5d608a7fdd47dc7e8cf719cc6897c03f404ed50a643f732f3b608")]  // 128*maxChunkData +1
        [InlineData(1048576, RedundancyLevel.Paranoid, "b5fc40be9dbc1c646c001fa09e2277150e4d9f1885994ea864f0a00c8f60bf7e")] // 1MB
        public async Task ProduceCorrectHashWithRedundancy(
            int inputDataSize,
            RedundancyLevel redundancyLevel,
            SwarmHash expectedHash)
        {
            var data = new byte[inputDataSize];
            new Random(0).NextBytes(data);
            var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                new FakeChunkStore(),
                new FakePostageStamper(),
                redundancyLevel,
                false,
                0,
                null);

            var result = await pipeline.HashDataAsync(data);
            
            Assert.Equal(expectedHash, result.Hash);
        }

        [Theory, MemberData(nameof(ProduceCorrectRootReplicasWithRedundancyTests))]
        public async Task ProduceCorrectRootReplicasWithRedundancy(ProduceCorrectRootReplicasWithRedundancyTestElement test)
        {
            var data = new byte[test.InputDataSize];
            new Random(0).NextBytes(data);
            var chunkStore = new MemoryChunkStore();
            var pipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                new FakePostageStamper(),
                test.RedundancyLevel,
                false,
                0,
                null);

            var result = await pipeline.HashDataAsync(data);

            var rootChunk = await chunkStore.GetAsync(result.Hash);
            var replicaChunks = chunkStore.AllChunks.Values.OfType<SwarmSoc>().ToArray();
            
            Assert.Equal(test.ExpectedReplicaHashes.Order(), replicaChunks.Select(c => c.Hash).Order());
            foreach (var replicaChunk in replicaChunks)
                Assert.Equal(rootChunk.Hash, replicaChunk.InnerChunk.Hash);
        }
    }
}