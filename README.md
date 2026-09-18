English | [日本語](README_ja.md)

# JSON Behavior Import for Unity Behavior

An Editor extension for Unity Behavior (`com.unity.behavior`) that lets you
**declare a graph as JSON and transcribe it directly into a native `.asset`**.

## What this is for

Unity Behavior is powerful, but it assumes visual graph editing as its
authoring format. That's fine for humans working in the GUI, but it doesn't
fit code generation, automation, or working alongside an LLM/coding agent:

- Node coordinates and wiring are **positional information**, which is a poor
  fit for generation, review, or diffing
- There's no official API for editing graph assets directly from code
- Reviewing on Git means reviewing "what a node graph looks like", not
  readable text

This package lets you **declare the tree structure as JSON**, then combines
Unity Behavior's standard node assets — your own custom Action/Condition
nodes, plus Unity's 45 official built-in nodes — **completely unmodified**,
and builds the result with Unity's own graph compiler.

- JSON is a format an LLM/coding agent can generate, validate, and diff-review
- It doesn't reimplement node execution logic (Action/Condition) — it rides on
  the existing Behavior Agent ecosystem
- The output is a fully native `.asset`. `BehaviorGraphAgent` runs it as-is,
  and Unity's native visual debugger (breakpoints, highlighting the currently
  running node) works unchanged

The goal is a position no existing tool occupies: it doesn't implement node
logic itself, doesn't implement its own graph compiler, and doesn't depend on
GUI positional data — all three at once.

## Requirements

- Unity 6000.6 or later
- `com.unity.behavior` 1.0.16 (resolved automatically as a dependency)
- `com.unity.nuget.newtonsoft-json` (same as above — the tree is
  self-referential and hits `JsonUtility`'s serialization depth limit, which is
  why this package uses Newtonsoft.Json)

## Quick start

**Requirements:** Unity **6000.6+** · [Unity Behavior](https://docs.unity3d.com/Packages/com.unity.behavior@latest) `1.0.16` (resolved automatically — see [Requirements](#requirements) below for the full list).

1. **Install** — Package Manager → `+` → *Add package from git URL* →
   `https://github.com/shirousa/unity-jsonbehavior.git#main`
2. **Author** — declare your behavior tree as a `.json` TextAsset in your
   project (see the [JSON schema](#json-schema) below).
3. **Import** — select that JSON in the Project window, right-click →
   `Create > Behavior > Import JSON to Behavior Graph`. A `.asset` with the
   same name is generated in the same folder.
4. **Use it** — drag the generated `.asset` onto a `BehaviorGraphAgent` and
   use it like any other Behavior Graph.

Or call it directly from code:

```csharp
using Org.Shirousa.JsonBehavior.Editor;

bool succeeded = BehaviorGraphImporter.Import(jsonText, "Assets/MyBehaviors/enemy.asset");
```

See `Samples~/PetAI/pet_default.json` for a sample (importable as "Samples"
from this package's details page in the Package Manager).

## JSON schema

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

### Top level

| Field | Meaning |
|---|---|
| `blackboard` | Declares the graph's Blackboard variables. `name` and `type` (a short name like `GameObject`, or a fully qualified class name) |
| `root` | The tree's root node. Automatically connected as the child of the `Start` node |

### Nodes (`NodeSpec`)

Meaning varies by `type`:

| `type` | Transcribes to | Extra fields |
|---|---|---|
| `selector` | `SelectorComposite` (picks the first child that succeeds) | `children` (`NodeSpec[]`) |
| `sequence` | `SequenceComposite` (proceeds until all children succeed) | `children` (`NodeSpec[]`) |
| `guard` | `ConditionalGuardModifier` (runs `child` only when the conditions are met) | `conditions` (`ConditionRefSpec[]`), `requiresAll` (true=AND, false=OR), `child` (`NodeSpec`) |
| `action` | The specified Action asset itself | `action` (class name), `fields` (`FieldSpec[]`) |

### `ConditionRefSpec`

```json
{ "condition": "HasHateCondition", "fields": [{ "name": "Self", "variable": "Self" }] }
```

`condition` is the name of an existing `Unity.Behavior.Condition`-derived
class (it can live anywhere in the project).

### `FieldSpec`

One Action/Condition BlackboardVariable field. Specify exactly one of
`variable` or `literal`.

| Field | Meaning |
|---|---|
| `name` | The name of a `BlackboardVariable<T>` field/property on the target class |
| `variable` | If set, links to a Blackboard variable declared in `blackboard` |
| `literal` | If set, assigns a literal value in place (supports `float`/`int`/`bool`/`string`/`enum`) |

### About `"Self"`

A Blackboard variable named `Self` is a special variable that Unity Behavior
auto-generates with a reserved GUID, and that `BehaviorGraphAgent` automatically
binds to its own GameObject at runtime. Declaring `"Self"` in `blackboard`
reuses the auto-generated one instead of creating a new one (creating a
duplicate would produce a separate variable that doesn't get the runtime
auto-binding).

## Supported / not yet supported

**Supported**: Selector, Sequence, single-condition Guard (AND/OR
switchable), Action, and BlackboardVariable of type
GameObject/float/int/bool/string/enum.

**Not yet supported (roadmap)**: Parallel, decorators like Repeat/Cooldown,
composite nodes with named children (e.g. Switch), and complex literal types
like Vector3. Node kinds that aren't currently supported fail with an
explicit exception (no silent breakage).

## How it works internally (technical background)

Unity Behavior's graphs are represented by Unity's own `internal` classes
(`BehaviorAuthoringGraph`, `NodeModel`, `ConditionalGuardNodeModel`, etc.),
and no public scripting API is provided for them. This package directly
assembles these internal structures via **reflection**
(`BehaviorReflection.cs`), and finally compiles by calling Unity's own public
method `BehaviorAuthoringGraph.BuildRuntimeGraph()`. No source code is copied
or modified — it only depends on `Unity.Behavior` as a package, using its
public API and internal data structures via reflection.

There's no dependency on `InternalsVisibleTo` or any specific assembly name
either — reflection isn't affected by C#'s access modifiers, so the package
works the same way regardless of what assembly configuration the installing
project uses.

Because of this design, if a future version of Unity Behavior changes the
structure of its internal classes, the transcription process fails with a
clear exception rather than breaking silently. The intended operating model
is to pin each package version to a known-compatible range of Unity Behavior
versions, and to re-verify on update.

### Known quirk: the first call to `BuildRuntimeGraph()`

Confirmed on Unity.Behavior 1.0.16: the first time
`BehaviorAuthoringGraph.BuildRuntimeGraph()` is called on a brand-new graph,
the runtime graph's `BlackboardReference` can fail to correctly reflect
variables newly added to the Blackboard (other than `"Self"`) — they become
unreachable via `GetVariable`/`SetVariableValue`, and aren't wired to node
fields either. This is caused by Unity.Behavior's internal compiler behavior,
not a bug in this package's implementation — confirmed by directly inspecting
the Blackboard asset and runtime graph at each stage via reflection. This path
doesn't appear to be hit by the normal GUI editing flow of adding variables
incrementally and saving.

The workaround: call `BuildRuntimeGraph()` once, then call
`SetAssetDirty()` on the Blackboard asset, then call `BuildRuntimeGraph()`
again (see `BehaviorGraphImporter.Import`). Confirmed that the second call
correctly reflects all variables.

### Known quirk: a new graph already includes a default Start node

The moment you create a new graph with
`ScriptableObject.CreateInstance(BehaviorAuthoringGraph)`, a default "On
Start" node has already been added to `Nodes`. If you don't notice this and
create an additional Start node, you end up with two roots, which Unity
automatically wraps in a `ParallelAllComposite`. In that case, the empty
extra Start node blocks execution, and the entire tree (every child under the
Selector) never ticks at all (confirmed on a real device in Play Mode by
directly dumping `CurrentStatus` on the node tree).

`BehaviorGraphImporter` checks whether `Nodes` already contains a
`StartNodeModel` right after creation and reuses it if so (see
`FindOrCreateStartNode`). This doesn't surface easily on simple trees (e.g. a
single-level guard), but reliably occurs on trees with nested
Sequence/Guard — found through testing a more complex configuration.

### Known quirk: stabilizing references on re-import requires reusing the graph's contents

An earlier version deleted the existing `.asset` at the output path (if any)
and created a new one. This changed the container asset's own GUID, which
broke references such as `BehaviorGraphAgent.Graph` in scenes on every
re-import.

The current version reuses the existing main asset object, and only replaces
the tree (`Nodes`) and the old Blackboard sub-asset
(`LoadOrCreateGraph`/`ClearGraphInPlace`). There's **one more subtlety**
here: even when the container's GUID stays the same, the compiled runtime
graph (`BehaviorGraph`), its `DebugInfo`, and the `RuntimeBlackboardAsset` —
three sub-assets — still break `BehaviorGraphAgent.Graph` (which points
directly at the `BehaviorGraph`) if what they themselves point to (the
sub-asset's fileID) changes. That's why `ClearGraphInPlace` **deliberately
leaves these three intact rather than destroying them**: `BuildRuntimeGraph()`
reuses and updates the existing sub-assets if they're still present
(confirmed on a real device). References in scenes survive re-import only
once both the container's GUID and its contents' fileIDs are stable.

### Known quirk: changing a Guard node's condition count can survive re-import incorrectly

The "content reuse" approach above normally works correctly, but when **the
number of conditions on an existing Guard node changes from the previous
import**, `BuildRuntimeGraph()` was confirmed on a real device to update the
old runtime graph (`BehaviorGraph`) incompletely, leaving null entries in its
internal `Conditions` list. Symptom: the import itself succeeds without error
(`HasRuntimeGraph=true` is returned), but **at Play Mode start**,
`Unity.Behavior.ConditionalGuardModifier.OnStart()` throws a
`NullReferenceException`, and every Agent using that Guard stops working.
`AssetDatabase.ForceReserializeAssets()` doesn't fix it (it only refreshes the
on-disk serialization — the inconsistency lives in the runtime graph object's
own internal state).

**Fix**: pass `forceFreshRuntimeGraph: true` to `Import()`. This discards the
existing runtime graph sub-asset and rebuilds it completely, which avoids the
inconsistency. The default stays `false`, because ordinary parameter tweaks
(e.g. changing a literal value, without changing the number of nodes or
conditions) should keep references that point directly at sub-assets — such
as `BehaviorGraphAgent.Graph` — stable across re-imports (the benefit
described in "content reuse" above).

**Caveat (trade-off)**: using `forceFreshRuntimeGraph: true` changes the
runtime graph sub-asset's fileID. Since the container `.asset`'s own GUID is
preserved, this doesn't affect references that go through the container (like
`BehaviorGraphAgent.Graph`), but any field that points **directly at the
runtime graph sub-asset** (e.g. a custom field typed as `BehaviorGraph`
instead of the container asset) will lose its reference (it will show as
`None` in the Inspector). Before using this, find every Prefab and scene
instance that directly references that graph, and re-assign them (manually or
via script) after re-import.

## Distribution

**Source-available on this repository; not submitted to the Unity Asset
Store or listed on OpenUPM.** Reason: this package's core mechanism
(transcribing JSON into a graph asset) relies on reflection into Unity
Behavior's internal types, which conflicts with the Unity Asset Store
Submission Guidelines (section 2.5.g, "prohibits access to Editor internal
APIs via reflection"). `com.unity.behavior` has no public API for
programmatically assembling a graph asset, and no practical alternative
architecture avoids this. This package is MIT-licensed — see
[LICENSE.md](LICENSE.md) for the full text.
