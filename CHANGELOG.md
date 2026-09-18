# Changelog

This project follows the [Keep a Changelog](https://keepachangelog.com/) format
and [Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-09-06

### Added
- Transcription from a JSON declaration (`BehaviorTreeSpec`) into a Unity Behavior
  `.asset` (`BehaviorGraphImporter`).
- Supported node types: `selector` (SelectorComposite), `sequence` (SequenceComposite),
  `guard` (ConditionalGuardModifier, AND/OR switchable), `action` (existing Action assets).
- Blackboard variable declaration/linking, and literal value assignment
  (float/int/bool/string/enum).
- Automatic detection that avoids duplicating the reserved `"Self"` variable.
- `Assets/Create/Behavior/Import JSON to Behavior Graph` context menu.
- A reflection-based access layer for Unity.Behavior's internal APIs that does not
  rely on `InternalsVisibleTo` (`BehaviorReflection`) — works regardless of which
  assembly configuration the project uses.
- A full set of EditMode tests, and a sample (`Samples~/PetAI`).

### Fixed
- Worked around a known Unity.Behavior 1.0.16 behavior where the first
  `BuildRuntimeGraph()` call on a brand-new graph fails to reflect newly added
  Blackboard variables (other than `"Self"`, e.g. Player/Target) into the runtime
  graph (calling `BuildRuntimeGraph()` twice, with `SetAssetDirty()` in between).
  See "Known quirks" in README.md.
- Fixed a bug where execution silently stalled on complex trees with nested
  Guard/Sequence nodes, caused by creating an extra Start node without noticing
  that a new graph already includes a default "On Start" node
  (added `FindOrCreateStartNode`, which reuses the existing Start node). This did
  not surface on simple trees; it was found through real-device verification on a
  production-scale, complex tree.
- Fixed evaluation order not being guaranteed for sibling nodes placed at the same
  X coordinate — node evaluation order is determined by X coordinate (left evaluates
  first), since `GraphAssetProcessor.GetSortedConnections` sorts by `Position.x`.
  Introduced a `LayoutCursor` that advances X per leaf node, so the declared order
  of a JSON `children` array is correctly reflected as left-to-right positions.

### Added (within 0.1.0)
- The generated graph now automatically places a StickyNote (a comment-only node)
  noting the source JSON path and a "do not edit directly" warning.

### Fixed (within 0.1.0)
- Fixed an issue where re-importing deleted and recreated the `.asset` each time,
  changing the container's GUID and breaking references such as
  `BehaviorGraphAgent.Graph` in scenes on every re-import. Changed to reuse the
  existing main asset object and only replace the tree structure and Blackboard
  (`LoadOrCreateGraph`/`ClearGraphInPlace`). Compiled runtime graph sub-assets are
  also left intact and updated in place via `BuildRuntimeGraph()` instead of being
  discarded, so their fileIDs stay stable too (see "Known quirks" in README.md).
  Verified, both by test and on a real device, that the GUID and fileIDs are
  unchanged across re-imports.
