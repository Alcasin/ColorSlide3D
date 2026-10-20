# ColorSlide3D — CV and portfolio descriptions

These descriptions reflect the inspected M0–M4.6 implementation at checkpoint `9102737`. The native test result is owner-reported; the M5-A static audit independently ran the managed subset. The approved gameplay recording is published as [ColorSlide3D — Full Gameplay Showcase](https://github.com/Alcasin/ColorSlide3D/releases/tag/portfolio-showcase-v1), alongside six genuine screenshots in the README.

## CV project entry

**ColorSlide3D | Unity 6, C#, URP, Input System | Puzzle game portfolio project**

- Implemented deterministic grid movement, irregular-footprint collision checks, color-matched exits, timed sessions and an eight-level campaign.
- Separated puzzle state from runtime level presentation; added adaptive portrait framing, mouse/primary-touch input, cosmetic exit effects and synthesized audio feedback.
- Validated gameplay and presentation with a 243-case Unity EditMode suite and solver-verified production-rule solution witnesses for advanced puzzles.

Repository: [github.com/Alcasin/ColorSlide3D](https://github.com/Alcasin/ColorSlide3D)

## GitHub portfolio overview

ColorSlide3D is an eight-level 3D sliding-block puzzle built with Unity 6 and C#. It combines deterministic occupancy and exit rules, solver-verified advanced puzzles, responsive portrait camera framing, Input System controls, custom visual and audio feedback, and automated Unity EditMode testing.

## Short interview introduction

“I built ColorSlide3D to practice separating puzzle rules from Unity presentation. The grid model validates every occupied cell and movement step, while separate systems handle pointer input, campaign transitions, camera fitting and feedback. I authored an eight-level progression and used production-rule solution witnesses to validate the advanced puzzles. The project has a 243-case EditMode suite, including regression tests for scene references, cleanup, UI input and camera projection. The exit burst is cosmetic, so the visual effect cannot change the puzzle outcome.”

## Evidence and claim boundaries

The [project audit](M5-Project-Audit.md) maps these claims to source files. The owner reports **243/243 native EditMode cases passing**; this audit independently compiled the assemblies and passed **179 managed cases**. Do not describe that managed run as a fresh native Unity run.

“Android portrait-oriented prototype” describes the target configuration and input implementation. It does not claim a tested Android device matrix, a shipped APK, a store release, measured mobile performance, commercial use or persistent saved progression. The solver validates witnesses; the master witnesses are not advertised as proven shortest solutions.

Use “cosmetic burst animation” rather than “physics-based destruction.” Keep the project description focused on the systems demonstrated in code and actual captured gameplay. Use the verified Release page linked above for the full gameplay showcase.
