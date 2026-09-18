namespace Org.Shirousa.JsonBehavior.Editor
{
    /// <summary>
    /// Unity.Behaviorの.assetへ転写するための、JSON宣言の受け皿（Newtonsoft.Jsonでパースする。
    /// 自己参照する木構造のためJsonUtilityの深さ制限に引っかかるので使わない）。
    /// ノードの座標のような位置情報は持たない——木構造と、既存のAction/Condition資産の
    /// クラス名・パラメータのみで行動を宣言する（README.md 参照）。
    /// </summary>
    public class BehaviorTreeSpec
    {
        public string name;
        public VariableSpec[] blackboard;
        public NodeSpec root;
    }

    /// <summary>Blackboard変数1件分。typeは"GameObject"等の短縮名、またはクラス名。</summary>
    public class VariableSpec
    {
        public string name;
        public string type;
    }

    /// <summary>
    /// 木のノード1件分。typeで意味が変わる：
    /// - "selector"/"sequence": childrenを子として持つ合成ノード
    /// - "guard": conditionsを満たした時だけchildを実行する（ConditionalGuardModifierに転写）
    /// - "action": 既存のAction資産（actionにクラス名）をリーフとして配置する
    /// </summary>
    public class NodeSpec
    {
        public string type;
        public NodeSpec[] children;
        public NodeSpec child;
        public ConditionRefSpec[] conditions;
        public bool requiresAll;
        public string action;
        public FieldSpec[] fields;
    }

    /// <summary>guardノードが参照する既存Condition資産1件分。</summary>
    public class ConditionRefSpec
    {
        public string condition;
        public FieldSpec[] fields;
    }

    /// <summary>
    /// Action/ConditionのBlackboardVariableフィールド1件分の値。
    /// variable/literalのどちらか一方だけを指定する
    /// （variable: Blackboard変数名にリンク、literal: リテラル値を文字列で指定）。
    /// </summary>
    public class FieldSpec
    {
        public string name;
        public string variable;
        public string literal;
    }
}
