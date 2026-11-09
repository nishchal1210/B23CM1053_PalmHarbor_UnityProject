# Palm Harbor

A first-person coastal exploration project built in Unity. Palm Harbor recreates a stylized seaside district with pastel Art Deco hotels, neon signs, a diner, palm-lined streets, parked cars, a beach, and a boardwalk. Explore on foot, switch from golden-hour lighting to night, or watch a cinematic camera tour.

## Features

- **Walkable coastal district:** CharacterController-based movement with sprinting, jumping, and collision.
- **Day/night lighting:** Smooth transitions affect sunlight, ambient light, fog, sky, ocean colors, and neon lighting.
- **Interactive diner radio:** Toggle a spatial audio source that plays a synth loop generated in code.
- **Cinematic tour:** A looping 56-second camera route through eight viewpoints.
- **Photo capture:** Save PNG screenshots in `PalmHarborPhotos` under Unity's `Application.persistentDataPath`.
- **Rebuildable environment:** An editor tool generates the district, materials, textures, and combined meshes using a fixed random seed.
- **Custom rendering:** Ocean and sunset sky shaders, plus the Universal Render Pipeline (URP).

## Requirements

- Unity Hub and **Unity Editor 6000.5.9f1**, the version recorded in `ProjectSettings/ProjectVersion.txt`.
- A keyboard and mouse for exploration.
- Internet access on first import so Unity Package Manager can restore the packages declared in `Packages/manifest.json`.

Key packages include URP **17.5.0**, Input System **1.20.0**, and Unity UI **2.5.0**. The full dependency list and resolved versions are in `Packages/`.

## Open and run

1. Clone or download this repository.
2. In Unity Hub, add the project folder containing `Assets`, `Packages`, and `ProjectSettings`.
3. Open it with Unity **6000.5.9f1** and allow asset import and package restoration to finish.
4. Open `Assets/PalmHarbor/Scenes/PalmHarbor.unity`.
5. Press **Play** and click the Game view to capture the mouse.

The Palm Harbor scene is already enabled in the project's build scene list. To create a standalone build, open **File > Build Profiles**, select your target platform, install its build support if needed, and build the project.

## Controls

| Input | Action |
| --- | --- |
| W / A / S / D | Move |
| Mouse | Look around |
| Shift | Sprint |
| Space | Jump |
| E | Toggle the radio when near the diner radio interaction point |
| N | Toggle golden hour / night |
| F | Start or stop the cinematic tour |
| P | Save a photo |
| H | Show or hide the HUD |
| R | Return to the starting position |
| Esc | Toggle mouse capture |
| Left click | Capture the mouse when released |

## Project structure

```text
Assets/
  PalmHarbor/
    Editor/PalmHarborBuilder.cs       # District generation tool
    Materials/                       # Environment materials
    Meshes/                          # Generated combined geometry
    Scenes/PalmHarbor.unity           # Main scene
    Scripts/PalmHarborExplorer.cs     # Movement, interaction, lighting, tour, photos
    Scripts/PalmHarborVideoCapture.cs # Optional editor-only frame capture
    Shaders/                         # Ocean and sunset sky shaders
    Textures/                        # Generated surface textures
  Settings/                          # Rendering configuration
Packages/                            # Package manifest and lock file
ProjectSettings/                     # Unity project settings
tmp/encode_walkthrough.py             # Optional walkthrough video encoder
```

## Rebuild the district

Outside Play mode, select **Palm Harbor > Build Coastal District** in the Unity menu. The builder saves the current scene if it has changes, creates a new district, and writes it to `Assets/PalmHarbor/Scenes/PalmHarbor.unity`. It also updates generated assets and the build scene list.

Commit or back up scene and generated-asset edits before rebuilding: this operation overwrites the generated scene and can replace generated assets.

## Optional walkthrough capture

`PalmHarborVideoCapture` is an editor-only component. Add it to a scene object and explicitly call its public `Begin()` method during Play mode from editor tooling or your own caller; there is no capture keyboard shortcut. Its default settings record 40 seconds at 1280 x 720 and 20 frames per second, writing 800 JPEG frames to `Submission/VideoFrames/` in the project root.

`tmp/encode_walkthrough.py` is a separate Python utility that expects those 800 frames, adds generated music, encodes the walkthrough, and creates a review contact sheet. It uses NumPy, Pillow, and imageio-ffmpeg, as well as a Windows Segoe UI font for the contact sheet. These Python dependencies are only needed for this optional utility, not to open or play the Unity project.

## Version control

Track `Assets/` (including all `.meta` files), `Packages/`, and `ProjectSettings/`. Generated meshes and textures in `Assets/PalmHarbor/` are part of the project and should be committed.

The included `.gitignore` excludes Unity caches, logs, local user settings, generated IDE files, builds, and captured video frames. Unity regenerates its caches when someone opens a fresh clone.
