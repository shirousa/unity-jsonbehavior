[English](README.md) | 日本語

# JSON Behavior Import for Unity Behavior

Unity Behavior（`com.unity.behavior`）のグラフを、**JSONで宣言して、そのままネイティブな`.asset`に転写する**Editor拡張。

## これは何のためにあるか

Unity Behaviorは強力だが、著者フォーマットが「ビジュアルグラフ」であることが前提になっている。
これは人間がGUIで組む分には良いが、コード生成・自動化・LLM/コーディングエージェントとの相性が悪い：

- ノードの座標や配線という**位置情報**は、生成・レビュー・差分管理に向かない
- グラフ資産をプログラムから直接編集する公式APIは提供されていない
- Gitでのレビューは「ノードグラフの見た目の差分」になり、テキストとして読めない

このパッケージは、**木構造をJSONとして宣言し**、Unity Behaviorが標準で持つノード資産
（あなたが書いたカスタムAction/Condition、およびUnity公式の45種の組み込みノード）を
**一切改変せずに**組み合わせて、Unity純正のグラフコンパイラでビルドする。

- JSONはLLM/コーディングエージェントが生成・検証・差分レビューするのに向いている
- ノードの実行ロジック（Action/Condition）は自作しない——既存のBehavior Agentエコシステムに乗る
- 出力物は完全にネイティブな`.asset`。`BehaviorGraphAgent`がそのまま実行し、
  Unity純正のビジュアルデバッガ（ブレークポイント、実行中ノードのハイライト）もそのまま使える

「ノードの中身を自作しない・グラフのコンパイラも自作しない・GUIの位置情報にも依存しない」
という3点を同時に満たす、既存の位置づけの製品には無かった立ち位置を狙っている。

## 必要環境

- Unity 6000.6以降
- `com.unity.behavior` 1.0.16（依存関係として自動的に解決される）
- `com.unity.nuget.newtonsoft-json`（同上。木構造は自己参照するため`JsonUtility`の
  シリアライズ深度制限に引っかかる。Newtonsoft.Jsonを使う理由はこれ）

## クイックスタート

1. 行動を宣言するJSONファイルを`.json`のTextAssetとしてプロジェクトに置く（下記スキーマ参照）
2. Project WindowでそのJSONを選択し、右クリック →
   `Create > Behavior > Import JSON to Behavior Graph`
3. 同じ場所に同名の`.asset`（Behavior Graph資産）が生成される
4. 生成された`.asset`を`BehaviorGraphAgent`にドラッグ＆ドロップして使う（通常のBehavior Graphと同じ）

コードから直接呼び出す場合：

```csharp
using Org.Shirousa.JsonBehavior.Editor;

bool succeeded = BehaviorGraphImporter.Import(jsonText, "Assets/MyBehaviors/enemy.asset");
```

サンプルは`Samples~/PetAI/pet_default.json`を参照（Package Managerの当パッケージ詳細画面から
「Samples」としてインポートできる）。

## JSONスキーマ

```json
{
  "name": "PetDefault",
  "blackboard": [
    { "name": "Self", "type": "GameObject" },
    { "name": "Player", "type": "GameObject" }
  ],
  "root": {
    "type": "selector",
    "children": [
      {
        "type": "guard",
        "requiresAll": true,
        "conditions": [
          { "condition": "HasHateCondition", "fields": [{ "name": "Self", "variable": "Self" }] }
        ],
        "child": {
          "type": "action",
          "action": "CancelNavigationAction",
          "fields": [{ "name": "Self", "variable": "Self" }]
        }
      },
      {
        "type": "action",
        "action": "FollowPlayerAction",
        "fields": [
          { "name": "Agent", "variable": "Self" },
          { "name": "Player", "variable": "Player" },
          { "name": "Distance", "literal": "3" }
        ]
      }
    ]
  }
}
```

### トップレベル

| フィールド | 内容 |
|---|---|
| `blackboard` | グラフのBlackboard変数の宣言。`name`と`type`（`GameObject`等の短縮名、または完全なクラス名） |
| `root` | 木のルートノード。`Start`ノードの子として自動的に接続される |

### ノード（`NodeSpec`）

`type`によって意味が変わる：

| `type` | 転写先 | 追加フィールド |
|---|---|---|
| `selector` | `SelectorComposite`（最初に成功した子を採用） | `children`（`NodeSpec[]`） |
| `sequence` | `SequenceComposite`（すべての子が成功するまで進む） | `children`（`NodeSpec[]`） |
| `guard` | `ConditionalGuardModifier`（条件を満たした時だけ`child`を実行） | `conditions`（`ConditionRefSpec[]`）、`requiresAll`（true=AND, false=OR）、`child`（`NodeSpec`） |
| `action` | 指定したAction資産そのもの | `action`（クラス名）、`fields`（`FieldSpec[]`） |

### `ConditionRefSpec`

```json
{ "condition": "HasHateCondition", "fields": [{ "name": "Self", "variable": "Self" }] }
```

`condition`は既存の`Unity.Behavior.Condition`派生クラスの名前（プロジェクト内のどこにあってもよい）。

### `FieldSpec`

Action/ConditionのBlackboardVariableフィールド1件分。`variable`と`literal`はどちらか一方だけを指定する。

| フィールド | 内容 |
|---|---|
| `name` | 対象クラスの`BlackboardVariable<T>`フィールド/プロパティ名 |
| `variable` | 指定した場合、`blackboard`で宣言したBlackboard変数にリンクする |
| `literal` | 指定した場合、リテラル値をその場に設定する（`float`/`int`/`bool`/`string`/`enum`に対応） |

### `"Self"`について

`Self`という名前のBlackboard変数は、Unity Behavior側が予約GUIDで自動生成し、
実行時に`BehaviorGraphAgent`が自身のGameObjectを自動バインドする特別な変数。
`blackboard`に`"Self"`を宣言すると、新規作成せず自動生成済みのものを再利用する
（重複作成すると、実行時の自動バインドの恩恵を受けられない別変数になってしまうため）。

## 対応済み / 未対応

**対応済み**：Selector、Sequence、単一条件のGuard（AND/OR切り替え可）、Action、
GameObject/float/int/bool/string/enum型のBlackboardVariable。

**未対応（ロードマップ）**：Parallel、Repeat/Cooldownのようなデコレータ、
named childrenを持つ複合ノード（Switch等）、Vector3等の複雑なリテラル型。
現時点で対応していないノード種別は、明示的な例外で失敗する（サイレントに壊れない）。

## 内部の仕組み（技術的な裏付け）

Unity BehaviorのグラフはUnity独自の`internal`クラス（`BehaviorAuthoringGraph`, `NodeModel`,
`ConditionalGuardNodeModel`等）で表現されており、公式に公開されたスクリプトAPIは無い。
このパッケージは、**リフレクション**（`BehaviorReflection.cs`）でこれらの内部構造を直接組み立て、
最後にUnity純正の公開メソッド`BehaviorAuthoringGraph.BuildRuntimeGraph()`を呼び出して
コンパイルを行う。ソースコードのコピー・改変は一切行っていない
（`Unity.Behavior`パッケージへの依存として、公開されたAPIとinternalなデータ構造を
リフレクション越しに利用しているだけ）。

`InternalsVisibleTo`のような特定アセンブリ名への依存もない——リフレクションはC#の
アクセス修飾子に左右されないため、パッケージがどのアセンブリ構成のプロジェクトに
インストールされても同じように動く。

この設計ゆえに、Unity Behaviorパッケージの将来のバージョンで内部クラスの構造が
変わった場合、転写処理は（サイレントに壊れるのではなく）明確な例外で失敗する。
パッケージのバージョンごとに対応するUnity Behaviorのバージョン範囲を明記し、
更新時は再検証する運用を前提としている。

### 既知の癖：BuildRuntimeGraph()の初回呼び出し

Unity.Behavior 1.0.16で確認：新規グラフに対して`BehaviorAuthoringGraph.BuildRuntimeGraph()`を
初めて呼び出すと、実行用グラフの`BlackboardReference`が、Blackboardに新規追加した変数
（`"Self"`以外のもの）を正しく反映しないことがある（`GetVariable`/`SetVariableValue`で
参照できない＝ノードのフィールドにも配線されない）。原因はUnity.Behavior側の内部コンパイラの
挙動によるもので、こちらの実装の誤りではないことを、Blackboard資産・実行用グラフの各段階を
リフレクションで直接検証して切り分け済み。GUIで少しずつ変数を追加しながら保存する通常の
編集フローでは踏まない経路と見られる。

回避策として、`BuildRuntimeGraph()`を1回呼んだ後、Blackboard資産に対して`SetAssetDirty()`を
呼んでからもう一度`BuildRuntimeGraph()`を呼んでいる（`BehaviorGraphImporter.Import`参照）。
2回目の呼び出しでは正しく全変数が反映されることを確認済み。

### 既知の癖：新規グラフには既定のStartノードが最初から1つ含まれる

`ScriptableObject.CreateInstance(BehaviorAuthoringGraph)`で新規グラフを作った時点で、
既定の"On Start"ノードが1つ自動的に`Nodes`へ追加されている。これに気づかず`Start`ノードを
追加で1つ作ると、ルートが2つになり、Unity側が自動的に`ParallelAllComposite`で両方を
包んでしまう。この場合、片方の空のStartノードが実行を阻害し、木全体（Selector配下の
すべての子）が一切ティックされなくなる（実機のPlay Modeで、ノードツリーの`CurrentStatus`を
直接ダンプして確認済み）。

`BehaviorGraphImporter`は、新規作成直後の`Nodes`に既存の`StartNodeModel`が無いか確認し、
あれば再利用する（`FindOrCreateStartNode`参照）。単純な木（ガード1段のみ等）では顕在化しにくいが、
Sequence/Guardを入れ子にした複雑な木では確実に発生するため、複雑な構成のテストで発見した。

### 既知の癖：再インポート時の参照安定化には「中身の再利用」が必要

以前のバージョンは、同じ出力パスに既存の`.asset`があれば削除してから新規作成していた。
これはコンテナ.asset自体のGUIDを変えてしまい、シーン上の`BehaviorGraphAgent.Graph`のような
参照が再インポートのたびに切れる原因になっていた。

現在は既存のメインアセットオブジェクトを再利用し、木構造（`Nodes`）と旧Blackboardサブアセット
だけを差し替える（`LoadOrCreateGraph`/`ClearGraphInPlace`）。ここで**もう1段注意が必要**
だったのが、コンパイル済みランタイムグラフ（`BehaviorGraph`）・その`DebugInfo`・
`RuntimeBlackboardAsset`という3つのサブアセット——コンテナのGUIDが同じでも、これらが
指す先自体（サブアセットのfileID）が変われば、`BehaviorGraphAgent.Graph`（このうち
`BehaviorGraph`を直接指す）は結局壊れる。そのため`ClearGraphInPlace`はこれら3つを
**あえて破棄せず残す**：`BuildRuntimeGraph()`は既存のサブアセットが残っていればそれを
更新する形で再利用してくれる（実機で確認済み）。コンテナのGUIDと中身のfileIDの両方が
安定して初めて、シーン上の参照は再インポートを跨いで生き残る。

### 既知の癖：Guardノードの条件数を変えると、再インポートだけでは直らないことがある

「中身の再利用」（上記）は通常は正しく機能するが、**既存のGuardノードが持つ条件
（conditions）の数を前回のインポートから増減させた場合**、`BuildRuntimeGraph()`が
古いランタイムグラフ（`BehaviorGraph`）を不完全に更新し、内部の`Conditions`リストに
null要素が残ることを実機で確認した。症状：インポート自体はエラーなく成功する
（`HasRuntimeGraph=true`が返る）が、**Play Mode開始時**に
`Unity.Behavior.ConditionalGuardModifier.OnStart()`が
`NullReferenceException`を投げ、そのGuardを含む全てのAgentが動かなくなる。
`AssetDatabase.ForceReserializeAssets()`では直らない（ディスク上のシリアライズが
更新されるだけで、この不整合はランタイムグラフオブジェクトの内部状態そのものにあるため）。

**対処**：`Import()`に`forceFreshRuntimeGraph: true`を渡す。既存のランタイムグラフ
サブアセットを破棄してから完全に再構築するため、この不整合が起きなくなる。
既定値は`false`のまま——通常のパラメータ調整（数値リテラルの変更等、ノード・条件の
数は変えない変更）では、`BehaviorGraphAgent.Graph`のようなサブアセットを直接指す
参照を再インポートを跨いで維持したいため（本セクション冒頭の「中身の再利用」の
恩恵をそのまま享受する）。

**注意（トレードオフ）**：`forceFreshRuntimeGraph: true`を使うと、ランタイムグラフ
サブアセットのfileIDが変わる。コンテナ.asset自体のGUIDは維持されるため
`BehaviorGraphAgent.Graph`がコンテナ経由で参照している場合は影響しないが、
コンテナ経由ではなくランタイムグラフサブアセットを直接指すフィールド（例：`BehaviorGraph`
型のカスタムフィールド）は参照が切れる（Inspectorでは`None`に見える）。
使う前に、そのグラフを直接参照している全てのPrefab・シーン上のインスタンスを
洗い出し、再インポート後に手動（またはスクリプトで）再設定すること。

## 配布について

**このリポジトリでソースを公開。Asset Store・OpenUPMへの登録は行わない。** 理由：このパッケージの核心
（JSON→グラフ資産への転写）はUnity Behaviorのinternal型へのリフレクションに依存しており、
これがUnity Asset Store Submission Guidelines（2.5.g「Editor internal APIへのリフレクション経由の
アクセスを禁止」）に抵触するため。`com.unity.behavior`には、グラフ資産をプログラムから
組み立てるための公開APIが存在せず、これを回避する現実的な代替アーキテクチャも無いと判断した。
ライセンスはMIT——条文は[LICENSE.md](LICENSE.md)を参照。
