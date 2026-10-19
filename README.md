# ColorSlide3D — M0 foundation

Current gameplay milestone: **M4.6 Phase B**, eight levels in the existing `FeedbackCampaign` scene.
Allow Unity to compile and press Play; no Apply step is needed. Editor Game View keys **5–8** select the four advanced levels.
See [master challenge implementation, solutions and QA](docs/M4.6-Phase-B-Master-Levels.md) and the [earlier M4.6 report](docs/M4.6-Advanced-Levels.md).
Levels 7–8 now default to **1.25x zoom**. Focus Game View and use **Equals/Plus / Minus** (or numpad equivalents)
to adjust 1.00–1.25x live; see [zoom configuration, measurements and QA](docs/M4.6-Adjustable-Zoom.md).

Unity **6000.3.8f1 LTS**, Universal 3D/URP, intended for Android portrait.
M0 prepares the project and a 3D rendering scene. Gameplay is outside this milestone.

## First Unity Editor QA

1. Open this project in Unity 6000.3.8f1 and allow import/compilation to finish. Save any
   open scene edits. No setup code runs automatically on import.
2. Run **ColorSlide > Setup > Apply M0 Foundation**. This creates
   `Assets/_ColorSlide/Scenes/Gameplay.unity` and
   `Assets/_ColorSlide/Materials/BoardPlaceholder.mat` through Unity APIs, then applies
   Portrait and the 1080 × 1920 reference resolution through `PlayerSettings` APIs.
3. Run **ColorSlide > Setup > Open Gameplay**. This opens Gameplay alone after checking
   that all currently loaded scenes are saved. Choose a **1080 × 1920** fixed resolution
   or **9:16** aspect in Game View; the reference resolution does not select a Game View
   preset automatically. Verify a lit neutral board with visible thickness and a dark
   background, viewed from above at a slight angle. There should be no pink material.
4. Run **Apply M0 Foundation** again, then **Validate M0 Foundation**. Confirm the scene
   has one camera, one directional light and one board, with no changed authored values.
5. In **Window > General > Test Runner > EditMode**, run **ColorSlide.Tests.EditMode**
   (five tests). Setup must have succeeded, and open scenes must be saved first.
6. Check the Console. Actual Editor compilation, visual QA, and Test Runner results are
   manual acceptance checks; static compilation is not a substitute.

No Input Handling change or restart is currently required: the inspected project already
uses **Input System Package (New)** (`activeInputHandler: 1`). If changed later, use
**Edit > Project Settings > Player > Other Settings > Configuration > Active Input Handling >
Input System Package (New)** and accept Unity's requested Editor restart. Setup reports a
disabled backend and does not modify undocumented serialized PlayerSettings fields.

## Project layout and assemblies

All new Unity assets live under `Assets/_ColorSlide/`:

| Folder | Purpose |
| --- | --- |
| `Runtime/Input` | `PointerActions`, a small typed accessor for the authored action map |
| `Editor` | Explicit setup and read-only validation commands |
| `Tests/EditMode` | Configuration, scene, non-mutating validation, repeatability and unsaved-edit tests |
| `Input` | `ColorSlide.inputactions` |
| `Scenes`, `Materials` | Unity-created Gameplay scene and placeholder material |
| `Prefabs`, `Art`, `Audio` | Reserved asset locations; no content or systems yet |

`ColorSlide.Runtime` references `Unity.InputSystem` and has no Editor dependency.
`ColorSlide.Editor` is Editor-only and references Runtime, Input System and URP.
`ColorSlide.Tests.EditMode` is Editor-only, marked as a test assembly, references the two
project assemblies, Input System and the Unity test runners, and explicitly references NUnit.
Namespaces follow `ColorSlide`, `ColorSlide.Input`, `ColorSlide.Editor` and
`ColorSlide.Tests.EditMode`.

`PointerActions` keeps one useful runtime assembly available without a placeholder
manager. It only resolves the existing `Pointer` map and its named actions. It does not
clone or enable the asset, register callbacks, or process input. Nothing is attached to
the scene to run gameplay logic.

## Input and scene conventions

The `Pointer` action map contains:

| Action | Type | Binding |
| --- | --- | --- |
| `Position` | Value / Vector2 | `<Pointer>/position` |
| `Press` | Button | `<Pointer>/press` |

Generic pointer controls cover a mouse's primary button and a touchscreen's primary
touch. The Button action retains default press/release behavior; future code can use its
press and release state or phases. This is preparation for one pointer interaction,
without multi-touch gestures, selection, dragging or raycasts. The imported asset stays
disabled until future code explicitly enables it. The original template input asset and
its project-wide assignment are preserved; M0 does not replace that assignment or add
PlayerInput/UI modules. See Unity's [pointer documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.18/manual/Pointers.html).

World **X** is board horizontal, **Z** is board vertical, and **Y** is height. The board is
a cube scaled to a thin slab, with its top at Y=0 and its collider removed. Its initial
6 × 8 × 0.25 dimensions are editable visual placeholders, not a grid or level definition.
The fixed orthographic camera's transform and size are ordinary Inspector values. The
material uses URP/Lit and a directional light supplies basic lighting; no Global Volume
or camera controller is needed in the new scene.

## Setup safety

- Setup refuses Play Mode, compilation, Prefab Mode and any unsaved open scene. It
  reports the reason and makes no changes in those cases. Save/discard edits explicitly
  and rerun the command.
- Existing Gameplay and board material assets are checked before any writes. Valid
  assets are reused without being resaved or reset. Incompatible path contents, missing
  required objects or broken framing cause a diagnostic instead of automatic repair.
  Fix the reported conflict in Inspector; do not delete customized assets to rerun setup.
- Scene creation uses a temporary additive scene and restores the user's active scene
  and loaded scene arrangement. Validation opens and closes an isolated preview scene
  and checks the **saved** scene; save authored changes before validating them.
- Running setup again reapplies only the three required project settings (Portrait,
  reference width and height). Other PlayerSettings, URP assets, build target, build scene
  list and template files remain unchanged. The explicit Open Gameplay command opens
  the saved scene alone for QA. SampleScene remains in the original build scene list;
  build-profile configuration is deferred.
- No menu performs an Android switch, build, package install or destructive cleanup.
  File creation can still fail due to disk/permission problems; partial newly created
  assets are retained for inspection, and setup reports the error.

## Environment and Git

Installed packages: URP **17.3.0**, Input System **1.18.0**, Test Framework **1.6.0**.
The original template includes PC and Mobile pipeline/renderer assets, with Mobile the
Android quality default. These and all package dependencies were preserved.

Android Build Support and the bundled SDK, NDK and OpenJDK were found for this Editor.
No device connection, SDK execution, platform switch or Android build has been verified.
On a different machine install **Android Build Support**, **Android SDK & NDK Tools**,
and **OpenJDK** through Unity Hub for this Editor version before Android build QA.

A local Git repository was initialized at this project root after confirming there was
no parent repository. `.gitignore` excludes generated Unity/IDE/build data while retaining
assets and `.meta` files. `.gitattributes` treats Unity YAML as text and common imported
media as binary. No UnityYAMLMerge driver, LFS dependency, remote, commit or push was
configured. Make your first checkpoint only after successful Unity QA.

Static verification and its limits are recorded in [docs/M0-Validation.md](docs/M0-Validation.md).
