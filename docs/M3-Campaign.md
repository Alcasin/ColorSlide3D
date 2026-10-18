# M3 — Authored levels and campaign progression

Prepared for manual Unity 6000.3.8f1 QA. The starting repository was clean on `main` at
`4c819e2` (accepted M2). No Unity process, batchmode, headless mode, Hub or computer-use
automation was launched. Native assets are created by the supported Editor APIs in the
setup command, not by writing scene/prefab YAML.

## Create and play

1. Let scripts import and compile; save open scene edits.
2. Run **ColorSlide > Setup > Apply M3 Campaign**.
3. This creates missing assets `Assets/_ColorSlide/Levels/Level01.asset` through
   `Level04.asset` and `Assets/_ColorSlide/Scenes/Campaign.unity`.
4. Run **Open M3 Campaign**, choose a 9:16 Game View and enter Play Mode.
5. The campaign starts at Level 1. Win, then press Next. Level 4 ends with
   **Campaign Complete!** and Retry for replaying the final level; there is no Level 5.
6. Exit Play Mode, save intended edits and run **Validate M3 Campaign**.

The four `.asset` files and Campaign scene do not exist until the manual setup menu is
run. All authoring defaults, asset creation, wiring and validation are implemented.
No accepted M0/M1/M2 scene is rewritten.

## Integration and ownership

M2's `InteractionBoard` owns explicit views and a logical model. Its existing
`GameplayController` owns that model's `PuzzleSession`, collider states, a short local
exit animation and in-place Retry. M2's scene geometry was created only in Editor code;
there was no runtime factory or reusable board prefab.

M3 adds a small `LevelPresentation` factory that reconstructs that same graybox style
using the accepted `GridBlockView`, `ExitGateView`, `InteractionBoard`,
`PointerDragController`, `GameplayController`, seven M1 materials and pointer asset.
It owns only the current level hierarchy. Camera, light, slab, EventSystem and HUD persist.
The accepted M2 UI input module and its imported action references are copied with the
scene; M3 creates no new UI references, action assets or input modules.

`LevelDefinition` is a ScriptableObject containing serializable `LevelData`: identity,
display name, dimensions, cell size, seconds, blocks, obstacles and gates. Block entries
store identity, logical color, anchor and footprint; gate entries reconstruct existing
immutable `ExitGate` objects. Placement and gate validation use the production board;
no second collision model or solver exists. Invalid data is reported with level and
block/gate context, not corrected silently. A block must have a matching aperture wide
enough for its full perpendicular footprint.

`CampaignSession` holds ordered data, current index and the existing single-level
session. Completion and Next eligibility derive from that session's state. It validates
all four definitions before starting. Next requires Won; a newly loaded Playing session
rejects a duplicate Next. Failed never advances. Restart/Retry delegates to the current
M2 controller, retaining the selected level and its original snapshot and timer.

For Next, a replacement hierarchy is constructed **inactive** before progression commits.
A preparation failure leaves the current index/session unchanged and destroys its staged
objects. The old hierarchy is then deactivated synchronously, cancelling drag and disabling
its owned action clone, and retired. Destruction completes at the end of the frame in a
player; inactive old objects cannot receive input or tick. The new hierarchy then activates.
There are no timing delays, coroutines, global managers or accumulating listeners.

Next is disabled until the accepted final exit animation finishes. Restart remains usable
during that animation and uses M2's existing cancellation. Gameplay stays disabled in
Won/Failed. The timer and exit-before-timeout precedence remain M2 behavior.

The existing HUD gains optional campaign references, a level label and Next. M2's null
campaign path remains intact. The copied campaign top bar is 240 reference pixels high;
the accepted M2 layout and scene are untouched. The same HUD persists across levels and
adds/removes its listeners only on enable/disable.

## Exact authored data

All boards are **6 columns × 8 rows**, with **0.68-unit cells**. X is column/world X,
Y is row/world Z; world Y remains height. North is +row, South -row, East +column,
West -column. Anchors are occupied lower-left cells. Shape offsets:

- S: `(0,0)`
- H: `(0,0), (1,0)`
- V: `(0,0), (0,1)`
- L: `(0,0), (1,0), (0,1)`

Gate notation gives **edge, start, span**. N/S starts use columns; E/W starts use rows.
Intervals are `[start, start + span)` and every gate matches its named color.

| Level | Blocks: ID/color, shape, anchor | Obstacles | Gates | Timer / purpose |
| --- | --- | --- | --- | --- |
| 1 Basic Exits | Blue/Blue S (1,1); Amber/Amber S (4,6) | None | Blue N,1,1; Amber S,4,1 | 180s; direct matching exits |
| 2 Make Space | Blue/Blue S (2,1); Amber/Amber H (1,3); Purple/Purple V (4,5) | None | Blue N,2,1; Amber S,1,2; Purple E,5,2 | 180s; clear a blocking dependency |
| 3 Irregular Footprints | Green/Green L (0,3); Amber/Amber H (0,5); Purple/Purple V (4,3) | (2,3), (2,4), (2,5), (2,6) | Green W,5,2; Amber S,0,2; Purple E,3,2 | 240s; route the full L around a narrow passage |
| 4 Combined Constraints | Blue/Blue S (2,0); Amber/Amber H (1,2); Green/Green L (0,3); Key/Blue S (0,5); Purple/Purple V (2,5) | (3,3), (3,4) | Blue N,2,1; Amber S,1,2; Green W,5,2; Purple E,5,2 | 300s; several blocking dependencies |

The upper Blue piece in Level 4 has logical ID `Key`; both Blue pieces legitimately
share the same Blue gate. Level coordinates live in Editor creation defaults and serialized
assets, not runtime controllers. After creation the assets are authoritative, including
timer tuning; setup never replaces them with defaults.

## Verified solutions

Start from a fresh level/Retry. Each arrow is a separate axis-aligned drag target for the
block anchor. An outside target marked **exit** invokes the existing atomic swept exit;
the presentation continues to full departure, including a two-cell L's trailing cells.

1. **Basic Exits:** Blue → `(1,8)` **exit North**; Amber → `(4,-1)` **exit South**.
2. **Make Space:** Blue → `(0,1)`; Amber → `(1,-1)` **exit South**; Blue → `(2,1)`;
   Blue → `(2,8)` **exit North**; Purple → `(6,5)` **exit East**.
3. **Irregular Footprints:** Green → `(0,1)`; Green → `(3,1)`; Amber → `(0,-1)`
   **exit South**; Green → `(0,1)`; Green → `(0,5)`; Green → `(-1,5)` **exit West**;
   Purple → `(6,3)` **exit East**.
4. **Combined Constraints:** Purple → `(6,5)` **exit East**; Key → `(2,5)`;
   Key → `(2,8)` **exit North**; Green → `(0,5)`; Green → `(-1,5)` **exit West**;
   Blue → `(0,0)`; Amber → `(1,-1)` **exit South**; Blue → `(2,0)`;
   Blue → `(2,8)` **exit North**.

These are one valid solution each, not claims of unique solutions. Level 2's Blue and
Amber obstruct each other's direct exits. In Level 3, the pillar blocks Green's lateral
route at its original row, and Green blocks Amber's southbound route; the L must move
into the lower passage first. In Level 4, Purple blocks Key's direct path toward the Blue
gate, Key blocks Green's northward alignment, and the lower Blue and Amber obstruct
each other's direct exits. Repositioning can offer alternative orders, as intended for
ordinary sliding blocks; direct outward dragging of every piece cannot clear these layouts.

## Conservative setup

Apply checks the M0 saved-scene/compilation/Play/Prefab guard and accepted M2 validation
before creation. It validates all existing/default level data, materials and full-departure
camera envelopes before copying. The camera is never adjusted to accommodate a layout.
Missing assets are created with `CreateInstance`, `CreateAsset`, `SaveAssetIfDirty`, then
synchronous import and resolution of the persistent representation. Conflicting/orphaned
paths or invalid/dirty existing level assets stop setup with diagnostics.

Only the new copy of GameplayDemo has its board replaced and HUD extended. The previous
active scene is restored and only the owned copy is closed. Failed creation rolls back
only newly created assets when safe; existing assets remain intact. A partially existing
set of valid level assets is reused. An existing Campaign scene is validated and preserved,
never reconstructed or resaved. Validation inspects saved assets in preview scenes.

## Changed and new files

Only two accepted runtime files change:

- `GameplayController.cs`: expose whether an exit is presenting and accept the campaign's
  validated session before activating the newly constructed hierarchy. Its movement,
  animation, timer advancement, Retry and input-hit-test method bodies are unchanged.
  This small hook is necessary because Session and its collider snapshot are privately owned.
- `GameplayHud.cs`: optional campaign references, dynamic current owner, level text and
  Next/result routing. Replacing the HUD would duplicate its working timer/buttons and
  input-blocking integration. Existing M2 behavior remains the null-campaign path.

New runtime: `LevelDefinition.cs` (data and SO), `CampaignSession.cs`,
`CampaignController.cs`, `LevelPresentation.cs`.
New Editor: `M3AuthoredLevels.cs`, `M3Campaign.cs`, `M3Validation.cs`.
New tests: `CampaignTests.cs`, `M3EditorSetupTests.cs`. Matching source metadata is included.
No assembly/package configuration, old test, old Editor utility, material, input asset,
accepted scene, project setting or existing metadata is changed.

## Validation and manual QA

Standalone Roslyn compilation of runtime, Editor and tests uses installed Unity/package
DLLs with warnings treated as errors. NUnit under Unity's bundled Mono executes the
production-model fixtures, including all four explicit solutions, every intermediate
anchor and occupancy state, initial dependencies, invalid data, progression, final
completion, retry and failed presentation preparation. Native Editor tests are prepared
for persisted assets/reimport, idempotency, repeated presentation cleanup and dirty-scene
refusal. Compilation is not a substitute for native Unity scene lifecycle or input QA.

Actual standalone results: **62 passed, 0 failed/skipped/inconclusive** (47 accepted M1/M2
model cases and 15 campaign cases). All three assemblies compile; the runtime compiles
without Editor defines or UnityEditor references. The full native suite should contain
**75 cases**: 56 accepted, 15 new model cases and 4 new Editor integration cases. Those
13 native Editor-dependent cases were not executed here. Transient compiler/test outputs
are under ignored `Temp/M3Validation/`.

Manual QA:

1. Apply/Open M3, then Play in 9:16. Confirm Level 1, one camera/light/HUD/EventSystem,
   two pieces, 180 seconds and no initial result panel.
2. Follow all four solutions, press Next only after each non-final Win, and verify the
   next timer/count/name and correct board. Repeat clicks must not skip a level.
3. Verify Level 4's completion message, no Next/Level 5, and Retry of Level 4.
4. Temporarily tune the selected level asset's Seconds to 1 before Play; verify Fail,
   disabled dragging and no Next. Retry must retain the level. Restore its documented
   timer afterward; setup must not reset approved tuning.
5. Restart while playing, dragging and exiting; verify original positions, colliders,
   occupancy, timer and count. Repeat Next/Retry/Restart, checking for stale geometry,
   duplicated roots, input subscriptions or repeated button callbacks.
6. Check real mouse and touchscreen UI activation without selecting underlying pieces,
   full irregular exits, camera framing and the added top-bar/Next layout.
7. Exit Play, save edits, run Apply twice and Validate. Check level values, scene bytes
   and GUIDs remain stable. Unsaved scene changes must refuse setup without saving or
   discarding them. Run the full EditMode suite, then smoke-test M0/M1/M2 and Console.

Actual Unity creation, imported asset/scene references, UI appearance, pointer/touch
behavior, lifecycle/animation transitions and native Editor tests require manual QA.
There is no disk progression persistence; a new Play session starts at Level 1.
No M4 work, commit, push, rebase, tag, release or build preparation was performed.
