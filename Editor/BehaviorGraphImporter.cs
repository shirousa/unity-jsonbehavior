using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using static Org.Shirousa.JsonBehavior.Editor.BehaviorReflection;

namespace Org.Shirousa.JsonBehavior.Editor
{
    /// <summary>
    /// JSON宣言（BehaviorTreeSpec）から、Unity.Behaviorの.assetを転写するツール本体。
    /// GUIは一切使わず、既存のAction/Condition資産（ユーザープロジェクトのカスタムノード、
    /// および Unity.Behavior 組み込みノード）をそのまま参照して木を組み立て、
    /// Unity純正のBuildRuntimeGraph()でコンパイルする（README.md 参照）。
    /// ノードの座標は表示上の都合でしかなく、意味を持たない。
    /// Unity.Behaviorのinternal型にはBehaviorReflection経由でアクセスする
    /// （InternalsVisibleToに依存しないため、どのアセンブリからでも動く）。
    /// </summary>
    public static class BehaviorGraphImporter
    {
        [MenuItem("Assets/Create/Behavior/Import JSON to Behavior Graph", true)]
        static bool ValidateImportSelected() => Selection.activeObject is TextAsset;

        [MenuItem("Assets/Create/Behavior/Import JSON to Behavior Graph")]
        public static void ImportSelected()
        {
            var jsonAsset = (TextAsset)Selection.activeObject;
            string sourcePath = AssetDatabase.GetAssetPath(jsonAsset);
            string outputPath = sourcePath.Replace(".json", ".asset");
            Import(jsonAsset.text, outputPath, sourcePath);
        }

        /// <summary>JSONを.assetへ転写する。戻り値はUnity純正コンパイラが実行用グラフの生成に成功したか。
        /// sourcePathを渡すと、生成元を示すコメントノード（StickyNote）をグラフに残す。
        ///
        /// `forceFreshRuntimeGraph`は既定でfalse——通常の再インポート（パラメータの数値調整等）は
        /// 既存のランタイムグラフ（BehaviorGraph）サブアセットを維持し、`BehaviorGraphAgent.Graph`の
        /// ようなサブアセットを直接指す参照が再インポートを跨いで生き残るようにする
        /// （README.md「既知の癖：再インポート時の参照安定化には『中身の再利用』が必要」参照）。
        /// **ただし、Guardノードの条件（conditions）の数を前回から増減させる場合は`true`を渡すこと**
        /// ——実機で、古いランタイムグラフを不完全に更新し`Conditions`リストにnull要素が残る
        /// 既知の不具合を確認した（結果、Play Mode開始時に`ConditionalGuardModifier.OnStart()`が
        /// NullReferenceExceptionを投げる）。`true`の場合、ランタイムグラフサブアセットを
        /// 破棄してから完全に再構築するため、コンテナ.asset経由ではなくランタイムグラフ
        /// サブアセットを直接指すフィールド（例：`BehaviorGraph`型のカスタムフィールド）は
        /// fileIDが変わり、参照している全Prefab・シーンで再設定が必要になる
        /// （影響範囲を洗い出してから使うこと）。</summary>
        public static bool Import(string json, string outputPath, string sourcePath = null, bool forceFreshRuntimeGraph = false)
        {
            var spec = JsonConvert.DeserializeObject<BehaviorTreeSpec>(json)
                ?? throw new ArgumentException("JSONの解析結果が空です。");

            var graph = LoadOrCreateGraph(outputPath);

            // Blackboard資産を自前で組み立てる（EnsureAssetHasBlackboard()は使わない。
            // 使うと変数を追加する前に内部でRebuildAndSave()が走ってしまうため）。
            var blackboard = ScriptableObject.CreateInstance(BehaviorTypes.BehaviorBlackboardAuthoringAsset);
            var blackboardObject = (UnityEngine.Object)blackboard;
            blackboardObject.name = graph.name + " Blackboard";
            blackboardObject.hideFlags = HideFlags.HideInHierarchy;
            SetField(graph, "Blackboard", blackboard);
            AssetDatabase.AddObjectToAsset(blackboardObject, graph);

            // "Self"はUnity.Behavior側が予約GUIDで扱う特別な変数（実行時にBehaviorGraphAgentが
            // 自身のGameObjectを自動バインドする）。Unity純正のヘルパーで正しいGUIDのまま追加する。
            InvokeStaticMethod(BehaviorTypes.GraphAssetProcessor, "EnsureBlackboardGraphOwnerVariable", blackboard);

            var variables = BuildBlackboardVariables(graph, spec.blackboard);

            // ScriptableObject.CreateInstance(BehaviorAuthoringGraph)の時点で、既定の"On Start"
            // ノードが1つ自動的に追加されている。気づかずに新規CreateNodeすると、2つのStartノードが
            // ParallelAllCompositeの下に並んでしまい、実行木が正しく回らなくなる
            // （Unity.Behavior 1.0.16で確認）。既存のStartノードを再利用する。
            var startNode = FindOrCreateStartNode(graph);
            var cursor = new LayoutCursor();
            var rootNode = BuildNode(graph, spec.root, variables, cursor, depth: 1);
            Connect(graph, startNode, rootNode);

            CreateGeneratedNotice(graph, sourcePath);

            InvokeMethod(blackboard, "RebuildAndSave");
            InvokeMethod(graph, "ValidateAsset");

            if (forceFreshRuntimeGraph)
            {
                ClearRuntimeGraphInPlace(graph);
            }

            // Unity.Behaviorのコンパイラは、新規グラフに対する最初のBuildRuntimeGraph()呼び出しでは
            // 実行用グラフのBlackboard参照を完全には同期しないことがある（Unity.Behavior 1.0.16で確認、
            // 既知の挙動として回避）。SetAssetDirty()を挟んでもう一度呼ぶと正しく同期される。
            InvokeMethod(graph, "BuildRuntimeGraph", true);
            InvokeMethod(blackboard, "SetAssetDirty");
            InvokeMethod(graph, "BuildRuntimeGraph", true);

            InvokeMethod(graph, "SaveAsset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool hasRuntimeGraph = (bool)GetProperty(graph, "HasRuntimeGraph");
            Debug.Log($"[BehaviorGraphImporter] {outputPath} を生成しました。HasRuntimeGraph={hasRuntimeGraph}");
            return hasRuntimeGraph;
        }

        static Dictionary<string, object> BuildBlackboardVariables(object graph, VariableSpec[] specs)
        {
            var blackboard = GetField(graph, "Blackboard");
            var variableList = GetList(blackboard, "Variables");
            var variables = new Dictionary<string, object>();

            foreach (var variableSpec in specs ?? Array.Empty<VariableSpec>())
            {
                // "Self"はEnsureAssetHasBlackboard()が予約GUIDで自動生成し、
                // 実行時にBehaviorGraphAgentが自身のGameObjectを自動バインドする特別な変数。
                // 名前が一致する既存変数があれば再利用し、重複作成を避ける。
                object variable = variableList.Cast<object>()
                    .FirstOrDefault(v => (string)GetField(v, "Name") == variableSpec.name);

                if (variable == null)
                {
                    var variableType = ResolveGeneric("Unity.Behavior.GraphFramework.TypedVariableModel`1", ResolveRuntimeType(variableSpec.type));
                    variable = Construct(variableType);
                    SetField(variable, "Name", variableSpec.name);
                    variableList.Add(variable);
                }

                variables[variableSpec.name] = variable;
            }

            return variables;
        }

        /// <summary>
        /// グラフの評価順序はノードのX座標（左が先）で決まる（Unity.Behaviorの
        /// GraphAssetProcessor.GetSortedConnectionsがPosition.xでソートするため）。
        /// 兄弟ノードを同じXに置くとソートが不安定になり評価順序が保証されない。
        /// そのため、葉（action）ノードを構築するたびにNextXを進め、複合/guardノードは
        /// 自分の最初の子と同じX（=子を構築する前のNextX）を持たせることで、
        /// JSONのchildren配列の宣言順を左から右への座標に正しく反映する。
        /// </summary>
        class LayoutCursor
        {
            public float NextX;
        }

        const float ColumnWidth = 300f;
        const float RowHeight = 200f;

        static object BuildNode(object graph, NodeSpec spec, Dictionary<string, object> variables, LayoutCursor cursor, int depth) => spec.type switch
        {
            "selector" => BuildComposite(graph, BehaviorTypes.SelectorComposite, spec.children, variables, cursor, depth),
            "sequence" => BuildComposite(graph, BehaviorTypes.SequenceComposite, spec.children, variables, cursor, depth),
            "guard" => BuildGuard(graph, spec, variables, cursor, depth),
            "action" => BuildAction(graph, spec, variables, cursor, depth),
            _ => throw new ArgumentException($"未対応のnode typeです: {spec.type}"),
        };

        static object BuildComposite(object graph, Type compositeRuntimeType, NodeSpec[] children, Dictionary<string, object> variables, LayoutCursor cursor, int depth)
        {
            var position = new Vector2(cursor.NextX, depth * RowHeight);
            var node = CreateNode(graph, compositeRuntimeType, position);

            foreach (var childSpec in children ?? Array.Empty<NodeSpec>())
            {
                var childNode = BuildNode(graph, childSpec, variables, cursor, depth + 1);
                Connect(graph, node, childNode);
            }

            return node;
        }

        static object BuildGuard(object graph, NodeSpec spec, Dictionary<string, object> variables, LayoutCursor cursor, int depth)
        {
            var position = new Vector2(cursor.NextX, depth * RowHeight);
            var guardNode = CreateNode(graph, BehaviorTypes.ConditionalGuardModifier, position);
            SetProperty(guardNode, "RequiresAllConditionsTrue", spec.requiresAll);

            var conditionList = GetList(guardNode, "ConditionModels");
            foreach (var conditionSpec in spec.conditions ?? Array.Empty<ConditionRefSpec>())
            {
                var conditionType = ResolveRuntimeType(conditionSpec.condition);
                var conditionInfo = InvokeStaticMethod(BehaviorTypes.ConditionUtility, "GetInfoForConditionType", conditionType);
                var conditionInstance = Construct(conditionType);
                var conditionModel = Construct(BehaviorTypes.ConditionModel, guardNode, conditionInstance, conditionInfo);

                foreach (var fieldSpec in conditionSpec.fields ?? Array.Empty<FieldSpec>())
                {
                    ApplyField(conditionModel, fieldSpec, variables, conditionType);
                }

                conditionList.Add(conditionModel);
            }

            var childNode = BuildNode(graph, spec.child, variables, cursor, depth + 1);
            Connect(graph, guardNode, childNode);
            return guardNode;
        }

        static object BuildAction(object graph, NodeSpec spec, Dictionary<string, object> variables, LayoutCursor cursor, int depth)
        {
            var actionType = ResolveRuntimeType(spec.action);
            var position = new Vector2(cursor.NextX, depth * RowHeight);
            cursor.NextX += ColumnWidth;

            var node = CreateNode(graph, actionType, position);

            foreach (var fieldSpec in spec.fields ?? Array.Empty<FieldSpec>())
            {
                ApplyField(node, fieldSpec, variables, actionType);
            }

            return node;
        }

        /// <summary>
        /// 既存の.assetがあれば、メインオブジェクト（＝GUID）を再利用しつつ中身だけを空にする。
        /// 削除して作り直すとGUIDが変わり、シーン上のBehaviorGraphAgent.Graph参照が
        /// 再インポートのたびに切れてしまうため（README.md「既知の癖」参照）。
        /// </summary>
        static ScriptableObject LoadOrCreateGraph(string outputPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputPath);

            if (existing != null && existing.GetType() == BehaviorTypes.BehaviorAuthoringGraph)
            {
                ClearGraphInPlace(existing, outputPath);
                return (ScriptableObject)existing;
            }

            if (existing != null)
            {
                // 想定外の型が同じパスに存在する場合のみ、安全側に倒して削除・作り直す
                AssetDatabase.DeleteAsset(outputPath);
            }

            var created = (ScriptableObject)ScriptableObject.CreateInstance(BehaviorTypes.BehaviorAuthoringGraph);
            AssetDatabase.CreateAsset(created, outputPath);
            return created;
        }

        /// <summary>
        /// 既存グラフの木構造（Nodes）と旧Blackboardサブアセットを破棄し、まっさらな状態に戻す。
        /// メインアセットオブジェクト自身（GUID）は維持する。ノードモデル自体は
        /// [SerializeReference]でメインオブジェクトに直接埋め込まれており個別のサブアセットには
        /// ならないため、Nodesリストのクリアだけで済む。
        ///
        /// 注意：コンパイル済みランタイムグラフ（BehaviorGraph）・そのDebugInfo・
        /// RuntimeBlackboardAssetは、ここではあえて破棄せずそのまま残す。破棄すると
        /// 直後のBuildRuntimeGraph()がそれらを新規オブジェクトとして作り直してしまい、
        /// サブアセットのfileIDが変わって、シーン上のBehaviorGraphAgent.Graph参照
        /// （このBehaviorGraphサブアセットを直接指している）が再インポートのたびに
        /// 切れてしまう——コンテナ.asset自体のGUIDを維持するだけでは不十分で、
        /// この中身の再利用こそが参照安定化の本体（README.md「既知の癖」参照）。
        /// BuildRuntimeGraph()は既存のBehaviorGraphサブアセットが残っていればそれを
        /// 更新する形で再利用する（実機で確認済み）。
        /// </summary>
        static void ClearGraphInPlace(object graph, string outputPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(outputPath))
            {
                if (asset != null && asset.GetType() == BehaviorTypes.BehaviorBlackboardAuthoringAsset)
                {
                    AssetDatabase.RemoveObjectFromAsset(asset);
                    UnityEngine.Object.DestroyImmediate(asset, true);
                }
            }

            GetList(graph, "Nodes").Clear();
            SetField(graph, "Blackboard", null);
        }

        /// <summary>
        /// 既存のランタイムグラフ（BehaviorGraph）サブアセットをアセットから切り離して破棄し、
        /// `m_RuntimeGraph`をnullに戻す。直後の`BuildRuntimeGraph()`は「既存のサブアセットが
        /// 無い」状態から完全に再構築するため、Guardノードの条件数変更等で古いランタイムグラフの
        /// 内部状態（Conditionsリスト等）が不完全なまま再利用されることがなくなる
        /// （Import()内のコメント参照）。コンテナ.asset自体のGUIDは維持されるため、
        /// `BehaviorGraphAgent.Graph`がコンテナを指している場合の参照は保たれるが、
        /// ランタイムグラフサブアセットを直接指す参照（`BehaviorGraph`型のカスタムフィールド等）は
        /// fileIDが変わるため再設定が必要になる。
        /// </summary>
        static void ClearRuntimeGraphInPlace(object graph)
        {
            var runtimeGraph = GetField(graph, "m_RuntimeGraph") as UnityEngine.Object;
            if (runtimeGraph == null)
            {
                return;
            }

            AssetDatabase.RemoveObjectFromAsset(runtimeGraph);
            UnityEngine.Object.DestroyImmediate(runtimeGraph, true);
            SetField(graph, "m_RuntimeGraph", null);
        }

        static object CreateNode(object graph, Type runtimeType, Vector2 position)
        {
            var info = InvokeStaticMethod(BehaviorTypes.NodeRegistry, "GetInfo", runtimeType)
                ?? throw new ArgumentException($"NodeInfoが見つかりません: {runtimeType}（[NodeDescription]属性の付与を確認してください）");
            var modelType = BehaviorTypes.Unwrap(GetField(info, "ModelType"));
            return InvokeMethod(graph, "CreateNode", modelType, position, null, new object[] { info });
        }

        /// <summary>
        /// 新規BehaviorAuthoringGraphには既定のStartノードが1つ自動的に含まれているため、
        /// それを再利用する。無ければ新規作成する（防御的措置）。
        /// </summary>
        static object FindOrCreateStartNode(object graph)
        {
            var existing = GetList(graph, "Nodes").Cast<object>()
                .FirstOrDefault(n => n.GetType().Name == "StartNodeModel");
            return existing ?? CreateNode(graph, BehaviorTypes.Start, new Vector2(0, 0));
        }

        /// <summary>
        /// StickyNote（コメント専用・ポートを持たずツリーの実行には一切関与しないノード）で、
        /// 自動生成であることと転写元のJSONパスをグラフ上に残す。手で編集しても実行結果には
        /// 影響しないが、直接の変更はJSON側に反映されず次回の転写で失われるため注記する。
        /// </summary>
        static void CreateGeneratedNotice(object graph, string sourcePath)
        {
            string source = string.IsNullOrEmpty(sourcePath) ? "(inline JSON)" : sourcePath;
            string text = "自動生成ファイルです。直接編集しないでください。\n"
                + $"Source: {source}\n"
                + "Generated by Org.Shirousa.JsonBehavior.Editor.BehaviorGraphImporter";

            var position = new Vector2(-400, -200);
            var note = InvokeMethod(graph, "CreateNode", BehaviorTypes.StickyNoteModel, position, null, null);
            SetField(note, "Text", text);
        }

        static void Connect(object graph, object parent, object child)
        {
            InvokeMethodWithOut(parent, "TryDefaultOutputPortModel", out var output);
            InvokeMethodWithOut(child, "TryDefaultInputPortModel", out var input);
            InvokeMethod(graph, "ConnectEdge", output, input);
        }

        /// <summary>node/conditionModelいずれもSetField/GetOrCreateFieldの形が共通なので、リフレクションでまとめて扱える。</summary>
        static void ApplyField(object nodeOrCondition, FieldSpec fieldSpec, Dictionary<string, object> variables, Type ownerType)
        {
            var valueType = GetBlackboardFieldValueType(ownerType, fieldSpec.name);

            if (!string.IsNullOrEmpty(fieldSpec.variable))
            {
                InvokeMethod(nodeOrCondition, "SetField", fieldSpec.name, variables[fieldSpec.variable], valueType);
                return;
            }

            var field = InvokeMethod(nodeOrCondition, "GetOrCreateField", fieldSpec.name, valueType);
            var localValue = GetField(field, "LocalValue");
            SetProperty(localValue, "ObjectValue", ParseLiteral(fieldSpec.literal, valueType));
        }

        static Type GetBlackboardFieldValueType(Type ownerType, string fieldName)
        {
            MemberInfo member = ownerType.GetField(fieldName) ?? (MemberInfo)ownerType.GetProperty(fieldName)
                ?? throw new ArgumentException($"フィールドが見つかりません: {ownerType.Name}.{fieldName}");

            var wrapperType = member switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo property => property.PropertyType,
                _ => throw new ArgumentException($"未対応のメンバーです: {ownerType.Name}.{fieldName}"),
            };

            return wrapperType.GenericTypeArguments.Length > 0 ? wrapperType.GenericTypeArguments[0] : wrapperType;
        }

        static object ParseLiteral(string literal, Type targetType) => targetType switch
        {
            _ when targetType == typeof(float) => float.Parse(literal),
            _ when targetType == typeof(int) => int.Parse(literal),
            _ when targetType == typeof(bool) => bool.Parse(literal),
            _ when targetType == typeof(string) => literal,
            _ when targetType.IsEnum => Enum.Parse(targetType, literal),
            _ => throw new ArgumentException($"リテラル値に未対応の型です: {targetType}"),
        };

        /// <summary>JSONのクラス名（"FollowPlayerAction"等）を、読み込み済みアセンブリ全体から解決する。</summary>
        static Type ResolveRuntimeType(string typeName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.Name == typeName)
            ?? throw new ArgumentException($"型が見つかりません: {typeName}");

        static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException) { return Array.Empty<Type>(); }
        }
    }
}
