using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Org.Shirousa.JsonBehavior.Tests.Editor
{
    /// <summary>
    /// テスト専用のAction/Condition。パッケージはどのプロジェクトの
    /// カスタムノードにも依存しないため、テストも自前のノードで完結させる。
    /// </summary>
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Test Succeed", story: "[Self] test succeeds", category: "Test", id: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public partial class TestSucceedAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<float> Speed;

        protected override Status OnStart() => Status.Success;
    }

    [Serializable, GeneratePropertyBag]
    [Condition(name: "Test Always True", story: "[Self] test always true", category: "Test", id: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public partial class TestAlwaysTrueCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        public override bool IsTrue() => true;
    }
}
