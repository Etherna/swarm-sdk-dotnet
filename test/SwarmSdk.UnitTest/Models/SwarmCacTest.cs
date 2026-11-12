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
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Etherna.SwarmSdk.Models
{
    public class SwarmCacTest
    {
        // Internal classes.
        public record CanGetIntermediateReferencesFromSpanDataTestElement(
            byte[] SpanData,
            bool EncryptedDataReferences,
            SwarmShardReference[] ExpectedShardReferences);
        
        // Data.
        public static IEnumerable<object[]> CanGetIntermediateReferencesFromSpanDataTest
        {
            get
            {
                var tests = new List<CanGetIntermediateReferencesFromSpanDataTestElement>
                {
                    // Redundancy level None, plain data shards.
                    new(Convert.FromBase64String("ACgAAAAAAADd8Q1YvCn/iqRZbQ1vHHrU3Ja0IsH4h58i+9XLYsY/rEWi+zMBY3580jWn9KJq542moRXYfV+O74QONewdmDO/bhg56kd+r2uKP2+QDMP++e9jivOONR4WzGgVGk/+j+k="),
                        false,
                        [
                            new("ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac", false),
                            new("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf", false),
                            new("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9", false)
                        ]),
                    
                    // Redundancy level Medium, plain data shards.
                    new(Convert.FromBase64String("ACgAAAAAAIHd8Q1YvCn/iqRZbQ1vHHrU3Ja0IsH4h58i+9XLYsY/rEWi+zMBY3580jWn9KJq542moRXYfV+O74QONewdmDO/bhg56kd+r2uKP2+QDMP++e9jivOONR4WzGgVGk/+j+mIvL8VM1ODio4RifrmCIgJOd6sf++BwuoxaeOvqvxQrO2n7PEAownddFmICQrzyybmiREY+x4+qZmGzF/71d0wucMNudgYNVhjl5E+VVVpgHV1mYVpxn96sE+jEtZqr7w="),
                        false,
                        [
                            new("ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac", false),
                            new("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf", false),
                            new("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9", false),
                            new("88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac", true),
                            new("eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30", true),
                            new("b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc", true)
                        ]),
                    
                    // Redundancy level Strong, plain data shards.
                    new(Convert.FromBase64String("ACgAAAAAAILd8Q1YvCn/iqRZbQ1vHHrU3Ja0IsH4h58i+9XLYsY/rEWi+zMBY3580jWn9KJq542moRXYfV+O74QONewdmDO/bhg56kd+r2uKP2+QDMP++e9jivOONR4WzGgVGk/+j+mIvL8VM1ODio4RifrmCIgJOd6sf++BwuoxaeOvqvxQrO2n7PEAownddFmICQrzyybmiREY+x4+qZmGzF/71d0wucMNudgYNVhjl5E+VVVpgHV1mYVpxn96sE+jEtZqr7wXdSX+OARG4tFEmJVrk8qMAQxiXb3G/Qa8ES5c54l6MWejF3yKJFAanjIWjy9BHdB5TFhgtA/mj9Is3eQGZQX7"),
                        false,
                        [
                            new("ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac", false),
                            new("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf", false),
                            new("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9", false),
                            new("88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac", true),
                            new("eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30", true),
                            new("b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc", true),
                            new("177525fe380446e2d14498956b93ca8c010c625dbdc6fd06bc112e5ce7897a31", true),
                            new("67a3177c8a24501a9e32168f2f411dd0794c5860b40fe68fd22cdde4066505fb", true)
                        ]),
                    
                    // Redundancy level Insane, plain data shards.
                    new(Convert.FromBase64String("ACgAAAAAAIPd8Q1YvCn/iqRZbQ1vHHrU3Ja0IsH4h58i+9XLYsY/rEWi+zMBY3580jWn9KJq542moRXYfV+O74QONewdmDO/bhg56kd+r2uKP2+QDMP++e9jivOONR4WzGgVGk/+j+mIvL8VM1ODio4RifrmCIgJOd6sf++BwuoxaeOvqvxQrO2n7PEAownddFmICQrzyybmiREY+x4+qZmGzF/71d0wucMNudgYNVhjl5E+VVVpgHV1mYVpxn96sE+jEtZqr7wXdSX+OARG4tFEmJVrk8qMAQxiXb3G/Qa8ES5c54l6MWejF3yKJFAanjIWjy9BHdB5TFhgtA/mj9Is3eQGZQX7V0t21CQZnjxZotphCaCuABz74rXyIKbGJWPw5glMLqOrAr06lnBIwwLBFuFrXN+1iOnGVr1Bivk+Q+S9WJlXIQ=="),
                        false,
                        [
                            new("ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac", false),
                            new("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf", false),
                            new("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9", false),
                            new("88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac", true),
                            new("eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30", true),
                            new("b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc", true),
                            new("177525fe380446e2d14498956b93ca8c010c625dbdc6fd06bc112e5ce7897a31", true),
                            new("67a3177c8a24501a9e32168f2f411dd0794c5860b40fe68fd22cdde4066505fb", true),
                            new("574b76d424199e3c59a2da6109a0ae001cfbe2b5f220a6c62563f0e6094c2ea3", true),
                            new("ab02bd3a967048c302c116e16b5cdfb588e9c656bd418af93e43e4bd58995721", true)
                        ]),
                    
                    // Redundancy level Paranoid, plain data shards.
                    new(Convert.FromBase64String("ACgAAAAAAITd8Q1YvCn/iqRZbQ1vHHrU3Ja0IsH4h58i+9XLYsY/rEWi+zMBY3580jWn9KJq542moRXYfV+O74QONewdmDO/bhg56kd+r2uKP2+QDMP++e9jivOONR4WzGgVGk/+j+mIvL8VM1ODio4RifrmCIgJOd6sf++BwuoxaeOvqvxQrO2n7PEAownddFmICQrzyybmiREY+x4+qZmGzF/71d0wucMNudgYNVhjl5E+VVVpgHV1mYVpxn96sE+jEtZqr7wXdSX+OARG4tFEmJVrk8qMAQxiXb3G/Qa8ES5c54l6MWejF3yKJFAanjIWjy9BHdB5TFhgtA/mj9Is3eQGZQX7V0t21CQZnjxZotphCaCuABz74rXyIKbGJWPw5glMLqOrAr06lnBIwwLBFuFrXN+1iOnGVr1Bivk+Q+S9WJlXIVpsbAV6eP3P+d2hcNaoJwocdpQS9sHpUKBhBZdL7vkst2/cA1PkGWc7fb1k8o/dFRSPBFGR0C8sOf5NatZGQXvr1nMIgjOprrMuYEEaEkptVBUWP5P+QxK8fzGAYeO5PLoDvhXwQmBcu0bkCEWI6kIXqaTv5GraAZWB+mX1VorlSD1upUqZrRcFvM0BA/l3G3a8+J9h3ZpwtPKE8AiS+RoIBYANuvbpI9ZiWXayYh11SgpGMMpWl19av2Qt71YTbj6Yum7PnYeRb8mZiI7iczwtOgbixX5MYw3iip+7nWKfLiIZPTYbEg2frwLkggKrhNys1mQYDbRuM5nYzZfsJssmJ01WJZ8VtKhnLJGHWQEELAHFOfI6sek8Eu0L9buQkLT6/ZodYwoSZ/uo+RCHj3BKMXQbIKkDmG5d5X8ihH86LBQSmzDQBZlHreguym8CMCH6jbeB4YRaQmPSVZySFifEYjnAE+cbOWwPVIxglIGOHBGqX9/aC/TLeex0QFI/8vBc9H794ZadAnMYjUfV8Ew/YUxog5K4rYC79nNV+JRrUCEav/wFaAbTWoBGXwhYryTkKlEiZJHd2SKcSeWavBFL/JvNcruUJCfbOogksg0Wgp5BchZj9Zeq9S8wCBBDdaZoF+P1xp0KkmMWXrIfaEiCcUKevlPmYBLyt9bKe8mXawP/3AVLbtuv4dcdNZyiiySM7jhSnFd2lDIENwmgQngwALrVMD7YEC/op/EUkWTZ3Mwo3hbQnFvK7jKeONrtFll93mLzP8Pj7SV0U0mhnwZPc2sC6MKaLuZ3wbDb9A1E"),
                        false,
                        [
                            new("ddf10d58bc29ff8aa4596d0d6f1c7ad4dc96b422c1f8879f22fbd5cb62c63fac", false),
                            new("45a2fb3301637e7cd235a7f4a26ae78da6a115d87d5f8eef840e35ec1d9833bf", false),
                            new("6e1839ea477eaf6b8a3f6f900cc3fef9ef638af38e351e16cc68151a4ffe8fe9", false),
                            new("88bcbf153353838a8e1189fae608880939deac7fef81c2ea3169e3afaafc50ac", true),
                            new("eda7ecf100a309dd745988090af3cb26e6891118fb1e3ea99986cc5ffbd5dd30", true),
                            new("b9c30db9d81835586397913e555569807575998569c67f7ab04fa312d66aafbc", true),
                            new("177525fe380446e2d14498956b93ca8c010c625dbdc6fd06bc112e5ce7897a31", true),
                            new("67a3177c8a24501a9e32168f2f411dd0794c5860b40fe68fd22cdde4066505fb", true),
                            new("574b76d424199e3c59a2da6109a0ae001cfbe2b5f220a6c62563f0e6094c2ea3", true),
                            new("ab02bd3a967048c302c116e16b5cdfb588e9c656bd418af93e43e4bd58995721", true),
                            new("5a6c6c057a78fdcff9dda170d6a8270a1c769412f6c1e950a06105974beef92c", true),
                            new("b76fdc0353e419673b7dbd64f28fdd15148f045191d02f2c39fe4d6ad646417b", true),
                            new("ebd673088233a9aeb32e60411a124a6d5415163f93fe4312bc7f318061e3b93c", true),
                            new("ba03be15f042605cbb46e4084588ea4217a9a4efe46ada019581fa65f5568ae5", true),
                            new("483d6ea54a99ad1705bccd0103f9771b76bcf89f61dd9a70b4f284f00892f91a", true),
                            new("0805800dbaf6e923d6625976b2621d754a0a4630ca56975f5abf642def56136e", true),
                            new("3e98ba6ecf9d87916fc999888ee2733c2d3a06e2c57e4c630de28a9fbb9d629f", true),
                            new("2e22193d361b120d9faf02e48202ab84dcacd664180db46e3399d8cd97ec26cb", true),
                            new("26274d56259f15b4a8672c91875901042c01c539f23ab1e93c12ed0bf5bb9090", true),
                            new("b4fafd9a1d630a1267fba8f910878f704a31741b20a903986e5de57f22847f3a", true),
                            new("2c14129b30d0059947ade82eca6f023021fa8db781e1845a4263d2559c921627", true),
                            new("c46239c013e71b396c0f548c6094818e1c11aa5fdfda0bf4cb79ec7440523ff2", true),
                            new("f05cf47efde1969d0273188d47d5f04c3f614c688392b8ad80bbf67355f8946b", true),
                            new("50211abffc056806d35a80465f0858af24e42a51226491ddd9229c49e59abc11", true),
                            new("4bfc9bcd72bb942427db3a8824b20d16829e41721663f597aaf52f3008104375", true),
                            new("a66817e3f5c69d0a9263165eb21f68488271429ebe53e66012f2b7d6ca7bc997", true),
                            new("6b03ffdc054b6edbafe1d71d359ca28b248cee38529c57769432043709a04278", true),
                            new("3000bad5303ed8102fe8a7f1149164d9dccc28de16d09c5bcaee329e38daed16", true),
                            new("597dde62f33fc3e3ed25745349a19f064f736b02e8c29a2ee677c1b0dbf40d44", true)
                        ]),
                    
                    // Redundancy level None, encrypted data shards.
                    new(Convert.FromBase64String("ACgAAAAAAAD2hUvSbpo90kPjYy9fEapP2yiDIOdSBrtChwmJCVQdYPS0RfFG8s5Y/u9xZheyRaZXvgtCEj42AnG/ox0fIDCughovj+TBOl96eLrylk+J6U5hWc7dKA7vc7mXzH74SGXnSVYIJtK9oq3nypUfZGzHjY6/Y6CfxmqQ98lTICeb8VuzvpPwpyvcsGnaRUTWPF6H6YMDQ9WpUp+cGlbOzRdIk8jTrnwBJ7xVNh3O9PULLkRHQgdRT3vI/wpHXBT6YR4="),
                        true,
                        [
                            new("f6854bd26e9a3dd243e3632f5f11aa4fdb288320e75206bb4287098909541d60f4b445f146f2ce58feef716617b245a657be0b42123e360271bfa31d1f2030ae", false),
                            new("821a2f8fe4c13a5f7a78baf2964f89e94e6159cedd280eef73b997cc7ef84865e749560826d2bda2ade7ca951f646cc78d8ebf63a09fc66a90f7c95320279bf1", false),
                            new("5bb3be93f0a72bdcb069da4544d63c5e87e9830343d5a9529f9c1a56cecd174893c8d3ae7c0127bc55361dcef4f50b2e44474207514f7bc8ff0a475c14fa611e", false)
                        ]),
                    
                    // Redundancy level Medium, encrypted data shards.
                    new(Convert.FromBase64String("ACgAAAAAAIHCX+jr/ldB1nrx5JCZznyxG8uww0XBq+xuKeVGoVoBFXFtDUfEDtWlhs/eBZgJwgACvddXv00WEHaXZnDWoAqHQNzYO0EHpgu8L5gVqA9s+AABcX3F+ZT4inLK/tXE2ERQnBjVBiljm0ElbYX8kSco+rHuJQSV/i1VignznrcbteuUNY07jNzvWKvqr7TOHeAulArw0fQt505jhSkSzGAhpVs68o4NF8Mt7KcmHNnlG2DH01eKtT5mSu6veK35XKPXPx9sfLMNOTaXxx3tA0Cheq6enJA5MWSWDMp415bE7EobqaAzeIZ1eid73wOgucg5wBSkd1y4jZwD609fBrSgvtWayqvTSOLd3TGAd6N0d+2i4H3k0ghtkJ1gqXLHIMF7jtPns9OT+OtQ8SsZ8d5lniIItnBbGRIGI23Xf7ZZyw=="),
                        true,
                        [
                            new("c25fe8ebfe5741d67af1e49099ce7cb11bcbb0c345c1abec6e29e546a15a0115716d0d47c40ed5a586cfde059809c20002bdd757bf4d161076976670d6a00a87", false),
                            new("40dcd83b4107a60bbc2f9815a80f6cf80001717dc5f994f88a72cafed5c4d844509c18d50629639b41256d85fc912728fab1ee250495fe2d558a09f39eb71bb5", false),
                            new("eb94358d3b8cdcef58abeaafb4ce1de02e940af0d1f42de74e63852912cc6021a55b3af28e0d17c32deca7261cd9e51b60c7d3578ab53e664aeeaf78adf95ca3", false),
                            new("d73f1f6c7cb30d393697c71ded0340a17aae9e9c90393164960cca78d796c4ec", true),
                            new("4a1ba9a0337886757a277bdf03a0b9c839c014a4775cb88d9c03eb4f5f06b4a0", true),
                            new("bed59acaabd348e2dddd318077a37477eda2e07de4d2086d909d60a972c720c1", true),
                            new("7b8ed3e7b3d393f8eb50f12b19f1de659e2208b6705b191206236dd77fb659cb", true)
                        ]),
                    
                    // Redundancy level Strong, encrypted data shards.
                    new(Convert.FromBase64String("ACgAAAAAAILijjpwpxbflP8vE04VjTU8XXlIQj7QJZXiZXFWQKasLOrd4ejRzKDWDTve6KRLewnNdKsrAErujanZNcbemlA3ga19IoKrypj0QZQ/C88L2DulTR7hlIgHq8PrTec6q06qcM9g2+8N+7O0WziRxeCZz2hsjKNfadalZFCkXfwvqo/TIMUuDZa/aRdo5ycFSzfzYxWe0N4m0KGtdDplCdzoDa48yp+hES9JUV7n5Hb/9fdaTG7eFRrrDxOFOA1RkdP0aLHivuALFZCOAY76Al/bd8gUYX9ZIHmZ43R1pgqA5zvibzFuGw1Q2t5MmSkBfx5p2Au81dIaFHf2k7vT5gSeVcZPQZSK+rqBGyR3v30CVLeREhuR2veKFIzVdNmADMdhxMD2ujOZH/NxBqxdxCVY6ttAo7PU2aFG8qUU8sbjSu2SyNFZWL99uJVGRvd2hZZkmBa0/qM7Fy6551tF3ziqEBCZBgObgYqqOJ1QeKPVDyQBcdjbQTxgx/TGXWnse+2LyT8cc1mNGF41nzGgMbLup5SPqxow/95+fdSc+j+0VA=="),
                        true,
                        [
                            new("e28e3a70a716df94ff2f134e158d353c5d7948423ed02595e265715640a6ac2ceadde1e8d1cca0d60d3bdee8a44b7b09cd74ab2b004aee8da9d935c6de9a5037", false),
                            new("81ad7d2282abca98f441943f0bcf0bd83ba54d1ee1948807abc3eb4de73aab4eaa70cf60dbef0dfbb3b45b3891c5e099cf686c8ca35f69d6a56450a45dfc2faa", false),
                            new("8fd320c52e0d96bf691768e727054b37f363159ed0de26d0a1ad743a6509dce80dae3cca9fa1112f49515ee7e476fff5f75a4c6ede151aeb0f1385380d5191d3", false),
                            new("f468b1e2bee00b15908e018efa025fdb77c814617f59207999e37475a60a80e7", true),
                            new("3be26f316e1b0d50dade4c9929017f1e69d80bbcd5d21a1477f693bbd3e6049e", true),
                            new("55c64f41948afaba811b2477bf7d0254b791121b91daf78a148cd574d9800cc7", true),
                            new("61c4c0f6ba33991ff37106ac5dc42558eadb40a3b3d4d9a146f2a514f2c6e34a", true),
                            new("ed92c8d15958bf7db8954646f7768596649816b4fea33b172eb9e75b45df38aa", true),
                            new("10109906039b818aaa389d5078a3d50f240171d8db413c60c7f4c65d69ec7bed", true),
                            new("8bc93f1c73598d185e359f31a031b2eea7948fab1a30ffde7e7dd49cfa3fb454", true)
                        ]),
                    
                    // Redundancy level Insane, encrypted data shards.
                    new(Convert.FromBase64String("ACgAAAAAAINVmmK/daTHnvPEuu4M1O9ZgmIh0S3hLswTbXCSaezBBg1m0ZrhbwTAJpLmr1BSz04Zh6YST1hTkPXMXUKtkuXs5D7StBG/l2GPXODiha/bafudH/82TS4aY7PDe4I+xNWdMjTngT7aGHEnTZZ5OP30zGQBxMT9mdK1iaWSw5GahAPlyINGYsoHBC87ofq0+s8xKrc88x0DcERdFkpBD9YErbANxhMGxa3OtHuCla1gdMUNOeYROYT7OB/3Iu1Y9+Q9OhzK2eoFjx/Yyw+8sKuyaXxibAiMz7Jh23Zaj0Gj+oaXY2xzNGadB5o78DN+rmiuYj8phnKTKiQ4xYbmr6BSUtELSbF9zVI1CIeNO2tGoPPKKbfYCWru256qBgZFkMcVlbnYaVKrEujlXO/2AMsEu9ZsW5xZZBiI98c3MkhZTF2BTOCkFGkcIjRFqgkb1002Qj1L/AOv1MYwiDyWyTfBBmON/0wiQECQy2waBAR483hsnAJ4KkSqfr9X/Jgy9u/Yh89uBLB2xm6I4xIF/pKK8DSpz8gQUNURkfAQcPQBNmgwjjq2fqwSdmeMOvyRdexbymcdG47e5i86j1v7+y36FeUD9dF2LUCZ+V0otYbkhs1OmlpUPuTZFOVJ1qpRf8o="),
                        true,
                        [
                            new("559a62bf75a4c79ef3c4baee0cd4ef59826221d12de12ecc136d709269ecc1060d66d19ae16f04c02692e6af5052cf4e1987a6124f585390f5cc5d42ad92e5ec", false),
                            new("e43ed2b411bf97618f5ce0e285afdb69fb9d1fff364d2e1a63b3c37b823ec4d59d3234e7813eda1871274d967938fdf4cc6401c4c4fd99d2b589a592c3919a84", false),
                            new("03e5c8834662ca07042f3ba1fab4facf312ab73cf31d0370445d164a410fd604adb00dc61306c5adceb47b8295ad6074c50d39e6113984fb381ff722ed58f7e4", false),
                            new("3d3a1ccad9ea058f1fd8cb0fbcb0abb2697c626c088ccfb261db765a8f41a3fa", true),
                            new("8697636c7334669d079a3bf0337eae68ae623f298672932a2438c586e6afa052", true),
                            new("52d10b49b17dcd523508878d3b6b46a0f3ca29b7d8096aeedb9eaa06064590c7", true),
                            new("1595b9d86952ab12e8e55ceff600cb04bbd66c5b9c59641888f7c7373248594c", true),
                            new("5d814ce0a414691c223445aa091bd74d36423d4bfc03afd4c630883c96c937c1", true),
                            new("06638dff4c22404090cb6c1a040478f3786c9c02782a44aa7ebf57fc9832f6ef", true),
                            new("d887cf6e04b076c66e88e31205fe928af034a9cfc81050d51191f01070f40136", true),
                            new("68308e3ab67eac1276678c3afc9175ec5bca671d1b8edee62f3a8f5bfbfb2dfa", true),
                            new("15e503f5d1762d4099f95d28b586e486cd4e9a5a543ee4d914e549d6aa517fca", true)
                        ]),
                    
                    // Redundancy level Paranoid, encrypted data shards.
                    new(Convert.FromBase64String("ACgAAAAAAITc+tI3XV9oFkcT1PKQorF/tApWn7ngEwJ/njSWRdDMX+o6iEtc5wg02Xe5aXJq7PBkxP6VrnouqmCe0/cRNuUq1twk6pLJBID/7w1MiC9wPF0K7iLkXQ7EL4H/hwzSS3SCoxxIkNZTEqOcJr/hIEFfkhqfMDR0sIRju5WbrVrDy/Itq0MkfVSAOwZiGXnPFaEJqoBLMHwMM5vsK19QT/rz3F8SYfnpPhiEVqKyzCD9mWOqpn/yz5/BxFa/TmEj1IKnmvM6Sx2kPJhYm4wcXq1HjUAuRquN+uGSOYynmtjP83kWPr0tTuB1/hvAMAIpqsMm3qpMsn8UHIUC9E+OcVnjPwtLbuiN8uweX/lFtmvku0wshkme1JCCDyPABnVBl6eeqMFGGKIHvChAg5e5Wi8fDRgp2QGAPf4kQLwRn8X25iWXir/H5yyEHu90TI28TdYJ2U/B8enx1ZHojD4PSpieDc+n2dQfew6s0T6YkWsENsnDLbnAbuTWogH4HL4aqeHc3JwKJCJKWC8ebHY3r7vE/P5iTJsyBNBanw5Ue+SuC9QXswWH/QGKUBV79TR1bhB2DqS0Kh6mdV12lWNxaYRVwKwgM/29OMB0vM42FhzXT59vL83fmowvmnHpJRqtTBLBWZZ1iAEvW0gJHJEzINdxFBS7tPOxeqOJezCa8ONXHhULWo+DZrQAz5QcFbed8wap5dKnOgQyX8gRGcTNZEDcxh7d334Hsj0lGXsXRMAe4tQZu9xfPEgSH3qrN2IX8maD/omCA5gnRPMFdpW0avlsw6EL+fdx+R9mMlH2mrNj0Feq/VBKw8kuJcTqn9Kwm9PTflaF9JTdT3EyBhYQV2Wi6//uesyu5w2XSvqr8m1swQihqrv7rcuia0NbP65V+TqpTvbLBlFcP29UXl9aTPSaREvhUpiYD1znMb0eJ4/SRY5gcrmhZBjDx6A7cvhneAfdRgIMfGLq47r7nPzy+10DTVL62Wk5vYpkmQn2f550PfieiYcXYojhqH+OqklzztxhnZZtx8ckuHoLiXaf8Gzj0UyAyZeGRAZLGdBGdNbDfbh5V9OntRvh8nLRJG/8oQysByx87PV6yfN/dbU9spI/0Wp9k/Axr0QyF6r7EbviQGNftwyU9Sl+stUhl5zv2jzjSM4wIlHOnC5yynpcCeVSuhNuax3DouoWVaqLNP0cohLA3oYXgf0Yj4PLScCkJFW2BI1/ziiTs/GFQZN1K2cXQ1JOP7xugGqcBxR1VQ1hPjpGdTvvUiKn9w5j6GOXoa65XPL+4GXgVykJiBrdBorizvzr/FIwmObUwxnsvqAckmCiGUg2uPSUDalLxvDUe3tKrW9AH21NfiktvqZ0CqFn8WsyFfIYDnmDmCLdVxkWgbuV6k51SEyTqy23BR8/rls37SvlCdP3X3FtiYtYrro/JPNdIaZoaE8PK4IaCmV9FFTJ7E+4PnUCEYsfKrtRmNBvja+QKN0sZcDOA1whJH1uJj+pETgkfPOnTlFeU5iyk9ddpRCVuuDMLhAQMENo4itstPUAfv8aJnkApGh9ud8WHfbQ6oEm6w1YXek9Pig1pJ/kSJtkVDvnQh/9m16joZKiHoHayVFOOPDdZ5ziufDeHyGXbSPAVff1LwwtOjsQYmg+GDmFFPN/PSB5mNjQ/fkzH0ijo5uWbvMSW7JVh/WD80U2dTsCJLhoMX4mchhyqA=="),
                        true,
                        [
                            new("dcfad2375d5f68164713d4f290a2b17fb40a569fb9e013027f9e349645d0cc5fea3a884b5ce70834d977b969726aecf064c4fe95ae7a2eaa609ed3f71136e52a", false),
                            new("d6dc24ea92c90480ffef0d4c882f703c5d0aee22e45d0ec42f81ff870cd24b7482a31c4890d65312a39c26bfe120415f921a9f303474b08463bb959bad5ac3cb", false),
                            new("f22dab43247d54803b06621979cf15a109aa804b307c0c339bec2b5f504ffaf3dc5f1261f9e93e188456a2b2cc20fd9963aaa67ff2cf9fc1c456bf4e6123d482", false),
                            new("a79af33a4b1da43c98589b8c1c5ead478d402e46ab8dfae192398ca79ad8cff3", true),
                            new("79163ebd2d4ee075fe1bc0300229aac326deaa4cb27f141c8502f44f8e7159e3", true),
                            new("3f0b4b6ee88df2ec1e5ff945b66be4bb4c2c86499ed490820f23c006754197a7", true),
                            new("9ea8c14618a207bc28408397b95a2f1f0d1829d901803dfe2440bc119fc5f6e6", true),
                            new("25978abfc7e72c841eef744c8dbc4dd609d94fc1f1e9f1d591e88c3e0f4a989e", true),
                            new("0dcfa7d9d41f7b0eacd13e98916b0436c9c32db9c06ee4d6a201f81cbe1aa9e1", true),
                            new("dcdc9c0a24224a582f1e6c7637afbbc4fcfe624c9b3204d05a9f0e547be4ae0b", true),
                            new("d417b30587fd018a50157bf534756e10760ea4b42a1ea6755d76956371698455", true),
                            new("c0ac2033fdbd38c074bcce36161cd74f9f6f2fcddf9a8c2f9a71e9251aad4c12", true),
                            new("c159967588012f5b48091c913320d7711414bbb4f3b17aa3897b309af0e3571e", true),
                            new("150b5a8f8366b400cf941c15b79df306a9e5d2a73a04325fc81119c4cd6440dc", true),
                            new("c61edddf7e07b23d25197b1744c01ee2d419bbdc5f3c48121f7aab376217f266", true),
                            new("83fe898203982744f3057695b46af96cc3a10bf9f771f91f663251f69ab363d0", true),
                            new("57aafd504ac3c92e25c4ea9fd2b09bd3d37e5685f494dd4f71320616105765a2", true),
                            new("ebffee7accaee70d974afaabf26d6cc108a1aabbfbadcba26b435b3fae55f93a", true),
                            new("a94ef6cb06515c3f6f545e5f5a4cf49a444be15298980f5ce731bd1e278fd245", true),
                            new("8e6072b9a16418c3c7a03b72f8677807dd46020c7c62eae3bafb9cfcf2fb5d03", true),
                            new("4d52fad96939bd8a649909f67f9e743df89e8987176288e1a87f8eaa4973cedc", true),
                            new("619d966dc7c724b87a0b89769ff06ce3d14c80c9978644064b19d04674d6c37d", true),
                            new("b87957d3a7b51be1f272d1246ffca10cac072c7cecf57ac9f37f75b53db2923f", true),
                            new("d16a7d93f031af443217aafb11bbe240635fb70c94f5297eb2d521979cefda3c", true),
                            new("e348ce302251ce9c2e72ca7a5c09e552ba136e6b1dc3a2ea1655aa8b34fd1ca2", true),
                            new("12c0de861781fd188f83cb49c0a42455b6048d7fce2893b3f1854193752b6717", true),
                            new("43524e3fbc6e806a9c071475550d613e3a46753bef5222a7f70e63e86397a1ae", true),
                            new("b95cf2fee065e0572909881add068ae2cefcebfc523098e6d4c319ecbea01c92", true),
                            new("60a2194836b8f4940da94bc6f0d47b7b4aad6f401f6d4d7e292dbea6740aa167", true),
                            new("f16b3215f2180e79839822dd57191681bb95ea4e75484c93ab2db7051f3fae5b", true),
                            new("37ed2be509d3f75f716d898b58aeba3f24f35d21a668684f0f2b821a0a657d14", true),
                            new("54c9ec4fb83e7502118b1f2abb5198d06f8daf9028dd2c65c0ce035c21247d6e", true),
                            new("263fa91138247cf3a74e515e5398b293d75da51095bae0cc2e1010304368e22b", true),
                            new("6cb4f5007eff1a267900a4687db9df161df6d0ea8126eb0d585de93d3e2835a4", true),
                            new("9fe4489b64543be7421ffd9b5ea3a192a21e81dac9514e38f0dd679ce2b9f0de", true),
                            new("1f21976d23c055f7f52f0c2d3a3b1062683e18398514f37f3d207998d8d0fdf9", true),
                            new("331f48a3a39b966ef3125bb25587f583f34536753b0224b868317e26721872a8", true)
                        ])
                };

                return tests.Select(t => new object[] { t });
            }
        }
        
        // Tests.
        [Theory, MemberData(nameof(CanGetIntermediateReferencesFromSpanDataTest))]
        public void CanGetIntermediateReferencesFromSpanData(CanGetIntermediateReferencesFromSpanDataTestElement test)
        {
            var references = SwarmCac.GetIntermediateReferencesFromSpanData(
                test.SpanData[..SwarmCac.SpanSize],
                test.SpanData[SwarmCac.SpanSize..],
                test.EncryptedDataReferences);
            Assert.Equal(test.ExpectedShardReferences, references);
        }

        [Theory]
        [InlineData(4097UL, RedundancyLevel.None, false, new ulong[] { 4096, 1 })]
        [InlineData(3 * 4096UL, RedundancyLevel.None, false, new ulong[] { 4096, 4096, 4096 })]
        [InlineData(128 * 4096UL + 1, RedundancyLevel.None, false, new ulong[] { 128 * 4096UL, 1 })]
        [InlineData(2 * 4096UL + 5, RedundancyLevel.Medium, true, new ulong[] { 4096, 4096, 5 })]
        public void GetDataReferenceSpanLengthsGivesAFullSectionToEveryReferenceButTheLast(
            ulong spanLength, RedundancyLevel redundancyLevel, bool isEncrypted, ulong[] expectedSpanLengths)
        {
            var spanLengths = SwarmCac.GetDataReferenceSpanLengths(spanLength, redundancyLevel, isEncrypted);

            Assert.Equal(expectedSpanLengths, spanLengths);
        }
    }
}