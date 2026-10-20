# ColorSlide3D — Portfolio demo and screenshot plan

**M5-C update, 2026-10-20:** the owner chose the approved **2:29.6 continuous gameplay recording** instead of the proposed 56-second edit below. Six screenshots have been extracted from that MP4 at native resolution. The [final media report](M5-C-Portfolio-Media.md) contains the actual filenames, timestamps and manual GitHub Release workflow. The MP4 stays outside the main repository. The following M5-A shot list and image-sequence workflow are historical proposals, not additional required capture or editing work.

M5-A preparation, 2026-10-20. No footage, screenshots, recorder assets or packages were created by this stage. Capture the accepted project at checkpoint `9102737`; preserve its levels, camera, HUD, lighting, audio and animation settings.

## Deliverable

Create a **45–60-second vertical showcase**, with a planned duration of **56 seconds**, at **1080×1920**. Aim for 60 FPS when the recording machine sustains it. Use actual Play Mode input and original game audio. This is an edited gameplay showcase with clearly separated level excerpts, not a continuous speedrun or a performance benchmark.

## 56-second shot list

Times below are positions in the finished video, not limits on how long recording or solving must take. Keep 1–2 seconds of handles around each raw take.

| Time | Source | Record this action | What should read clearly |
| --- | --- | --- | --- |
| 0–6 s | Level 1 — Basic Exits | Begin dragging Blue immediately, then move it north through its matching gate | Mouse/primary-pointer control, grid motion, matching gate, glow and burst; brief title over unused background |
| 6–13 s | Same Level 1 take | Exit Amber south, allow the final burst and win celebration to finish, then click Next | Honest win-to-next sequencing, original UI sound, Level 2 title appearing |
| 13–20 s | Level 2 — Make Space | Drag Blue north until Amber blocks it, release, slide Blue left into the free lane, then exit Amber south | A real obstruction, restrained contact feedback, and a legal clearance sequence |
| 20–28 s | Level 5 — Traffic Lock | Show two useful legal moves through the transfer lanes; hold the full board briefly | Larger 7×9 layout, obstacles, rectangular and L-shaped footprints |
| 28–35 s | Level 6 — Grand Escape | Show a deliberate repositioning in its separated board regions | 8×10 composition, gate readability and consistent apparent tray width |
| 35–44 s | Level 7 — Chain Reaction | Show consecutive dependent moves, with the whole board and HUD visible | Strategic rearrangement on an 8×11 board; caption “Level 7 — gameplay excerpt” |
| 44–53 s | Level 8 — Final Lockdown | Show a short sequence of legal rearrangements, then a successful exit if it occurs naturally in the chosen take | Crowded final challenge; caption “Level 8 — gameplay excerpt,” not a claim that it was solved in nine seconds |
| 53–56 s | Same Level 8 gameplay | Settle on a readable board state | Small closing title: “ColorSlide3D · Unity 6 · C#”; leave the HUD and gates uncovered |

The first two shots can be one continuous take. Blue's north path and Amber's south path in Level 1 are clear in the authored initial layout. In Level 2, Amber crosses Blue's north route. After Blue stops at grid anchor (2,2), slide it left to (0,2) before dragging Amber south through its two-cell gate. Dragging Amber south immediately would still be blocked by Blue. These are grid anchors for rehearsal, not on-screen labels or scripted moves.

Both opening sequences were replayed successfully through the actual `PuzzleSession` model in a temporary managed diagnostic, using the M3 catalog checked against the saved first-two-level layouts. This verifies legal moves and the Level 1 Won state; it does not constitute pointer, animation, recording or native Editor QA.

Rehearse advanced excerpts using `Assets/_ColorSlide/Editor/AdvancedLevelSolutions.cs`, `MasterLevelSolutions.cs`, or the diagrams in the [master-level report](M4.6-Phase-B-Master-Levels.md). Execute moves manually through real input. Witnesses are rehearsal references, not a new playback tool. Record a longer solve if an exit takes more preparation, then select an honest excerpt.

Win celebration is covered in Level 1. An optional final-level completion take may replace the closing hold only after a real Level 8 solve; keep its actual level identity visible. Do not use an earlier level's win card as if it completed Level 8.

## Existing capture capability and recommended workflow

The manifest contains no `com.unity.recorder`, no capture script was found, and no gameplay video or screenshot set is present. Unity's screen-capture module is installed, but it is not a configured video recorder. No additional runtime system is needed.

For M5-B, use official **Unity Recorder** in an isolated local capture copy of the accepted project. This addresses the demonstrated need to record rendered frames and the overlay HUD at full resolution on a display too small to show a 1080×1920 Game View at 1:1. Prepare that copy from `Assets`, `Packages` and `ProjectSettings`, retaining all `.meta` files and excluding generated caches. Keep the accepted working project and its package files unchanged. Copy/package preparation can be handled when M5-B is authorized; it was not performed in M5-A.

Unity's [6000.3 package documentation](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.recorder.html) currently lists Recorder **5.1.7** as released for this Editor line. In the capture copy, use **Window > Package Management > Package Manager > Unity Registry > Recorder**, choosing the released compatible version. Do not upgrade Unity or install paid capture tools, FFmpeg integrations or other packages for this plan.

### Configure the capture copy

1. Open `Assets/_ColorSlide/Scenes/FeedbackCampaign.unity`. Let import finish and keep authored scene edits saved beforehand. No milestone Apply/Validate command is needed.
2. Set Game View to **Fixed Resolution, 1080×1920**, not just a 9:16 aspect ratio. Turn off Game View Gizmos. Keep the approved camera control at its **1.25** default; no camera, root-scale, lighting or HUD adjustment is required.
3. Open **Window > General > Recorder > Recorder Window**. Add one **Movie** recorder with **Source: Game View** and **Output Resolution: Custom, W 1080, H 1920**. Choose **Unity Media Encoder**, **H.264 MP4**, **High** quality and **Include Audio**. Use manual recording and unique take names under `Recordings/M5/` in the capture copy. See Unity's [Movie Recorder reference](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/RecorderMovie.html).
4. Choose **Playback: Variable**, **Max FPS: 60**, and **Render Frame Step: 1** for interactive input. Unity recommends variable playback for gameplay; see [Recorder frame-rate settings](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/RecorderWindowRecordingProperties.html). This project advances its timer and effects with `Time.unscaledDeltaTime`, so check real-time pacing rather than assuming offline frame-stepped capture preserves it.
5. Enter Play Mode, start recording and return focus to Game View before playing. Disable Recorder's exit-Play-Mode-on-stop option for repeated takes in one session; otherwise stopping will reset progress. Use distinct take numbers so recordings cannot overwrite good takes.

**Capture Game View, not a camera-only source.** The HUD uses Screen Space Overlay canvases; record the composed Game View for the full interface. Recorder reads rendered frames. The 0.32× preview can remain useful for operating the Editor, but a desktop crop of it contains only roughly **346×614 pixels** before enlargement. Increasing a desktop recording's output size cannot recover detail. Check that the encoded file is 1080×1920 rather than trusting the preview or a preset named “1080p.”

Recorder can route audio into the file instead of system output during capture; inspect the saved take if the Editor becomes silent. The project has stereo output and one nonspatial feedback AudioSource. Preserve its accepted volume and six clips. Do not add a microphone, music or replacement effect track.

### Quality check before full takes

Record a five-second take containing a drag, blocked gesture and exit. Play it outside Unity and check encoded dimensions, readable HUD, full tray/gates, orientation and audio sync. Over a separate 10-second sample, compare wall time, HUD countdown and file duration; the timer should advance approximately ten seconds while the session is Playing. Do not change gameplay timing to accommodate recording.

Inspect motion for missed/held frames and listen for dropouts. “60 FPS” is a target, not a measured project performance claim. If 60 is unstable, try **Variable, Max FPS 30** and repeat the check at the same resolution and visual quality. Keep raw pacing within each excerpt. A later constant-frame-rate export must preserve timestamps and duration; do not speed up the timer or interpolate fake movement.

Focus Game View and press **5**, **6**, **7** or **8** before advanced takes; top-row and numpad keys work. Do not hold Ctrl, Alt, Meta or Shift for navigation. These shortcuts rebuild through normal campaign transitions without saving progression. Trim the jump and label the new excerpt. To return to Level 1, stop and re-enter Play Mode; no shortcut for Levels 1–4 is implemented.

## Screenshot plan

Capture **six PNGs at 1080×1920**, retaining the whole-board composition and HUD. Filenames below are reserved destinations under `docs/media/screenshots/`, not existing image links.

| File | Source and moment | Composition / evidence |
| --- | --- | --- |
| `01-basic-exits.png` | Level 1, initial state | Cover image: full tray, both gate colors, readable title/timer; no result overlay |
| `02-blocked-movement.png` | Level 2, Blue held against Amber's blocking route | Selection and real contact response with the surrounding route; no Scene View repositioning |
| `03-soft-cube-burst.png` | Level 1 or 2, shortly after an exit | Roughly 0.23–0.32 s after logical exit: separated fragments, matching gate and readable remaining board |
| `04-traffic-lock.png` | Level 5, initial or early valid state | 7×9 obstacles and mixed footprints; cursor away from title and gates |
| `05-final-lockdown.png` | Level 8, initial state | Whole 8×11 final challenge, gates and HUD at the approved framing |
| `06-win-presentation.png` | Level 1, about 0.2–0.35 s into result reveal after the final burst | Actual win card and sparkles with Retry/Next readable; identify it as a Level 1 win |

Use **Image Sequence**, **Game View**, custom **1080×1920**, **PNG**, as documented in Unity's [Image Sequence Recorder reference](https://docs.unity3d.com/Packages/com.unity.recorder@5.1/manual/RecorderImage.html). Disable the Movie recorder for these takes. Record 1–2-second sequences around transient contact/burst/win moments and select an original frame; use an idle sequence for initial boards. Use the same tested real-time settings and unique names under `Recordings/M5/Stills/`. PNG avoids extracting compressed stills from MP4.

Keep source PNGs unaltered. Do not generate effects, composite a different HUD, stretch the board, hide obstacles or paint out pieces. The burst is cosmetic; a still alone cannot establish puzzle correctness, so accompany it with the recorded exit. Keep any explanatory caption outside the gameplay image in the portfolio page.

## Media handoff and exact M5-B manual actions

Once the capture copy and Recorder are prepared, the unavoidable manual work is:

1. Open FeedbackCampaign in that copy, select 1080×1920 Game View and enter Play Mode.
2. Configure recording as above and review the quality and countdown-duration checks.
3. Perform the shot-list actions using real dragging/Next and the 5–8 shortcuts. Record longer preparations where needed and keep each raw take.
4. Capture the six PNG subjects and select real frames from short image sequences.
5. Review MP4s/PNGs at native resolution, check original audio, and hand off good takes with level/take labels for a later editing stage.

No need to rerun setup menus, change scenes/timers, install an Android build or edit puzzle data. If Recorder causes compilation errors in the capture copy, stop capture preparation there and report them instead of changing the accepted project.

Raw files stay under the capture copy's `Recordings/` directory. M5-C supersedes the earlier proposed repository MP4 destination: distribute `ColorSlide3D_Full_Gameplay_Showcase.mp4` as a GitHub Release asset, retain only the selected PNGs in the main repository, and add the verified video URL after manual publication. See the [current media handoff](M5-C-Portfolio-Media.md). M5-A itself performed no editing, publishing or committing.
