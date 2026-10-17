# M2 — single-level gameplay loop

Prepared for manual Unity Editor QA on Unity 6000.3.8f1 / URP 17.3.0 / Input System
1.18.0 / Test Framework 1.6.0. No Unity Editor, batchmode, Hub or computer-use automation
was run in this milestone. Scene creation is deliberately through the menu below.

## Generate and open

1. Allow Unity import/compilation to finish and save open scene edits.
2. Run **ColorSlide > Setup > Apply M2 Gameplay Demo**.
3. Run **ColorSlide > Setup > Open M2 Gameplay Demo**. Use a **9:16** or **1080 × 1920**
   Game View and enter Play Mode.
4. After authoring changes, save and run **ColorSlide > Setup > Validate M2 Gameplay Demo**.

Setup creates `Assets/_ColorSlide/Scenes/GameplayDemo.unity` as a copy of the accepted
M1 scene. It replaces the M1 interaction hierarchy **in that new copy only**, retaining
the copied camera, light, board placeholder and lighting settings. The M0 Gameplay and
M1 InteractionDemo scenes remain untouched. Seven existing M1 URP materials are reused;
no new material, external artwork, package or TextMeshPro resource import is required.

The new M2 grid is 6 × 8 with **0.68-unit cells**. This is a demo-specific scale, leaving
room for full departure of a two-cell piece within the accepted orthographic camera.
M1's 0.9-unit grid, layout, transforms, camera and materials are preserved.

## Authored layout and solution

Grid columns correspond to world X; rows correspond to world Z; world Y is height.
Origin is the center of cell (0,0) on the board surface. Footprint offsets are nonnegative
and include occupied offset (0,0). N/S spans use columns; E/W spans use rows. Spans
are inclusive at Start and exclusive at Start + Span.

| Block / logical color | Anchor | Footprint offsets | Matching gate |
| --- | --- | --- | --- |
| Blue | (2,1) | (0,0) | North, start 2, span 1 |
| Amber | (1,3) | (0,0), (1,0) | South, start 1, span 2 |
| Green | (0,5) | (0,0), (1,0), (0,1) | West, start 5, span 2 |
| Purple | (4,5) | (0,0), (0,1) | East, start 5, span 2 |

Static obstacles are **(3,3)** and **(3,4)**. North is +row/+Z, South is -row/-Z,
East is +column/+X, and West is -column/-X. The default time limit is **180 seconds**.

Amber blocks Blue's northbound route. Blue initially blocks Amber's route to the south.
A valid solution, verified through the actual production session/movement APIs:

1. Move Blue horizontally left to **(0,1)**, clearing Amber's path.
2. Drag Amber down through its south gate: request anchor row **-1** or further.
3. Move Blue right to **(2,1)**, then drag north through column 2: request row **8** or further.
4. Drag Green west through its two-row gate: request column **-1** or further.
5. Drag Purple east through rows 5–6: request column **6** or further. The level wins.

No solver or randomized layout is involved. `M2DemoLayout` is one concrete authored
configuration shared by Editor construction and the solution regression test, not a
generic level format or ScriptableObject framework.

## Gates, exit safety and presentation

`BlockColor` is a logical enum independent of renderer/material colors. The default
`None` preserves accepted M1 data; M2 sessions require explicitly colored pieces and
matching gate colors. Gates have immutable identity, edge, color, Start and Span.
Invalid enum values, malformed/overflowing spans, duplicate IDs and overlapping openings
are rejected. Adjacent separate gates do not combine into one wider aperture.

M1's original `MoveTowards` and `TryMoveStep` remain boundary-clamped and unchanged.
M2 opts into `MoveWithExits` through `PuzzleSession`. The pointer target is not clamped
before M2 sees it. Dominant-axis choice and its X-tie rule remain the accepted M1 rule.

Within the board, the existing adjacent-step method remains authoritative. On an actual
outward request at a boundary, M2 finds one matching aperture that contains **every
perpendicular footprint cell**. It then checks **every cell at every adjacent anchor**
until the entire shape has departed. Remaining inside cells are checked against blocks
and obstacles even after leading cells are outside. Outside cells must cross the chosen
gate edge within its aperture. There is no diagonal movement, tunneling or pathfinding.

Only after the whole sweep succeeds are the current occupied cells released, the active
block removed, and an immutable `ExitPlan` returned. Repeating a request for that identity
returns inactive, without a second removal. Merely touching or pressing next to a gate
does not remove a block: the target must request an outward step beyond its valid anchor.
Fast interior-to-outside requests first validate all ordinary intermediate movement.

Presentation follows the accepted straight exit plan over **0.3 seconds** (configurable
within 0.2–0.4). The departing piece's colliders are disabled immediately; it becomes
inactive after the animation. Gate floors/jambs and solid boundary segments have no
decorative selection colliders. Solid wall segments are omitted at each gate cell.
Compound cell colliders still represent the actual rectangular/concave footprints.

**Presentation trade-off:** all new drag input is briefly blocked during an exit
animation. Logical cells are already free, but this serialization prevents another
piece entering the swept visual path before the departing piece disappears. The timer
continues during this brief interval. Normal inside-board movement remains snapped M1
movement, with no new smoothing or physics-based collision system.

## State, timer, Retry and HUD

`PuzzleSession` owns Ready → Playing → Won/Failed, remaining time and a fresh board
snapshot for Retry. It starts when the scene starts playing. The last logical removal
wins immediately; Win does not wait for visual animation. Clock advancement is in
`GameplayController.LateUpdate`, after pointer movement in Update. Therefore a final
accepted exit wins over expiration in the **same frame**. If a previous advancement
already failed, subsequent movement is rejected. Terminal states cannot replace each
other, cannot tick further and cannot accept input.

The deterministic core accepts an explicit nonnegative elapsed time. Runtime supplies
`Time.unscaledDeltaTime`; displayed time uses a nonnegative ceiling. Ready, Won and
Failed do not tick. No timer runs while the Editor setup/validation tools inspect scenes.

Restart is in place, without scene reload: cancel the drag, clear pending animation,
restore a fresh authoritative board with original anchors/footprints/colors/obstacles/
gates, reactivate the original views, restore their initial collider states, clear selection
feedback, reset 180 seconds, and enter Playing. The same objects and UI listeners are
reused. Retry also works during an exit animation or after either terminal state.

The HUD uses installed **UGUI 2.0.0**, a screen-space overlay Canvas/CanvasScaler at
1080 × 1920, built-in LegacyRuntime font and standard Text/Button components. The top
bar shows time, remaining blocks and Restart. A terminal overlay shows Win or Time's Up
and Retry. Its InputSystemUIInputModule uses the existing template asset's imported,
persistent UI action sub-assets; no Input Actions file is modified or regenerated.

UI presses are checked using an explicit GraphicRaycaster result at the pointer position
before 3D selection. This avoids depending on EventSystem update order or touch pointer
IDs, and prevents one button press simultaneously selecting a block. M1 still uses its
owned pointer-action clone without UI/session routing when its new optional M2 reference
is absent. HUD listeners are paired on enable/disable; Retry does not add listeners.

### Targeted UI reference correction after the first setup attempt

Input System 1.18.0 initializes default actions in `InputSystemUIInputModule.Reset`,
even when the new EventSystem object is inactive. Assigning a different `actionsAsset`
calls `UpdateReferenceForNewAsset`, which creates transient `InputActionReference`
wrappers. `SwapAction` then ignores assignments of imported references to the same
actions. Consequently the original setup found valid imported references but left
transient wrappers in the module; the persistence guard correctly refused to save.

The setup now clears `actionsAsset` first. This clears every default action slot before
assigning the existing project asset and its eight imported UI references. Unused tracked
device slots remain null. No new asset or sub-asset creation, accepted input edits, or
reimport during normal setup is needed. Validation checks all eight references for
persistence, sub-asset ownership and exact action identity, plus one input module.

The focused Editor regression copies the generated scene to a unique temporary asset,
checks all eight serialized GUID/file-ID references, forces a synchronous UI asset import,
and reloads the temporary scene. It verifies stable identities and unchanged source asset
bytes, then removes only its own copy. Run it after generating M2. It requires native
Unity Editor execution; standalone compilation does not prove AssetDatabase persistence.
Inspection after the reported failure found no GameplayDemo scene or orphan scene meta.

Relevant API checks: [InputSystemUIInputModule](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/api/UnityEngine.InputSystem.UI.InputSystemUIInputModule.html),
[GraphicRaycaster](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/UnityEngine.UI.GraphicRaycaster.html),
[unscaledDeltaTime](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-unscaledDeltaTime.html).

## File changes and protected integration points

New runtime files:

- `Board/ExitGate.cs`: logical colors/edges, validated gate data, move results and ExitPlan.
- `Board/GridBoard.Exits.cs`: gate validation, explicit exit-enabled movement and atomic removal.
- `Gameplay/PuzzleSession.cs`: deterministic state/timer/restart snapshot.
- `Gameplay/M2DemoLayout.cs`: one authored demo and matching gates.
- `Gameplay/ExitGateView.cs`: serialized gate data used by logic and geometry.
- `Gameplay/GameplayController.cs`: scene ownership, exit animation, Retry and UI hit filtering.
- `Gameplay/GameplayHud.cs`: UI display and button forwarding.

New Editor files: `M2GameplayDemo.cs`, `M2Validation.cs`. New test files:
`ExitRulesTests.cs`, `PuzzleSessionTests.cs`, `M2EditorSetupTests.cs`. New source/folder
metadata is included. No existing test or Editor setup utility is rewritten.

Necessary changes to protected files are limited to:

| File | Why this extension is necessary |
| --- | --- |
| `GridBoard.cs` | Add logical color/removal state and make the class partial so exit code can atomically access its private authoritative occupancy. Ordinary movement bodies are unchanged. Optional color defaults to None for M1 compatibility. |
| `GridBlockView.cs` | Persist explicit gameplay color and pass it during M2 construction. Existing visual behavior/default M1 calls are unchanged. A separate component alone would leave the existing board's shape import uncolored. |
| `InteractionBoard.cs` | Transfer view color into model data; internally adopt the session's restored model and snap the same views on Retry. Its private Model ownership could not otherwise be updated safely by an additive session component. |
| `PointerDragController.cs` | Add optional session routing, UI/terminal/animation gating and explicit drag cancellation; skip snapping removed identities. A separate controller would duplicate the accepted drag and compete for input. The null-session M1 path is retained. |
| Runtime/Editor asmdefs | Add the installed package assembly **UnityEngine.UI**, required to compile the HUD/EventSystem and their Editor construction. No package changes, test runner duplication or new assembly is introduced. |

The test asmdef also references **UnityEngine.UI** for the focused UI module persistence
regression; existing NUnit/Test Runner configuration and test bodies are preserved.

All accepted scenes, materials, camera/board transforms, Input Actions, project settings,
existing tests, M0/M1 Editor source, GUIDs and Git history remain untouched.

## Setup safety

The command reuses the M0 saved-scene/compilation/Play Mode/Prefab Mode guard. All
dependencies, materials, imported UI references and framing are checked before copying.
A clean untitled scene is safe: generation opens a saved copy additively rather than
creating a second untitled scene. Source inspection/validation uses isolated preview scenes.
The active scene is restored; only the owned copy is closed. Only a newly failed copy is
eligible for rollback. Existing M2 assets are validated and preserved, never resaved,
repaired or regenerated automatically. Conflicts stop with diagnostics. Existing user
scene edits are neither saved nor discarded by setup.

## Verification and manual QA

All three assemblies passed standalone Roslyn compilation against installed Unity/package
DLLs with warnings treated as errors. Production-model NUnit tests run outside Unity under
Unity's bundled Mono: **47 passed, zero failed/skipped** — 23 accepted M1 model cases,
17 exit-rule cases and 7 session cases. The solution sequence is one of these tests.
This does not execute the Unity scene lifecycle, Physics, native UI or pointer input.

Static verification also covers package/asmdef JSON, unchanged accepted tests and source
assets, runtime/editor boundaries, GUID uniqueness, metadata, persistent UI reference
availability in the importer, camera/exit framing arithmetic, Git index preservation and
diff/whitespace integrity. Transient compiler/NUnit output is only in ignored
`Temp/M2Validation/`. No Unity launch or GUI approval was attempted.

Manual QA after generating the scene:

1. Check 9:16 framing, visible gate colors/openings, four blocks, timer/count and HUD layout.
2. Press Restart/Retry with mouse and touch; UI must not drag blocks underneath.
3. Press/hold at a matching gate without outward motion: no removal. Push outward: one
   brief straight exit, collider disabled, one count decrement, correct cells freed.
4. After moving Amber out, bring Blue to (2,0) and push South: the Amber gate must reject
   its color. Check L/rectangle aperture alignment and blocked routes at fast speeds.
5. Follow the solution above to Win. Verify input and timer stop and Retry restores all.
6. Before entering Play Mode, temporarily set GameplayController's Time Limit to **1**
   in the M2 Inspector, then wait for Fail; verify a single terminal result and blocked
   input. Retry, test Restart during play/exit animation, then restore **180** for QA.
7. Exit Play Mode, save intended M2 edits, repeat Apply and Validate. Run the full EditMode
   suite: **56 cases expected** (29 accepted M0/M1, 24 new model cases, 3 new Editor
   integration cases). Check the Console for errors and warnings.

Scene/material reference serialization, built-in font availability, real UI layout/button
behavior, exit animation, touch interaction and the nine Editor-dependent tests still
require Unity verification. GameplayDemo does not exist until its setup menu is run.
No confirmed model-test or static compilation failure remains.

Git began clean on **main** at **562de72**. Existing index entries are preserved; new M2
files are unstaged. No commit, push, rebase, tag, release, Android build or later milestone
work was performed.
