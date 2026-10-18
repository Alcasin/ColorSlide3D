# M4 — Soft Toy Studio visual and audio polish

Starting point: clean `main` at accepted M3 commit `bb638f5`. Existing four levels,
75 tests and M0–M3 scenes are protected. Unity, batchmode, headless mode, Hub and
computer-use automation were not launched. Rendering, playback and native lifecycle
acceptance remain manual.

## Apply and inspect

1. Let Unity import the new scripts/shader/WAVs, then save open scene changes.
2. Run **ColorSlide > Setup > Apply M4 Polish**.
3. Run **Open M4 Polished Campaign**, select 9:16 and enter Play Mode.
4. After saving intended visual edits, run **Validate M4 Polish**.

Apply uses supported Editor APIs to copy `Campaign.unity` to
`Assets/_ColorSlide/Scenes/PolishedCampaign.unity`. It creates missing M4 native mesh,
material and theme assets. Those native assets and the new scene do not exist until
the menu is run. The shader source and six original PCM WAVs are already supplied.
The original Campaign and earlier scenes are never saved or rebuilt.

## Art and presentation

M3 constructs its 3D objects at runtime. A new optional `SoftToyTheme` is assigned only
to the polished scene's `LevelPresentation`, so every level/retry/transition receives
the style. The null-theme path retains the accepted graybox implementation.

Two small meshes are generated once and saved as shared assets:

- **RoundedBox.asset:** centered unit bounds, 384 vertices, 588 triangles, analytic
  smooth normals, outward winding and UVs. A rounded-box projection creates genuine
  beveled volume, with shared edge positions/normals across the six face patches.
- **ContactShadow.asset:** 65 vertices and 96 triangles on XZ, using vertex alpha that
  fades to a fully transparent outer rim. The included tiny URP unlit shader blends
  these contact shadows without textures or screen-space effects.

Block cells have separate rounded visual children. Their widths are 0.98 cells with
small 0.02-cell seams. The accepted 0.94 × 0.46 × 0.94 cell BoxColliders, their transforms,
root anchors and footprints remain intact. Assembled cells are deliberate: they handle
the L's concave corner reliably without a fragile polygon-union algorithm or filled
empty corner. Seams remain visible at close range; this is not a single welded mesh.
Selection lifts only visual children by 0.018 cells and retains the accepted brightness
emphasis. Exit visuals shrink by at most 6% using the existing exit progress. The original
root movement, duration, collider disabling, occupancy release and terminal timing are
unchanged. Visual state resets on Restart and disable.

The seven-color M4 palette is Blue `#529FE8`, Amber `#F3AD55`, Violet `#9A7BDD`, Green
`#50B993`, two neighboring neutral tile tones `#E3E9EE` / `#E1E7EC`, and obstacle
`#9AA9B8`. A separate `#D9E3EC` tray material and contact-shadow material bring the total
to **nine new material assets**. URP Lit surfaces are opaque, nonmetallic, with restrained
smoothness (0.28 blocks, 0.18 neutrals). No approved M1 material is edited.

The project is linear. URP Lit's `_BaseColor` has no HDR/Gamma flag, so material creation
converts the authored sRGB values once. The existing property-block tint path receives
sRGB, matching Unity's different API contract and avoiding double conversion. References:
[Material.SetColor](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Material.SetColor.html)
and [MaterialPropertyBlock.SetColor](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MaterialPropertyBlock.SetColor.html).

Tiles, boundary segments, obstacles, gate floors and posts use the same rounded geometry
and coordinated materials. No aperture or logical location is changed; no decorative
collider is added. The copied tray's mesh/material changes, while its transform/collider
stays intact. Background is light `#F3F5F7`, with dark UI ink `#283747`. Directional
shadows have restrained strength, flat ambient light fills side faces and reflections
remain moderate. No bloom, SSAO, texture download or heavy post-processing is introduced.

The accepted mobile URP asset supports main-light shadows but disables soft filtering;
it remains unchanged. Faint procedural contact shadows supply softness independently of
that setting. Actual lighting/shadow balance must be assessed in Unity and on a device.

## Camera and HUD

The polished camera retains the original transform, orthographic projection, clipping
planes and 6.25 reference size at 9:16. M4 alone adds `PortraitCameraFit`: on a narrower
portrait aspect it increases orthographic size to retain at least the original horizontal
field of view. It never zooms in beyond the reference or changes grid data. The existing
pointer calculation uses that same camera, so projection and dragging remain aligned.
The accepted M3 camera has no such component and is untouched.

The same UGUI HUD, CanvasScaler, font, EventSystem and imported InputActionReferences are
reused. M4 creates no action assets/references or extra input module. The redesigned HUD
has a light rounded top panel, level number/title hierarchy, time and pieces pills,
neutral Restart/Retry and a blue Next button. Text is dark and buttons keep large touch
targets; a lone Retry is centered on Fail/final completion. Vector-generated rounded panels avoid sprite textures. Subtle UGUI shadows add
depth. Safe-area containers handle the top controls and centered result card, while the
result backdrop continues to cover the full screen and block underlying selection.

Win, Fail and campaign completion retain their existing logic/messages, with a supporting
caption and a 160ms visual-only fade/scale reveal. Retry/Next remain the original actions;
the reveal never delays state changes or progression. Existing scenes keep their original
labels because polished formatting is an optional serialized presentation flag.

## Original audio

The six mono **16-bit PCM / 22,050 Hz WAVs** total 67,524 bytes. They are deterministic
original synthesis, not downloaded library sounds. Soft attacks/releases, restrained
harmonics, slight plastic noise and a shared musical vocabulary keep them coordinated.

| File | Approximate duration | Trigger |
| --- | --- | --- |
| Select.wav | 90ms | One successful pointer selection |
| Blocked.wav | 140ms | First block/obstacle/boundary/wrong-gate rejection in a drag gesture |
| Exit.wav | 240ms | Once for each accepted logical block exit |
| UiClick.wav | 65ms | Restart, Retry or Next button activation |
| Win.wav | 560ms | Once when the current session reaches Won, including Level 4 |
| Fail.wav | 430ms | Once when the current session reaches Failed |

One persistent, nonspatial AudioSource uses PlayOneShot at 0.55 volume, with no autoplay,
loop or mixer framework. Generated peaks are below 0.263 before source attenuation and
start/end samples are zero. The cue policy latches blocked feedback for the entire held
gesture, deduplicates exited identities and terminal results, and resets on Retry/new level.
There is no repeating blocked cue every pointer frame. Subscriptions and button listeners
are paired on enable/disable and old level handlers are detached on replacement. Restart
or a new level stops lingering old sounds. There is no music, settings menu or SFX toggle.

After interaction-audio QA, only **Select.wav** and **Blocked.wav** were retuned. Ordinary
successful grid movement produces no cue: Select plays on selection, Blocked plays for
the first rejected move in the gesture, and Exit plays on a successful departure. There
is no separate movement sound to adjust. Exit, UiClick (including Next), Win and Fail
remain byte-identical, and their generator recipes still quantize to the existing PCM
samples exactly. Event timing, deduplication and the shared AudioSource are unchanged.

Select now uses a 280 Hz body with a weak 468 Hz inharmonic resonance instead of the
659-to-569 Hz pitch sweep and brighter second harmonic. Blocked uses a muted 180 Hz body
with a very quiet 283 Hz resonance. Both have a 10 ms attack (previously 6 ms), reduced
gain and faster decay; the overtones decay faster than the body. Two cascaded 650 Hz
low-pass stages soften the small deterministic noise component. This keeps a short
plastic contact character without a sharp broadband click, sustained beep, added bass
layer, reverb or continuous movement audio. The original 25 ms release and clip lengths
are preserved; Blocked's useful sound decays well before the end of its existing clip.

| Retuned file | Frames / actual duration | PCM peak before → after | Peak change |
| --- | --- | --- | --- |
| Select.wav | 1,985 / 90.023 ms | 0.25513 → 0.12772 (4,185/32,767) | −6.01 dB |
| Blocked.wav | 3,088 / 140.045 ms | 0.19501 → 0.08509 (2,788/32,767) | −7.20 dB |

Both files remain mono signed PCM16 at 22,050 Hz, with zero first/last samples, negligible
DC offset and no clipping. Spectral energy above 2 kHz falls from 0.065898% to 0.000097%
for Select and 0.062008% to 0.000167% for Blocked. These are offline waveform measurements;
final perceived loudness and timbre still need a brief listening check in Unity.

Only the two existing WAV payloads, M4SoundDesign.cs and this documentation changed.
The WAV filenames, metadata/GUIDs, theme clip order, feedback references and scene bytes
are preserved. Let Unity import the two changed files (or manually Reimport only those
two if automatic refresh is disabled); **Apply M4 Polish is not required**. Play and
compare repeated selection, normal dragging and first blocked contact, then confirm
Exit and UI/result sounds retain their accepted character.

Targeted checks: runtime, Editor and test assemblies compile against installed Unity
DLLs with warnings treated as errors. The three existing sound-recipe, PCM-write-safety
and cue-deduplication test methods pass under standalone Mono without starting Unity.
The production C# generator wrote only Select and Blocked; all six WAV headers/data and
the runtime GUID links were checked. No other WAV was regenerated. Full-file snapshots
verify all protected scenes, visuals, gameplay/input systems, tests and metadata remain
unchanged. No Unity batchmode, GUI automation, commit or push was used.

## Protected integration points and files

Six existing runtime files receive small compatible changes:

| File | Necessary change |
| --- | --- |
| LevelPresentation.cs | Optional theme, rounded visuals/contact shadows and child feedback, since scene-only styling would be replaced by this runtime factory. Collider construction remains identical. |
| GridBlockView.cs | Selection notification/state for cosmetic children; the accepted property-block behavior remains intact. |
| PointerDragController.cs | Notifications after actual selection and production movement results, needed for accurate audio without guessing from frame polling. Movement calculations/decisions are unchanged. |
| GameplayController.cs | Read-only exit progress and a notification after the existing complete Restart, for cosmetic reset and cue rearming. |
| CampaignController.cs | Notification after replacing its gameplay hierarchy so feedback can detach/attach once. Progression decisions are unchanged. |
| GameplayHud.cs | Optional compact formatting; existing button, terminal and progression logic remains intact. |

New runtime files are in `Runtime/Polish`: SoftGeometry, SoftToyTheme, PolishCueState,
PolishedBlockVisual, PolishFeedback, RoundedPanel, SafeAreaPanel, PolishedHudEffects,
ResultReveal and PortraitCameraFit. New Editor files are M4Polish, M4HudStyling,
M4SoundDesign and M4Validation. New tests are M4PolishTests and M4EditorSetupTests.
The shader, WAVs and source/folder metadata are included.

Every accepted scene, four LevelDefinition assets, old test, old Editor utility, material,
Input Actions asset, package/assembly configuration, project setting and existing metadata
is preserved. Board/exit/session/campaign model source is unchanged.

## Setup preservation

Apply starts with the existing saved-scene/compilation/Play/Prefab guard. Existing M4
scene assets are inspected and preserved without restyling. The targeted HUD repair below
is saved only when required components or decorative raycast flags need correction. Existing resource
assets are reused, not reset to default colors or regenerated. Dirty resources, conflicts,
missing references and invalid assets stop setup with diagnostics. Individual newly created
resources are saved without globally saving user assets. Valid resource assets from a
failed setup are preserved for a safe retry. Only a newly owned failed scene copy can be
closed/deleted; the previous active scene is restored. No scene/prefab YAML is authored.

## Verification and required manual QA

Actual standalone checks: runtime, Editor and test assemblies compile against installed
Unity/package DLLs with warnings treated as errors. Runtime uses no Editor defines or
UnityEditor references. Managed NUnit results: **69 passed, zero failed/skipped/inconclusive**:
62 accepted production-model cases plus seven M4 geometry/footprint/UI-math/cue/PCM cases.
The expected full Unity suite is **86 cases** (75 accepted plus 11 M4 cases); its 17 native
Editor-dependent cases were not run here. Transient outputs are in ignored `Temp/M4Validation/`.

Static checks cover original-file hashes, accepted levels/scenes/tests, metadata/GUIDs,
assembly/JSON boundaries, generated WAV headers/data/peaks, mesh geometry, shader source
dependencies and unchanged 9:16 camera framing with the new top HUD. Native material,
mesh, theme and scene persistence is covered by the prepared Editor integration tests.
C# compilation does not compile every HLSL/platform variant or prove rendered quality.

Manual Unity QA:

1. Apply/Open M4, then Play at 9:16. Inspect pastel blocks, the L's empty corner, narrow
   seams, tray/grid contrast, obstacles, complete gate openings and gentle lighting.
2. Try taller portrait aspect ratios and device safe-area simulation: check title, timer,
   count, touch targets, result card and full exit framing. Dragging must remain aligned.
3. Check select emphasis, blocked motion/wrong-color gates, fast dragging and full exits.
   Colliders/occupancy must remain the accepted behavior and visual children must reset.
4. Hear all six effects. Hold a blocked drag and repeat terminal frames: no cue spam or
   duplicate result sound. Check volume and clipping; Retry/Next must not leave old sounds.
5. Complete all four accepted solutions, verify Next/final completion, then Fail/Retry and
   Restart during selection/exit. Temporarily shorten only an M3 level timer for Fail QA
   if needed, then restore the approved value. Setup must not reset authored data.
6. Check result fades, UI mouse/touch activation and no underlying block drag from a button.
7. Exit Play, save approved M4 tuning, Apply twice and Validate. Run all 86 EditMode cases,
   smoke-test original Campaign and older demos, and inspect Console/shader errors.

Native asset/scene creation, HLSL compilation, rendered art, audio playback, real input,
Play Mode cleanup and device performance remain unverified until that QA. The existing
four solutions and gameplay semantics are preserved. No release build, next milestone,
commit, push, rebase or tag was performed.

## Targeted CanvasRenderer correction

The generated Time Pill, Pieces Pill and Result Card had a RectTransform and RoundedPanel
but no serialized CanvasRenderer. In installed UGUI 2.0.0, Graphic requires RectTransform
only; Image and Text declare CanvasRenderer separately. RoundedPanel omitted that dependency.
The other five rounded panels inherited renderers from their original Images. This is an
Editor construction defect; the runtime level factory does not construct UI. The default
raycastTarget on the new panels exposed the missing renderer through GraphicRaycaster.

RoundedPanel now declares RequireComponent(CanvasRenderer), including for runtime AddComponent
calls. The Editor replacement helper also supplies the renderer before adding the graphic.
Decorative panels ignore raycasts; Restart, Retry, Next and the modal result backdrop retain
their raycast behavior. The accepted gameplay UI-blocking code, raycaster, EventSystem,
Input System module and action assets are unchanged.

The existing scene needs one targeted repair: leave Play Mode, let scripts compile, save
your scene edits, then run **ColorSlide > Setup > Apply M4 Polish**. Apply refuses dirty
scenes. It first repairs and validates an isolated preview, then adds missing renderers
(including inactive result graphics) and clears decorative RoundedPanel raycast targets
in the existing M4 HUD through Unity Editor APIs. It saves that scene without rebuilding
the hierarchy, changing its GUID, replacing component references or resetting authored
colors, radius, layout, camera, audio or gameplay values. Only scenes opened by the repair
are closed; the previous active scene is restored. A second Apply makes no saved changes.
Reopen PolishedCampaign after repair, then Validate M4 Polish before Play. Code changes
alone do not repair already serialized component lists; do not delete/recreate the scene.

The new ProductionHudConstructionAndTargetedRepairSurviveSaveAndReload EditMode test uses
the real M4HudStyling.Apply on an owned temporary copy of Campaign. It checks runtime-style
RoundedPanel creation, every active/inactive Graphic's renderer, the Time Pill timer parent,
all eight rounded panels, button targets, the single canvas/EventSystem/input module and
all accepted persistent UI action references. It exercises repair of a renderer-less base
Graphic and a legacy decorative raycast flag, verifies tuning/references survive repair,
checks a second repair is a no-op, then saves/reopens the copy and checks again. Cleanup
closes/deletes only the owned copy in finally and restores the prior active scene.

The targeted fix is compiled against the installed Unity/package DLLs for runtime, Editor
and tests with warnings treated as errors. Static snapshots verify all existing scene,
asset and metadata bytes remain unchanged by this source correction. Native repair,
save/reload and the new regression test require Unity Editor execution; no batchmode or
computer-use automation was used. After repair, clear Console, Play, move the mouse across
the HUD and try touch input. Check no missing-renderer errors, unchanged appearance,
Restart/Retry/Next clicks and no underlying block drag from those controls. Show both
Win/Fail result panels, reopen the scene, repeat Validate/Apply and run the EditMode suite.
