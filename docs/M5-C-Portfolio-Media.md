# ColorSlide3D — M5-C portfolio media handoff

Media prepared **2026-10-20**; documentation finalized **2026-10-21** following the owner's Release publication. Official video title: **ColorSlide3D — Full Gameplay Showcase**. The approved continuous recording replaces the earlier proposed 56-second edit. No video editing or re-encoding was performed.

## Approved source and verification

The source remains in the separate capture project, outside this repository:

`C:\Users\alpr1\Desktop\Unity\ColorSlide3D_Capture\Recordings\ColorSlide3D_Full_Gameplay_Showcase.mp4`

| Property | Measured result |
| --- | --- |
| File size | **36,519,556 bytes** (36.52 MB; 34.83 MiB) |
| Container / duration | MP4; **00:02:29.600** (149.600 seconds) |
| Video | H.264/AVC (`avc1`); **1080×1920**, **9:16** |
| Frame rate / count | **60 FPS nominal**, **8,972 decoded frames** |
| Audio | MPEG-4 AAC (`mp4a`); **48,000 Hz**, **stereo** |
| SHA-256 | `e577f131de79fbabc66f27b39033ba6bd538f44ca2b6c3d9837395f0a4da9781` |

The installed OpenCV 4.13.0 FFmpeg backend decoded **all 8,972 frames**, each at 1080×1920. Python ISO BMFF inspection validated container box boundaries, video/audio sample counts and timing tables, and every media chunk's location within `mdat`. Video media duration is 149.5333 seconds; the container presentation ends at 149.600 seconds. These encoded properties are not Android performance measurements.

Representative frames across all eight levels and the final Campaign Complete screen were visually inspected. Audio stream metadata and packet/chunk structure were verified; audio was **not independently decoded or auditioned** during media preparation. This does not establish loudness, synchronization or dropout quality.

## Screenshot provenance

All six images are native **1080×1920 RGB PNGs**, losslessly encoded with Pillow optimization. They preserve the pixels decoded from the MP4; there is no crop, resize, retouching, composited HUD or added overlay. The video remains unchanged. Source compression is retained; PNG encoding cannot recover detail already lost in H.264.

Timestamps are relative to the start of the approved MP4, not the game timer. Frame indexes are zero-based and were selected through sequential decoding.

| Screenshot | Source timestamp | Frame | Subject | Bytes |
| --- | --- | --- | --- | ---: |
| [01-basic-gameplay.png](media/screenshots/01-basic-gameplay.png) | 00:00.000 | 0 | Level 1 — Basic Exits, full board and HUD | 161,360 |
| [02-block-interaction.png](media/screenshots/02-block-interaction.png) | 00:18.000 | 1,080 | Level 4 — selected piece among obstacles | 186,467 |
| [03-soft-cube-burst.png](media/screenshots/03-soft-cube-burst.png) | 00:18.450 | 1,107 | Level 4 — purple gate and exit fragments | 201,065 |
| [04-traffic-lock.png](media/screenshots/04-traffic-lock.png) | 00:24.200 | 1,452 | Level 5 — seven-piece 7×9 puzzle | 208,128 |
| [05-final-lockdown.png](media/screenshots/05-final-lockdown.png) | 01:41.400 | 6,084 | Level 8 — twelve-piece 8×11 puzzle | 257,657 |
| [06-campaign-complete.png](media/screenshots/06-campaign-complete.png) | 02:27.100 | 8,826 | Final campaign result with celebration | 138,185 |

The block-interaction still demonstrates selection; a single frame does not prove movement or a blocked gesture. The video supplies the temporal context. The burst is the actual cosmetic exit effect, and the completion card belongs to Level 8.

The [provenance manifest](media/screenshots/provenance.json) records source/file SHA-256 hashes, decoded RGB pixel hashes, dimensions, frame indexes and timestamps. All PNGs pass image-integrity checks and match their selected source-frame pixel hashes.

**New repository media:** **1,152,862 bytes** of PNGs plus **3,659 bytes** of provenance JSON = **1,156,521 bytes** total (1.16 MB; 1.10 MiB). The full MP4 is neither copied into this repository nor added to Git tracking.

## README and descriptions

The [README](../README.md) places the published video link and a **two-column, three-row gallery** before the gameplay details. Each image displays at 260 pixels wide and links to its original PNG. Captions identify the real levels and effects. Image paths, local document links and section anchors are checked; the video link uses the confirmed Release URL.

The professional introduction, eight-level table, mechanics, technology, installation instructions, camera/QA controls, test evidence and project limitations are preserved. CV/portfolio descriptions explicitly mention solver-verified advanced puzzles and Unity EditMode testing without claiming optimal solutions, device testing or publication. The older demo plan and audit have dated status notes so their historical findings remain identifiable.

## Published GitHub Release

The owner has published the showcase. The public Release page was independently opened on **2026-10-21**.

- **Title:** ColorSlide3D — Full Gameplay Showcase
- **Tag:** `portfolio-showcase-v1`
- **Release revision:** `c52b01c` (portfolio documentation and screenshots)
- **Attached asset:** `ColorSlide3D_Full_Gameplay_Showcase.mp4`, confirmed by the owner
- **Verified URL:** [Watch / Download Full Gameplay Showcase](https://github.com/Alcasin/ColorSlide3D/releases/tag/portfolio-showcase-v1)

Open the Release's **Assets** section and select the MP4 to watch or download the approximately **2:29 continuous showcase of all eight levels**, including Campaign Complete. The direct MP4 asset URL could not be independently verified during this update, so the README uses the verified Release page without guessing a download URL.

The Release is for portfolio gameplay media. No APK, installer or playable game build is attached; physical Android device testing and Google Play publication have not been performed. The MP4 remains outside the tracked source repository.

Publication and README linking are complete. This documentation update did not create, modify or republish the Release or its tag.

## Repository validation and handoff

- README: six unique PNG image paths, six full-resolution image links, two cells per gallery row; local documentation paths and anchors resolve.
- Media: six PNGs pass decoding/integrity checks; dimensions, source pixel hashes and file hashes match the provenance manifest. The approved source MP4 hash is unchanged.
- Protected content: all pre-task `Assets`, `Packages`, `ProjectSettings`, `Tools` and other non-documentation repository files are unchanged. Capture-project source, settings, documentation and recordings match the pre-task hash baseline.
- Initial media deliverables: README; status/description updates to the three existing M5 documents; this handoff; six PNGs and one provenance manifest. These deliverables are tracked at Release revision `c52b01c`. Final link updates change only README and the four M5 documents; the gallery images and provenance manifest remain unchanged.
- No source code or tests changed. No compilation or test run was needed for this documentation/media task. The latest author-confirmed result remains **243/243 Unity EditMode cases passed**; the M5-A audit's separate **179 managed passes** remain historical evidence.
- Media preparation and this final link update performed no Unity launch, batchmode, GUI automation, package installation, commit, push, tag creation or Release publication/modification. The owner published the Release separately. Git whitespace checks pass.

**Handoff complete:** the README links to the published showcase and preserves the six-image gallery. No media upload or video-link insertion remains outstanding.
