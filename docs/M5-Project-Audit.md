# ColorSlide3D — M5-A final project audit

**M5-C final media status, 2026-10-21:** the approved 2:29.6 continuous gameplay recording is published as [ColorSlide3D — Full Gameplay Showcase](https://github.com/Alcasin/ColorSlide3D/releases/tag/portfolio-showcase-v1). The README links to the Release and includes six genuine screenshots. See the [media report](M5-C-Portfolio-Media.md). This is a status update to the historical M5-A audit below; it does not constitute a new compilation, test run or resolution of the recorded configuration/licensing findings.

Audit date: **2026-10-20**. Baseline: **`9102737` on `main`**, with a clean working tree before M5-A. The configured remote is `https://github.com/Alcasin/ColorSlide3D.git`; this audit examines the local checkout, not the live GitHub page.

**Outcome:** the accepted eight-level Editor prototype is suitable for portfolio capture preparation. Static compilation and managed tests pass. The project is **not declared globally reference-clean or Android-release-ready**: pre-existing template/render-settings references need review, media has not been captured, and repository-wide licensing is not documented. No production code, accepted asset, scene or project/package setting was changed to obtain this result.

## Evidence and validation boundaries

| Check | Result / evidence |
| --- | --- |
| Native Unity suite | Owner reports **243/243 EditMode cases passed**, zero failures, at the accepted checkpoint. No native run was performed in M5-A. |
| Static C# compilation | Runtime, Android-defined Runtime, Editor, Tests and the managed verification runner compile against installed Unity/package assemblies, C# 9, warnings as errors. |
| Managed NUnit run | **179 passed, 0 failed, 0 skipped** via `Tools/Verify-M46.ps1`. The remaining 64 cases need native Unity services. |
| Discovery | **243 cases across 27 fixtures**; no test changes or new cases. |
| Capture choreography | Temporary managed replay through production `PuzzleSession` confirms Level 1's two exits/Win and Level 2's block → lateral clearance → Amber exit sequence. This adds no suite cases and does not simulate native pointer input. |
| Asset metadata | 168 asset/folder `.meta` files: no duplicate asset GUIDs, missing metadata for importable files, or orphan metadata. Five hidden `.gitkeep` files intentionally have no Unity metadata. |
| Serialized scan | 62 YAML assets/settings examined, 1,129 GUID occurrences checked including metadata definitions, 258 `m_Script` entries checked. All nonzero local `fileID` references resolve within their serialized documents. External-reference exceptions are listed below. |
| Input / assemblies | Both `.inputactions` files and all three `.asmdef` files parse. Action/binding IDs are unique within each asset, bindings name existing actions, and assembly references are not duplicated. |
| FeedbackCampaign ownership | One serialized CampaignController, LevelPresentation, GameplayHud, PortraitCameraFit, EventSystem, InputSystemUIInputModule, PolishFeedback, TimerUrgency, ResultReveal and AudioSource. |
| Protected content | SHA-256 baseline comparison confirms every tracked file except the intentionally rewritten README is unchanged. New tracked candidates are documentation only. |
| Git hygiene | Generated Library/Temp/Logs/UserSettings/build/recording paths are ignored and are not tracked. Assets and ProjectSettings are retained. Diff whitespace and new documentation links are checked. |

Reproducible compilation/managed validation: run `Tools/Verify-M46.ps1` after the project has imported its dependencies. It invokes the bundled C# compiler/Mono runner, not the Unity Editor. Local outputs are in ignored `Temp/M46Validation/out/` (`discovered.xml`, `managed.xml`, `solver-report.txt`). This audit's metadata/reference/WAV scan and baseline records are in ignored `Temp/M5Audit/`; these are local diagnostics, not committed deliverables or native test artifacts.

Static text scans cannot prove imported sub-asset `fileID` identity, MonoScript class loading, shader compilation, platform stripping, actual rendered frames, audio-device behavior or touchscreen behavior. The existing native tests and owner-confirmed manual QA support accepted Editor behavior; they do not establish physical Android results.

## Architecture and verified portfolio claims

Paths in this section are relative to `Assets/_ColorSlide/`.

| Area | Inspected implementation | Findings |
| --- | --- | --- |
| Puzzle rules | `Runtime/Board/GridBoard.cs`, `GridBoard.Exits.cs`, `ExitGate.cs` | Exact occupied footprints, obstacle occupancy, cardinal cell-by-cell movement, swept collision and whole-footprint color/aperture exit validation. No Rigidbody simulation decides puzzle moves. |
| Level/session state | `Runtime/Gameplay/LevelDefinition.cs`, `PuzzleSession.cs` | Serializable level data validates through the real board. Session owns Ready/Playing/Won/Failed, countdown and fresh restart snapshots. Final accepted exit wins over a same-frame timeout. |
| Campaign | `CampaignSession.cs`, `CampaignController.cs`, `FeedbackCampaignLevels.cs` | Ordered, in-memory progression; explicit win-gated Next; Restart/Retry stay on the selected level. No save-file/PlayerPrefs progression system was found. |
| Presentation ownership | `LevelPresentation.cs`, `GameplayController.cs` | Stage an inactive replacement, configure resources/session, retire the previous hierarchy, then activate and notify HUD/feedback. Drag, input clone and owned temporary effects are cleaned up on disable/replacement. |
| Pointer input | `Runtime/Input/PointerActions.cs`, `Runtime/Interaction/PointerDragController.cs` | Separate position/press actions, mouse or primary touch, raycast picking and camera-plane projection. A drag stays with its pressing device; cloned actions own enable/disable. UI raycasts prevent selecting blocks through controls. No multi-touch gesture claim. |
| UI input | `Editor/M2GameplayDemo.cs`, saved `Scenes/FeedbackCampaign.unity` | UI module uses nonzero imported references from the template `InputSystem_Actions.inputactions` UI map. Pointer gameplay uses the separate ColorSlide asset. Native tests cover persistence after import/save/reload. |
| Camera / HUD | `Runtime/Polish/BoardFraming.cs`, `PortraitCameraFit.cs`, `SafeAreaPanel.cs` | Data-driven orthographic fit uses projected tray/playable bounds, aspect, HUD and safe area. Target 90% of safe width; 90.5% tolerance limit. Control default 1.25, range 1.00–1.25, without stretching cells or scaling the logical board. |
| Feedback | `SoftCubeBurst.cs`, `SoftBurstMotion.cs`, `GateGlow.cs`, `PolishedBlockVisual.cs`, `TimerUrgency.cs`, `ResultReveal.cs` | Ten cosmetic fragments, 0.18 s outward + 0.30 s scatter; matching-gate pulse 0.30 s; blocked contact 0.18 s; urgency at 15 s; brief win decoration. Material property overrides and child visual transforms are restored; effects do not modify puzzle decisions. |
| Audio | `PolishFeedback.cs`, `PolishCueState.cs`, `SoftToyTheme.cs`, `Editor/M4SoundDesign.cs` | Six generated cues, one nonspatial AudioSource, PlayOneShot, paired event subscriptions and gesture/exit/result deduplication. No continuous movement cue or music system. |
| Editor tools | `Editor/CampaignPlayModeShortcuts.cs`, `PuzzleSolver.cs`, solution files | Focused Game View keys 5–8 select advanced levels via campaign APIs. Plus/Equals/Minus and numpad equivalents control temporary zoom; Ctrl/Alt/Meta suppress zoom. Editor-only APIs/state are absent from the Android-defined assembly. Authoring/setup tools are retained. |

The [unified framing report](M4.6-Unified-Camera-Framing.md) and retained regression tests cover 1080×1920, 1080×2400 and 720×1280 plus synthetic safe-area constraints. The reported ~90% widths are geometric results, not physical-device measurements. Successfully removed blocks may leave the viewport during their accepted departure; cosmetic burst-origin fitting preserves readable fragments without changing the logical exit end anchor.

## Campaign and difficulty evidence

The saved FeedbackCampaign references the four `LevelDefinition` assets under `Levels/`. `FeedbackCampaignLevels.Complete` appends the four source-authored advanced/master levels by unique identity; this explains why eight gameplay levels do not require eight `.asset` files or eight scenes.

| Level | Source | Grid | Time limit | Verified design emphasis |
| --- | --- | --- | --- | --- |
| Basic Exits | `Levels/Level01.asset` | 6×8 | 180 s | Two single-cell blocks, two matching exits |
| Make Space | `Levels/Level02.asset` | 6×8 | 180 s | A blocking route and rectangular footprints |
| Irregular Footprints | `Levels/Level03.asset` | 6×8 | 240 s | L-shape, obstacle column, route preparation |
| Combined Constraints | `Levels/Level04.asset` | 6×8 | 300 s | Mixed shapes and multiple route dependencies |
| Traffic Lock | `Runtime/Gameplay/AdvancedCampaignLevels.cs` | 7×9 | 420 s | Crowded transfer lanes; stored 21-action witness |
| Grand Escape | Same advanced catalog | 8×10 | 600 s | Separated regions; stored 32-action witness |
| Chain Reaction | `Runtime/Gameplay/MasterCampaignLevels.cs` | 8×11 | 720 s | Dependent rearrangements; stored 41-action witness |
| Final Lockdown | Same master catalog | 8×11 | 900 s | Denser combined constraints; stored 64-action witness |

The managed run replays advanced/master witnesses using `PuzzleSession`, checking accepted moves and final Won state. Bounded authoring searches also return valid solutions. Traffic Lock's A* search can prove its 21-action result in the defined slide-action metric; weighted master searches and locally shortened witnesses do not prove globally shortest solutions. Portfolio text therefore says “solver-verified” rather than claiming an optimal solution for every puzzle.

## Reference findings requiring follow-up

No unresolved GUID, zero script entry or dangling local reference was found under **`Assets/_ColorSlide/`**, or in the saved scene documents. That is a static result, not a fresh native Inspector pass.

The broader repository scan found **12 unresolved nonzero GUIDs** outside the custom project subtree, plus **five zero `m_Script` entries** in a volume-profile asset:

| Existing file | Exact finding | Interpretation and safe next action |
| --- | --- | --- |
| `Assets/Settings/DefaultVolumeProfile.asset` | Four unresolved component-script GUIDs: `OutlineVolumeComponent`, `OasisFogVolumeComponent`, `TestAnimationCurveVolumeComponent`, `TestVolume`. The first two remain listed in the profile's `components`; the last two are unlisted sub-assets. Five additional zero-script sub-assets name Render Pipeline Editor test components and are also unlisted. | The profile is referenced by `UniversalRenderPipelineGlobalSettings`. These are real serialized anomalies, not a ColorSlide scene-script failure. URP `VolumeProfile.OnEnable` removes null components in memory, which may explain accepted rendering, but no new native load/Console result is claimed. Inspect this profile in Unity before claiming repository-wide reference cleanliness; do not delete or regenerate render settings during capture. |
| `Assets/Settings/PC_Renderer.asset` | Seven unresolved references inside the legacy `probeVolumeResources` block: four probe-debug shaders, debug mesh, debug texture and blend-state compute shader. | Installed URP 17.3 source marks this resource container obsolete in `Runtime/Deprecated.cs` and moves resources to GraphicsSettings. Treat these as legacy serialized reference debt, not proof that accepted gameplay rendering is broken. Review only in a separately authorized rendering cleanup. |
| `Assets/TutorialInfo/Scripts/Readme.cs.meta` | One unresolved icon texture GUID (`a186f8a87ca4f4d3aa864638ad5dfb65`). | Template tutorial Inspector icon, not gameplay content. No production fix required for capture. |

The five zero-script volume sub-assets are **not five missing scripts on scene GameObjects**. The two listed unresolved profile components are the stronger follow-up concern. None of these reference issues was created or repaired in M5-A, and rendering consequences cannot be inferred solely from YAML. Stop and report if Unity shows fresh errors or changed appearance during capture preflight.

## Unity, Android and repository configuration

- Unity is pinned by `ProjectVersion.txt` to **6000.3.8f1**. Manifest versions are URP **17.3.0**, Input System **1.18.0**, Test Framework **1.6.0**, UGUI **2.0.0**. No package changes were made.
- Player settings specify portrait, a 1080×1920 reference size, linear color and the new Input System (`activeInputHandler: 1`). Android is configured for IL2CPP/ARM64, minimum SDK 25 and automatic target SDK. These are inspected values, not a validated Android build or current store-policy assessment.
- Android's default quality profile is Mobile; PC and Mobile pipeline/renderer assets are retained. The Editor's accepted visuals must not be retuned to force a recording frame rate.
- **Build Settings enables only `Assets/Scenes/SampleScene.unity`.** FeedbackCampaign must be opened directly for Play Mode. An unreviewed build from current defaults would not start the portfolio game.
- Company name is still `DefaultCompany`; Android application ID remains `com.UnityTechnologies.com.unity.template.urpblank`, version `0.1.0`, code 1, with no custom signing configured. These are deferred release-preparation gaps, not M5-A fixes.
- The bundled Android SDK directory exists on this machine, but no toolchain execution, platform switch, build, device installation, performance profile or store submission was performed or verified.
- Runtime, Editor and Tests have separate assembly definitions. Editor/Test assemblies target Editor only. Standalone Android-defined compilation plus metadata checks confirm the known level-selection and camera-override QA APIs/state are excluded. This is not a substitute for an eventual IL2CPP build.
- No TODO/FIXME/HACK marker, direct abstract `Graphic` construction, runtime `SendMessage` lifecycle dispatch, legacy keyboard polling, save-progression claim or capture hook was found in custom production code. Existing error diagnostics and Editor zoom logs are intentional. Test-only reflection helpers exercise cleanup bodies; they are not shipped behavior.
- `Assets` includes seven milestone scenes plus template SampleScene. Earlier scenes and safe authoring/validation menus are useful history and remain intact. `.gitignore` and `.gitattributes` appropriately retain Unity text/assets/metadata and exclude generated data; no cleanup deletion was necessary.

## Assets and licensing

The rounded mesh/contact geometry has source in `Runtime/Polish/SoftGeometry.cs`; the six WAVs have deterministic synthesis source in `Editor/M4SoundDesign.cs`. Each inspected WAV is mono, uncompressed 16-bit PCM at 22,050 Hz, with peak amplitude between **0.085086 and 0.262215**. Clip names/order resolve through `SoftToyTheme.asset` to Select, Blocked, Exit, UiClick, Win and Fail. They were read only, not regenerated.

HUD construction reuses Unity's built-in `LegacyRuntime.ttf`. The repository also retains Unity template icons, render assets and package dependencies. No imported commercial art/audio pack, externally downloaded music, standalone font file or gameplay-media set was found in the inspected Assets tree. Source generators are provenance evidence; they are not a guarantee of rights to every repository item or permission to relicense Unity/package content.

There is **no repository-wide LICENSE or separate attribution ledger**. The owner needs to choose an appropriate source/media reuse policy and verify any external provenance before granting reuse rights. No license was invented or added, and public text does not claim that every asset is independently authored or freely reusable. The remaining volume-component names are serialized references; their presence alone does not establish that third-party source/assets were imported.

## Genuine blockers and deferred work

| Item | Effect on next stage |
| --- | --- |
| No real portfolio media | Blocks a finished media-backed README; six PNG subjects and a 56-second showcase are planned. Placeholder paths are plain text, not broken downloads/images. |
| No configured high-resolution recorder | M5-B needs a tested render-source capture workflow. Official Recorder in an isolated capture copy is justified by full-resolution Game View + overlay HUD capture; no dependency is added to the accepted project in M5-A. |
| Listed unresolved volume-profile components | Prevents a blanket “no broken references” audit claim. Check native Console/appearance before capture; separately authorize any asset cleanup if needed. No demonstrated new gameplay blocker is inferred. |
| Undocumented source/media licensing | Publication/reuse documentation decision remains with the owner. |
| Template build scene / identifiers; no device QA | Blocks an Android-release-ready claim. These do not block Editor footage and are outside this task. |

## M5-A deliverables and M5-B handoff

Changed **README.md** and created **this audit**, [the demo/screenshot plan](M5-Portfolio-Demo-Plan.md), and [CV/portfolio/interview descriptions](M5-Portfolio-Descriptions.md). Historical milestone reports are preserved as historical evidence; the README now points to the current entry scene and current status.

For M5-B, prepare an isolated capture copy and compatible official Recorder, check its import/Console and accepted appearance, then have the user perform the plan's native recording quality check, real gameplay takes and six screenshot selections. Record any fresh volume-profile warnings for review instead of changing the accepted render assets. If a dependency is added to the capture copy, validate that copy before relying on its footage; the checkpoint's test result does not automatically certify a changed capture environment.

Only after media review and a separate instruction should footage be edited, final media links added, or publication/commits considered. M5-A used no Unity batchmode or GUI automation, performed no Android release work, made no commit/push, and stops before M5-B execution or M5-C.
