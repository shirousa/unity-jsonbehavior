using System.Linq;
using NUnit.Framework;
using Org.Shirousa.JsonBehavior.Editor;
using UnityEditor;
using UnityEngine;

namespace Org.Shirousa.JsonBehavior.Tests.Editor
{
    /// <summary>
    /// BehaviorGraphImporter のテスト。テスト専用ノード（TestSucceedAction/TestAlwaysTrueCondition）
    /// のみを使い、特定プロジェクトのカスタムノードに依存しない。
    /// </summary>
    public class BehaviorGraphImporterTests
    {
        const string OutputPath = "Assets/_BehaviorGraphImporterTests_generated.asset";

        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(OutputPath) != null)
            {
                AssetDatabase.DeleteAsset(OutputPath);
            }
        }

        [Test]
        public void Actionのみの木でRuntimeGraphが生成される()
        {
            const string json = @"{
                ""blackboard"": [{ ""name"": ""Self"", ""type"": ""GameObject"" }],
                ""root"": {
                    ""type"": ""action"", ""action"": ""TestSucceedAction"",
                    ""fields"": [{ ""name"": ""Self"", ""variable"": ""Self"" }]
                }
            }";

            bool succeeded = BehaviorGraphImporter.Import(json, OutputPath);

            Assert.IsTrue(succeeded);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(OutputPath));
        }

        [Test]
        public void SelectorとGuardとConditionを含む木でRuntimeGraphが生成される()
        {
            const string json = @"{
                ""blackboard"": [{ ""name"": ""Self"", ""type"": ""GameObject"" }],
                ""root"": {
                    ""type"": ""selector"",
                    ""children"": [
                        {
                            ""type"": ""guard"",
                            ""requiresAll"": true,
                            ""conditions"": [
                                { ""condition"": ""TestAlwaysTrueCondition"", ""fields"": [{ ""name"": ""Self"", ""variable"": ""Self"" }] }
                            ],
                            ""child"": {
                                ""type"": ""action"", ""action"": ""TestSucceedAction"",
                                ""fields"": [
                                    { ""name"": ""Self"", ""variable"": ""Self"" },
                                    { ""name"": ""Speed"", ""literal"": ""3.5"" }
                                ]
                            }
                        },
                        {
                            ""type"": ""action"", ""action"": ""TestSucceedAction"",
                            ""fields"": [{ ""name"": ""Self"", ""variable"": ""Self"" }]
                        }
                    ]
                }
            }";

            bool succeeded = BehaviorGraphImporter.Import(json, OutputPath);

            Assert.IsTrue(succeeded);
        }

        [Test]
        public void Self変数は予約済みの変数を再利用し重複させない()
        {
            const string json = @"{
                ""blackboard"": [{ ""name"": ""Self"", ""type"": ""GameObject"" }],
                ""root"": {
                    ""type"": ""action"", ""action"": ""TestSucceedAction"",
                    ""fields"": [{ ""name"": ""Self"", ""variable"": ""Self"" }]
                }
            }";

            BehaviorGraphImporter.Import(json, OutputPath);

            var graph = AssetDatabase.LoadAssetAtPath<ScriptableObject>(OutputPath);
            var blackboard = BehaviorReflection.GetField(graph, "Blackboard");
            var variables = BehaviorReflection.GetList(blackboard, "Variables").Cast<object>().ToList();

            int selfCount = variables.Count(v => (string)BehaviorReflection.GetField(v, "Name") == "Self");
            Assert.AreEqual(1, selfCount);
        }

        [Test]
        public void 再インポートしてもGUIDが変わらない()
        {
            const string json = @"{
                ""blackboard"": [{ ""name"": ""Self"", ""type"": ""GameObject"" }],
                ""root"": {
                    ""type"": ""action"", ""action"": ""TestSucceedAction"",
                    ""fields"": [{ ""name"": ""Self"", ""variable"": ""Self"" }]
                }
            }";

            BehaviorGraphImporter.Import(json, OutputPath);
            string guidBefore = AssetDatabase.AssetPathToGUID(OutputPath);

            BehaviorGraphImporter.Import(json, OutputPath);
            string guidAfter = AssetDatabase.AssetPathToGUID(OutputPath);

            Assert.AreEqual(guidBefore, guidAfter);
        }

        [Test]
        public void 再インポートを繰り返してもサブアセットが増え続けない()
        {
            const string json = @"{
                ""blackboard"": [{ ""name"": ""Self"", ""type"": ""GameObject"" }],
                ""root"": {
                    ""type"": ""action"", ""action"": ""TestSucceedAction"",
                    ""fields"": [{ ""name"": ""Self"", ""variable"": ""Self"" }]
                }
            }";

            // 初回は新規作成、2回目以降は既存アセットの再利用（中身クリア）経路を通り、
            // サブアセット構成が初回とわずかに異なりうる。ここで検証したいのは「再利用経路を
            // 繰り返しても増え続けないか」なので、2回目と3回目（どちらも再利用経路）を比較する。
            BehaviorGraphImporter.Import(json, OutputPath);
            BehaviorGraphImporter.Import(json, OutputPath);
            int countAfterSecond = AssetDatabase.LoadAllAssetsAtPath(OutputPath).Length;

            BehaviorGraphImporter.Import(json, OutputPath);
            int countAfterThird = AssetDatabase.LoadAllAssetsAtPath(OutputPath).Length;

            Assert.AreEqual(countAfterSecond, countAfterThird);
        }

        [Test]
        public void 未知のActionクラス名はわかりやすい例外を投げる()
        {
            const string json = @"{
                ""blackboard"": [],
                ""root"": { ""type"": ""action"", ""action"": ""NoSuchActionClass"" }
            }";

            Assert.Throws<System.ArgumentException>(() => BehaviorGraphImporter.Import(json, OutputPath));
        }
    }
}
