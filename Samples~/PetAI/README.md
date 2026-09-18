# Sample: PetAI

`pet_default.json` is a minimal pet AI declaration: it follows the player,
and stops following as soon as it has aggro (Hate).

Usage:
1. Copy this folder into your project's `Assets/` (done automatically when you
   import it from the "Samples" tab in the Package Manager).
2. Select `pet_default.json`, then right-click →
   `Create > Behavior > Import JSON to Behavior Graph`.
3. Assign the generated `pet_default.asset` to a `BehaviorGraphAgent`.

This sample references fictitious custom node classes — `HasHateCondition`,
`CancelNavigationAction`, and `FollowPlayerAction`. To actually run it, add
`Unity.Behavior.Condition`/`Action`-derived classes with those same names to
your project (see the "JSON Schema" section of the root README.md for how the
class names map to the JSON).
