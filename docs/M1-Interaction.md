# M1 — core 3D block interaction

Implementation is prepared for Unity Editor QA. M0's accepted files, assemblies, Input
Actions, camera, materials and project settings are unchanged.

## Create and run the demo

1. Open the project in **Unity 6000.3.8f1**, allow import/compilation to finish, and save
   any scene edits. Run **ColorSlide > Setup > Apply M1 Interaction Demo**.
2. Run **ColorSlide > Setup > Open M1 Interaction Demo**. The dedicated scene is
   `Assets/_ColorSlide/Scenes/InteractionDemo.unity`. Choose **9:16** or **1080 × 1920**
   in Game View, then enter Play Mode.
3. Hold the mouse button on a colored piece and drag. On Android the same interaction
   uses the touchscreen's primary pointer. The entire piece brightens while selected.
4. After editing the saved scene, use **ColorSlide > Setup > Validate M1 Interaction Demo**.
   Setup may be repeated: an existing valid M1 scene is inspected and left untouched.

**Scene/material creation has not been executed in this implementation session.**
Computer-use access to Unity Hub timed out at its app-approval step, before the Editor
could be opened. The setup menu above is therefore required to generate the actual
playable scene and seven new materials through Unity APIs. No scene or material YAML
was authored directly, and Unity was not launched in batchmode or headless mode.

The accepted M0 scene was inspected: its slab is 6 × 8 units, top Y=0; the camera is
orthographic, at (0,12,-6), rotation quaternion (0.5257311,0,0,0.85065085), size 6.25.
M1 derives a **6-column × 8-row**, **0.9-unit** grid inside this surface with margins.
The scene builder copies M0 and adds M1 content; it does not resize the original slab
or alter the copied camera/light settings. It checks 9:16 framing before creation.

## Layout

All anchors and blocked cells below are logical grid coordinates, with (0,0) at the
lower left. Layout is deterministic and intentionally spacious for mechanics QA.

| Piece | Initial anchor | Footprint offsets | Tint |
| --- | --- | --- | --- |
| Single | (0,0) | (0,0) | Blue |
| Horizontal | (2,1) | (0,0), (1,0) | Amber |
| Vertical | (5,1) | (0,0), (0,1) | Purple |
| Elbow | (1,5) | (0,0), (1,0), (0,1) | Green |

Static obstacles occupy **(2,3)** and **(3,3)**. Low boundary walls sit outside the grid.
The checker surface has a tile per cell. Each shape has a root and one colored cube
and BoxCollider per occupied cell, without a collider spanning the elbow's empty
corner. All cells of a piece share identity, selection feedback and motion. Small seams
between cubes and contrasting tints distinguish the graybox pieces.

## Logic, coordinates and movement

- `GridBoard` is independent of GameObjects, rendering, input and time. It owns stable
  block IDs, anchors, copied read-only footprints, static obstacles and occupancy.
- A footprint is nonempty, nonnegative, includes (0,0), and has unique offsets. Its
  anchor is an occupied lower-left cell. Every occupied cell is `anchor + offset`.
  Invalid placement, duplicate identities and malformed data are rejected atomically.
- `BoardCoordinates.Origin` is the **center of cell (0,0)** at surface height.
  `world = origin + (column × cellSize, 0, row × cellSize)`. Grid Y maps to world Z;
  world Y is visual height. World-to-cell uses nearest centers, exact half-cell ties
  towards the positive axis, and clamps extreme finite coordinates to integer range.
- On each pointer-motion update, the dominant displacement in **world X/Z** selects
  the requested axis. X wins exact ties. This axis choice lasts only for that update;
  the player may change it before releasing the pointer.
- `MoveTowards` follows **only that requested axis**, validating cardinal adjacent
  steps until the target or first obstruction. The unchosen axis is not tried as a
  fallback. A stationary pointer makes no further requests. This prevents implicit
  detours, diagonal jumps and tunneling through an occupied intermediate cell.
- Each accepted step validates the entire footprint, ignoring its own occupancy, then
  updates old/new occupancy and anchor together. Rejection retains the last valid
  anchor. Shape extent limits targets to valid board boundaries.

## Input and presentation

`PointerDragController` clones the existing M0 Input Actions asset and uses the existing
`PointerActions` accessor. It polls press/release state without event subscriptions.
It enables/disables only its owned clone, then destroys it on disable. Package files,
bindings and the project-wide input assignment are preserved.

Selection calls `Camera.ScreenPointToRay` and a 3D `Physics.Raycast`. Compound child
colliders identify their `GridBlockView` parent; only views owned by this board may be
selected. Physics transforms are synchronized before a new selection. Hitting a wall,
obstacle or empty space does not start a drag. See Unity's
[Physics.SyncTransforms API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Physics.SyncTransforms.html).

The initiating pointer device is retained for position and held state so movement from
a second device cannot steal the drag. Ray-plane intersection on the horizontal board
plane converts screen position to world position. The initial plane-hit-to-anchor offset
is retained, including when pressing a non-anchor cell of a rectangle or elbow.

**Visual simplification:** pieces snap directly to accepted grid anchors while held.
There is no animation or diagonal interpolation through other geometry. Fast requests
may advance several validated cells in one frame. Release leaves the current valid
anchor in place. This is intentionally a discrete graybox interaction, not continuous
sub-cell sliding or final animation.

Release/cancellation, device removal, lost focus, application pause, disabled views,
disabled board/camera and disabled controller end selection safely. Disabled blocks
retain logical occupancy; disabling presentation does not make occupied cells free.
One primary pointer is supported; multi-touch gestures are outside M1.

## Setup safety and authoring

The menu first uses the accepted M0 edit-mode/unsaved-scene guard and read-only project
validation. Missing prerequisites or conflicting asset paths stop with diagnostics.
Source scene inspection uses an isolated preview scene. Creation opens a **saved copy**
additively, so it also works with a clean untitled Editor scene present. It restores the
previous active scene and closes only its copy when finished.

An existing M1 scene is never regenerated, repaired, resaved or reset automatically.
Valid existing M1 materials are reused without resetting authored properties. New
materials use URP/Lit under `Assets/_ColorSlide/Materials/M1/`. If initial construction
fails, only the failed scene copy created by that call is eligible for rollback; created
materials are retained and reusable. No build list, target switch or PlayerSettings write
is performed. The explicit Open menu checks for unsaved scenes before opening M1 alone.

Board dimensions, cell size, obstacles, IDs, initial anchors, footprints, views and
controller references are serialized Inspector values. Geometry and collider arrangement
are intentionally simple authored content: changing a footprint requires updating its
child cells/colliders too. Validation reports mismatches rather than replacing authored
objects. The grid root must have identity rotation and unit world scale to preserve XZ.

## Implementation files

All C# additions use the existing assembly definitions; no assembly/package dependency
was added or changed, and no legacy `optionalUnityReferences` entry was introduced.

| New source | Responsibility |
| --- | --- |
| `Runtime/Board/GridBoard.cs` | GridBlock data, occupancy and adjacent movement |
| `Runtime/Board/BoardCoordinates.cs` | XZ conversion and deterministic axis choice |
| `Runtime/Interaction/InteractionBoard.cs` | Serialized board configuration and view ownership |
| `Runtime/Interaction/GridBlockView.cs` | Compound piece data, position and selection tint |
| `Runtime/Interaction/PointerDragController.cs` | Pointer selection, plane projection and drag lifetime |
| `Editor/M1InteractionDemo.cs` | Create-once scene/material workflow and open menu |
| `Editor/M1Validation.cs` | Read-only board, reference, collider and framing checks |
| `Tests/EditMode/GridBoardTests.cs` | Production model/coordinate tests |
| `Tests/EditMode/M1EditorSetupTests.cs` | Repeated setup preserves M0/M1 bytes and scene arrangement |

Each new Unity source/folder has a unique `.meta` file. The M0 tests and all existing
runtime/editor files are unchanged. Git started this milestone on **main**, with the
user's M0 files already staged and no commit; that index is preserved. New M1 content
is left unstaged. No commit, push, rebase, tag or release was performed.

## Verification and remaining QA

Static Roslyn compilation of Runtime, Editor and EditMode assemblies passed against
the installed Unity 6.3/package DLLs with warnings treated as errors. **23 model test
cases passed, zero failed or skipped**, using the installed NUnit library and Unity's
bundled Mono outside the Editor. This runs the production model, not a fake algorithm;
it does not exercise Unity's scene lifecycle, Physics, Input System events or rendering.

JSON/assembly dependencies, runtime/editor boundaries, Input Actions validity, new GUIDs
and metadata, asset paths, Git whitespace/diff integrity, the initial layout, portrait
framing arithmetic and preservation of original files/index are checked separately.
The new Editor integration test and the five accepted M0 tests must run in Unity.

Compact manual QA after generating/opening the demo:

1. Verify portrait framing and four colored shapes on the grid. Enter Play Mode.
2. Press empty space/obstacles; nothing should select. Press each piece and check the
   whole shape brightens, including when pressing an elbow arm away from its anchor.
3. Drag horizontally, reverse, then move vertically without releasing. Check response
   during the hold, no initial anchor jump, and no movement with a stationary pointer.
4. Push every edge; push a rectangle/arm into another block. The full footprint must
   remain inside and non-overlapping. Test the elbow's empty corner separately.
5. Move the blue piece along row 0 to column 2, then drag quickly towards row 7: the
   obstacle at (2,3) must stop it at (2,2). Repeat towards another block with a clear
   destination beyond it. Try diagonal motion at blocked corners; no corner cutting.
6. Release off-center, select another piece, repeat, lose/refocus Game View, and toggle
   the controller off/on. Positions must remain valid and selection must clear.
7. Exit Play Mode; save intended edits. Repeat Apply M1, then Validate M1. Run the full
   EditMode suite: the **five M0 tests**, **23 model cases**, and **one M1 Editor test**.
   Check the Console. Mouse behavior, Android touch, actual Unity compilation/import,
   scene construction, rendering and test execution require Editor/device verification.

No gameplay beyond block interaction is implemented: no exits, matching, removal,
rotation, solver, win/fail, timer, progression, UI, audio, final art or Android build.
