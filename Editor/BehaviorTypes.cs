using System;
using static Org.Shirousa.JsonBehavior.Editor.BehaviorReflection;

namespace Org.Shirousa.JsonBehavior.Editor
{
    /// <summary>Unity.Behaviorの型を、名前解決コストを1回にまとめてキャッシュする。</summary>
    internal static class BehaviorTypes
    {
        internal static readonly Type BehaviorAuthoringGraph = Resolve("Unity.Behavior.BehaviorAuthoringGraph");
        internal static readonly Type BlackboardAsset = Resolve("Unity.Behavior.GraphFramework.BlackboardAsset");
        internal static readonly Type NodeModel = Resolve("Unity.Behavior.GraphFramework.NodeModel");
        internal static readonly Type BehaviorGraphNodeModel = Resolve("Unity.Behavior.BehaviorGraphNodeModel");
        internal static readonly Type ConditionalGuardNodeModel = Resolve("Unity.Behavior.ConditionalGuardNodeModel");
        internal static readonly Type ConditionModel = Resolve("Unity.Behavior.ConditionModel");
        internal static readonly Type PortModel = Resolve("Unity.Behavior.GraphFramework.PortModel");
        internal static readonly Type SerializableType = Resolve("Unity.Behavior.GraphFramework.SerializableType");
        internal static readonly Type NodeRegistry = Resolve("Unity.Behavior.NodeRegistry");
        internal static readonly Type NodeInfo = Resolve("Unity.Behavior.NodeInfo");
        internal static readonly Type ConditionUtility = Resolve("Unity.Behavior.ConditionUtility");
        internal static readonly Type ConditionInfo = Resolve("Unity.Behavior.ConditionInfo");
        internal static readonly Type Condition = Resolve("Unity.Behavior.Condition");
        internal static readonly Type Start = Resolve("Unity.Behavior.Start");
        internal static readonly Type SelectorComposite = Resolve("Unity.Behavior.SelectorComposite");
        internal static readonly Type SequenceComposite = Resolve("Unity.Behavior.SequenceComposite");
        internal static readonly Type ConditionalGuardModifier = Resolve("Unity.Behavior.ConditionalGuardModifier");
        internal static readonly Type BehaviorBlackboardAuthoringAsset = Resolve("Unity.Behavior.BehaviorBlackboardAuthoringAsset");
        internal static readonly Type GraphAssetProcessor = Resolve("Unity.Behavior.GraphAssetProcessor");
        internal static readonly Type StickyNoteModel = Resolve("Unity.Behavior.GraphFramework.StickyNoteModel");

        /// <summary>SerializableType値をSystem.Typeへ展開する（NodeInfo.ModelType等）。</summary>
        internal static Type Unwrap(object serializableType) =>
            (Type)GetProperty(serializableType, "Type");
    }
}
